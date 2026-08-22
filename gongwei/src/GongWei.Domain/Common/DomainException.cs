namespace GongWei.Domain.Common;

/// <summary>
/// How a <see cref="DomainException"/> maps onto HTTP. The Domain layer stays free of
/// ASP.NET types; the API's Problem Details handler does the translation, following the
/// status/code pairs in api_v1_v1.0.md §1.4.
/// </summary>
public enum DomainErrorKind
{
    Validation,         // 400
    Unauthenticated,    // 401
    Forbidden,          // 403
    NotFound,           // 404
    Conflict,           // 409
    PayloadTooLarge,    // 413
    UnsupportedMedia,   // 415
    PreconditionRequired, // 428
    RateLimited,        // 429
    Maintenance         // 503
}

/// <summary>
/// A rule violation the caller can understand and act on. Anything that is a bug rather
/// than a rule violation should throw an ordinary exception instead — those become a
/// bare 500 with nothing but a request id (§11).
/// </summary>
public sealed class DomainException : Exception
{
    public string Code { get; }

    public DomainErrorKind Kind { get; }

    /// <summary>Extra machine-readable context, e.g. currentVersion on a conflict.</summary>
    public IReadOnlyDictionary<string, object?> Extensions { get; }

    /// <summary>
    /// Field-level messages rendered as the Problem Details <c>errors</c> object,
    /// e.g. <c>{"biography": ["自介至少需要 200 字"]}</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public DomainException(
        string code,
        string message,
        DomainErrorKind kind = DomainErrorKind.Conflict,
        IReadOnlyDictionary<string, object?>? extensions = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Code = code;
        Kind = kind;
        Extensions = extensions ?? new Dictionary<string, object?>();
        Errors = errors;
    }

    public static DomainException Validation(string message, string? code = null) =>
        new(code ?? ErrorCodes.ValidationFailed, message, DomainErrorKind.Validation);

    /// <summary>A 400 carrying per-field messages, which is what the build-a-character form needs.</summary>
    public static DomainException FieldErrors(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorCodes.ValidationFailed,
            "Request validation failed",
            DomainErrorKind.Validation,
            errors: errors);

    /// <summary>
    /// 404 is also used where the caller has no right to know the resource exists
    /// (api_v1_v1.0.md §1.4), so ids stay unguessable.
    /// </summary>
    public static DomainException NotFound(string what, object id) =>
        new(ErrorCodes.ResourceNotFound, $"{what} '{id}' was not found.", DomainErrorKind.NotFound);

    public static DomainException Forbidden(string message, string? code = null) =>
        new(code ?? ErrorCodes.Forbidden, message, DomainErrorKind.Forbidden);

    public static DomainException CharacterState(string message) =>
        new(ErrorCodes.CharacterStateForbidden, message, DomainErrorKind.Forbidden);

    public static DomainException Conflict(string code, string message) =>
        new(code, message);

    public static DomainException VersionConflict(long currentVersion) =>
        new(ErrorCodes.VersionConflict,
            "The resource was modified by someone else. Re-read it and retry.",
            DomainErrorKind.Conflict,
            new Dictionary<string, object?> { ["currentVersion"] = currentVersion });

    /// <summary>415 — the bytes are not a format we accept, whatever the client declared.</summary>
    public static DomainException UnsupportedMedia(string message) =>
        new(ErrorCodes.UnsupportedMediaType, message, DomainErrorKind.UnsupportedMedia);

    /// <summary>413 — over the endpoint's size limit.</summary>
    public static DomainException TooLarge(string message) =>
        new(ErrorCodes.PayloadTooLarge, message, DomainErrorKind.PayloadTooLarge);

    public static DomainException PreconditionRequired() =>
        new(ErrorCodes.PreconditionRequired,
            "This endpoint requires an If-Match header carrying the resource version.",
            DomainErrorKind.PreconditionRequired);
}
