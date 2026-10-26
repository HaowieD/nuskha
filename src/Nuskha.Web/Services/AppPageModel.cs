using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Nuskha.Web.Services;

public abstract class AppPageModel : PageModel
{
    protected Guid? CurrentUserId => User.GetUserId();
    protected bool IsLoggedIn => User.Identity?.IsAuthenticated == true;
    protected bool IsHtmx => Request.Headers.ContainsKey("HX-Request");

    /// <summary>Field name -> localization key. Rendered with the _FieldError partial.</summary>
    public Dictionary<string, string> Errors { get; } = new();
    public bool HasErrors => Errors.Count > 0;

    protected string SafeReturnUrl(string? url) => WebExtensions.IsLocalUrl(url) ? url! : "/";
}
