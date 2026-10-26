namespace Nuskha.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class LoginModel(SignInManager<AppUser> signIn, UserManager<AppUser> users, GoogleAuthState google, AccountEmails emails) : AppPageModel
{
    [BindProperty] public string? Login { get; set; }
    [BindProperty] public string? Password { get; set; }
    // Checkboxes post nothing when unchecked, so the default must be false; OnGet pre-ticks it for display.
    [BindProperty] public bool RememberMe { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public bool GoogleEnabled => google.Enabled;
    public bool EmailEnabled => emails.Enabled;
    /// <summary>Localization key of an error to show above the form.</summary>
    public string? Message { get; private set; }

    public IActionResult OnGet()
    {
        if (IsLoggedIn) return LocalRedirect(SafeReturnUrl(ReturnUrl));
        RememberMe = true;
        Message = TempData["AuthError"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var id = Login?.Trim();
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(Password))
        {
            Errors[nameof(Login)] = "val.required";
            return Page();
        }

        var user = id.Contains('@') ? await users.FindByEmailAsync(id) : await users.FindByNameAsync(id);
        if (user is null)
        {
            Message = "auth.invalid";
            return Page();
        }

        var result = await signIn.PasswordSignInAsync(user, Password, RememberMe, lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect(SafeReturnUrl(ReturnUrl));

        Message = result.IsLockedOut ? "auth.locked" : result.IsNotAllowed ? "auth.unconfirmed" : "auth.invalid";
        return Page();
    }
}
