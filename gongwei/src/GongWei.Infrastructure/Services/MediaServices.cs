using System.Security.Cryptography;
using GongWei.Application.Abstractions;
using GongWei.Domain.Common;
using GongWei.Domain.Identity;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace GongWei.Infrastructure.Services;

public sealed class MediaStorageOptions
{
    /// <summary>
    /// Absolute path to the persistent media volume. Must live outside the IIS web root
    /// and outside the deployment directory (spec §2.3).
    /// </summary>
    public string RootPath { get; set; } = null!;
}

/// <summary>
/// Local-disk media storage. Swapping in S3-compatible object storage means replacing
/// this class only — nothing above <see cref="IMediaStorage"/> changes.
/// </summary>
public sealed class FileSystemMediaStorage(IOptions<MediaStorageOptions> options) : IMediaStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(string storageKey, Stream content, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);

        return storageKey;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);

        return Task.FromResult<Stream?>(
            File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Keys are generated server-side, but this still refuses anything that would escape
    /// the media root — one traversal bug should not be enough to read the whole disk.
    /// </summary>
    private string ResolvePath(string storageKey)
    {
        if (storageKey.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(storageKey)
            || storageKey.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Rejected storage key '{storageKey}'.");
        }

        var path = Path.GetFullPath(Path.Combine(_root, storageKey));

        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Storage key '{storageKey}' escapes the media root.");
        }

        return path;
    }
}

/// <summary>
/// Portrait pipeline from spec §6.8: identify by magic bytes rather than by extension or
/// Content-Type, cap the pixel count to stop decode bombs, drop every metadata frame and
/// re-encode to a single-frame WebP.
/// </summary>
public sealed class ImageSharpPortraitProcessor : IImageProcessor
{
    /// <summary>Total pixels a decoded upload may occupy — 4096x4096 is already generous.</summary>
    private const int MaxPixels = 4096 * 4096;

    private static readonly string[] AllowedFormats = ["JPEG", "PNG", "WebP"];

    public async Task<ProcessedImage> ProcessPortraitAsync(Stream upload, CancellationToken ct = default)
    {
        // Buffer first: the format probe and the decode both need to read from the start.
        using var buffer = new MemoryStream();
        await upload.CopyToAsync(buffer, ct);

        if (buffer.Length == 0)
        {
            throw DomainException.UnsupportedMedia("上傳的檔案是空的。");
        }

        if (buffer.Length > MediaAsset.MaxByteSize)
        {
            throw DomainException.TooLarge(
                $"圖片不可超過 {MediaAsset.MaxByteSize / 1024 / 1024} MB。");
        }

        buffer.Position = 0;

        // Magic bytes only — the declared Content-Type and file name are never trusted.
        var format = await Image.DetectFormatAsync(buffer, ct);

        if (format is null || !AllowedFormats.Contains(format.Name, StringComparer.OrdinalIgnoreCase))
        {
            throw DomainException.UnsupportedMedia("只接受 JPEG、PNG 或 WebP 圖片。");
        }

        buffer.Position = 0;

        var info = await Image.IdentifyAsync(buffer, ct);

        if ((long)info.Width * info.Height > MaxPixels)
        {
            throw DomainException.Validation("圖片像素總量過大，請先縮小後再上傳。");
        }

        buffer.Position = 0;

        using var image = await Image.LoadAsync(buffer, ct);

        if (image.Width < MediaAsset.MinWidth || image.Height < MediaAsset.MinHeight)
        {
            throw DomainException.Validation(
                $"圖片尺寸至少需 {MediaAsset.MinWidth} × {MediaAsset.MinHeight}。");
        }

        // Strip EXIF/XMP/ICC and flatten any animation down to the first frame.
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;

        while (image.Frames.Count > 1)
        {
            image.Frames.RemoveFrame(image.Frames.Count - 1);
        }

        if (image.Width > MediaAsset.MaxDimension || image.Height > MediaAsset.MaxDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MediaAsset.MaxDimension, MediaAsset.MaxDimension)
            }));
        }

        using var output = new MemoryStream();
        await image.SaveAsync(output, new WebpEncoder { Quality = 85 }, ct);

        var bytes = output.ToArray();

        if (bytes.LongLength > MediaAsset.MaxByteSize)
        {
            throw DomainException.TooLarge("轉檔後的圖片仍然過大，請縮小後再試。");
        }

        return new ProcessedImage(bytes, "image/webp", image.Width, image.Height, SHA256.HashData(bytes));
    }
}
