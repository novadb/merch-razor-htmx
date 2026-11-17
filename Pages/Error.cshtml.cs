using Merch.Site;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Merch.Pages;

/// <summary>
/// The page for a 404 (re-executed by <c>UseStatusCodePagesWithReExecute</c>) and an error (<c>UseExceptionHandler</c>),
/// in the region of the URL that failed, else <c>us</c>.
/// </summary>
public sealed class ErrorModel(RegionContext site, ILogger<ErrorModel> logger) : PageModel
{
    public bool IsNotFound { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? code)
    {
        var failedPath = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath
            ?? HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path ?? "";
        IsNotFound = code == StatusCodes.Status404NotFound;
        Response.StatusCode = code ?? StatusCodes.Status500InternalServerError;
        try
        {
            await site.LoadAsync(Region.Find(failedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()) ?? Region.Us);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // NovaDB cannot be reached: there are no labels, the page stands alone in English.
            logger.LogError(e, "The error page has no texts");
        }
        return Page();
    }
}
