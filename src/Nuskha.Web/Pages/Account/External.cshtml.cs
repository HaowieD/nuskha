namespace Nuskha.Web.Pages.Account;

/// <summary>Starts the Google sign-in round trip. POST only, so it is protected by the antiforgery token.</summary>
[EnableRateLimiting("auth")]
public class ExternalModel(SignInManager<AppUser> signIn, GoogleAuthState google) : AppPageModel
{
    public IActionResult OnGet() => RedirectToPage("/Account/Login");

    public IActionResult OnPost(string? returnUrl)
    {
        if (!google.Enabled) return NotFound();
        var callback = Url.Page("/Account/ExternalCallback", new { returnUrl = SafeReturnUrl(returnUrl) });
        var properties = signIn.ConfigureExternalAuthenticationProperties("Google", callback);
        return Challenge(properties, "Google");
    }
}
