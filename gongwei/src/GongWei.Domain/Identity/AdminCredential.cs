using GongWei.Domain.Common;

namespace GongWei.Domain.Identity;

/// <summary>
/// Table: admin_credentials — a username and password for the control back office.
///
/// This is a deliberate departure from backend_spec_v1.1 §155, where admin identity also
/// arrives through LINE Login and the admin site only differs by cookie. A local
/// credential is a second door into the most privileged account in the system, and it has
/// none of what the LINE path gets for free: no identity provider, no device trust, no
/// second factor. Everything that partially compensates is here — lockout, an audit trail
/// on both success and failure, and a hash the database cannot leak usefully.
///
/// One rule this type exists to enforce: a local admin can reach the admin site and
/// nothing else. Player sessions are minted only by the LINE callback, so an account with
/// no LINE subject cannot obtain one.
/// </summary>
public class AdminCredential : IVersioned
{
    /// <summary>Also the primary key: one credential per user, never a second.</summary>
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Username { get; set; } = null!;

    /// <summary>
    /// Self-describing digest, <c>pbkdf2-sha256$&lt;iterations&gt;$&lt;salt&gt;$&lt;hash&gt;</c>.
    /// The parameters travel with the hash so they can be raised later without a flag day.
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>Set when an operator issues the password, cleared once the holder changes it.</summary>
    public bool MustChangePassword { get; set; }

    public int FailedAttempts { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset PasswordChangedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsLockedAt(DateTimeOffset now) => LockedUntil is { } until && until > now;

    /// <summary>
    /// Records a failed attempt and locks the account once the threshold is reached.
    /// Counting failures rather than rate-limiting by address is what makes this useful
    /// against a distributed guessing attempt.
    /// </summary>
    public void RegisterFailure(DateTimeOffset now)
    {
        FailedAttempts++;

        if (FailedAttempts >= AdminPasswordPolicy.MaxFailedAttempts)
        {
            LockedUntil = now.Add(AdminPasswordPolicy.LockoutDuration);
        }
    }

    public void RegisterSuccess(DateTimeOffset now)
    {
        FailedAttempts = 0;
        LockedUntil = null;
        LastLoginAt = now;
    }
}

/// <summary>Rules for local admin passwords and lockout.</summary>
public static class AdminPasswordPolicy
{
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 64;

    /// <summary>
    /// Long rather than complex. A length floor resists guessing far better than a rule
    /// demanding one symbol, which mostly produces "Password1!".
    /// </summary>
    public const int MinPasswordLength = 12;

    /// <summary>Bounded so a huge input cannot turn the hash into a denial of service.</summary>
    public const int MaxPasswordLength = 256;

    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Synthetic LINE subject for a local admin. Prefixed so it can never collide with a
    /// real one — LINE subjects are 33 characters beginning with 'U' and contain no colon.
    /// </summary>
    public static string SyntheticLineUserId(string username) => $"local:{username}";

    public static bool IsLocalAccount(string lineUserId) =>
        lineUserId.StartsWith("local:", StringComparison.Ordinal);

    /// <summary>Returns null when the username is acceptable, or the reason it is not.</summary>
    public static string? ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "帳號不得為空。";
        }

        var trimmed = username.Trim();

        if (trimmed.Length is < MinUsernameLength or > MaxUsernameLength)
        {
            return $"帳號長度必須介於 {MinUsernameLength} 至 {MaxUsernameLength} 字元。";
        }

        // ASCII only: a username that differs from another only by a homoglyph or an
        // invisible character is a way to impersonate an existing admin in an audit log.
        if (!trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
        {
            return "帳號只能包含英數字與 . _ - 。";
        }

        return null;
    }

    /// <summary>Returns null when the password is acceptable, or the reason it is not.</summary>
    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "密碼不得為空。";
        }

        if (password.Length < MinPasswordLength)
        {
            return $"密碼至少需要 {MinPasswordLength} 個字元。";
        }

        if (password.Length > MaxPasswordLength)
        {
            return $"密碼不得超過 {MaxPasswordLength} 個字元。";
        }

        if (password.Any(char.IsControl))
        {
            return "密碼不得包含控制字元。";
        }

        return null;
    }
}
