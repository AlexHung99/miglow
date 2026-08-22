namespace GongWei.Domain.Common;

/// <summary>
/// Stable machine-readable error codes. These are the exact strings the front end
/// switches on, taken from api_v1_v1.0.md §1.4 — renaming one is a breaking change.
/// </summary>
public static class ErrorCodes
{
    // --- common contract (api_v1_v1.0.md §1.4) ---
    public const string ValidationFailed = "VALIDATION_FAILED";          // 400
    public const string AuthRequired = "AUTH_REQUIRED";                  // 401
    public const string SessionExpired = "SESSION_EXPIRED";              // 401
    public const string Forbidden = "FORBIDDEN";                         // 403
    public const string CharacterStateForbidden = "CHARACTER_STATE_FORBIDDEN"; // 403
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";         // 404
    public const string VersionConflict = "VERSION_CONFLICT";            // 409
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED"; // 409
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";           // 413
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE"; // 415
    public const string PreconditionRequired = "PRECONDITION_REQUIRED";  // 428
    public const string RateLimited = "RATE_LIMITED";                    // 429
    public const string InternalError = "INTERNAL_ERROR";                // 500
    public const string MaintenanceMode = "MAINTENANCE_MODE";            // 503

    // --- auth (line_login_v1.1.md §6) ---
    // Deliberately coarse: the player-visible code must not reveal which internal step
    // failed, so several distinct causes deliberately collapse onto one code.
    public const string LineAccessDenied = "LINE_ACCESS_DENIED";              // 303 — player cancelled
    public const string AuthStateInvalid = "AUTH_STATE_INVALID";              // 400/303
    public const string AuthStateExpired = "AUTH_STATE_EXPIRED";              // 400/303
    public const string AuthStateReplayed = "AUTH_STATE_REPLAYED";            // 400/303
    public const string AuthLineTokenFailed = "AUTH_LINE_TOKEN_FAILED";       // 502/303
    public const string AuthIdTokenInvalid = "AUTH_ID_TOKEN_INVALID";         // 401/303
    public const string AuthAccountSuspended = "AUTH_ACCOUNT_SUSPENDED";      // 403/303
    public const string AuthRateLimited = "AUTH_RATE_LIMITED";                // 429
    public const string AuthAttemptUnprotectFailed = "AUTH_ATTEMPT_UNPROTECT_FAILED";
    public const string AuthReturnUrlInvalid = "AUTH_RETURN_URL_INVALID";     // 400
    public const string CsrfInvalid = "CSRF_INVALID";                         // 403

    // --- character application ---
    public const string OpenApplicationExists = "OPEN_APPLICATION_EXISTS";
    public const string CurrentCharacterExists = "CURRENT_CHARACTER_EXISTS";

    // --- events ---
    public const string EventFull = "EVENT_FULL";
    public const string EventNotOpen = "EVENT_NOT_OPEN";

    // --- economy ---
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
    public const string SoldOut = "SOLD_OUT";
    public const string InsufficientItems = "INSUFFICIENT_ITEMS";
    public const string PurchaseLimitReached = "PURCHASE_LIMIT_REACHED";

    // --- reproduction ---
    public const string HeirCapacityExhausted = "HEIR_CAPACITY_EXHAUSTED";
    public const string ReproductionClosed = "REPRODUCTION_CLOSED";

    // --- approval ---
    public const string SelfApprovalForbidden = "SELF_APPROVAL_FORBIDDEN";
    public const string ApprovalNotApproved = "APPROVAL_NOT_APPROVED";
    public const string ApprovalExpired = "APPROVAL_EXPIRED";

    // --- operations ---
    public const string JobAlreadyRunning = "JOB_ALREADY_RUNNING";
    public const string RequestInProgress = "REQUEST_IN_PROGRESS";
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string ConflictState = "CONFLICT_STATE";
}
