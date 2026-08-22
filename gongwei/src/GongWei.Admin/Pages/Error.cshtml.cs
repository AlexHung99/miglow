using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GongWei.Admin.Pages;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ErrorModel(ILogger<ErrorModel> logger) : PageModel
{
    public string? RequestId { get; private set; }

    public void OnGet()
    {
        RequestId = HttpContext.TraceIdentifier;

        // The detail stays in the server log — the page shows only the request id (spec §11).
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

        if (feature?.Error is { } error)
        {
            logger.LogError(error, "Unhandled admin error on {Path}", feature.Path);
        }
    }
}
