using System.Net.Mail;

namespace Nuskha.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class RegisterModel(UserManager<AppUser> users, SignInManager<AppUser> signIn, GoogleAuthState google) : AppPageModel
{
    [BindProperty] public string? UserName { get; set; }
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? DisplayName { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? ConfirmPassword { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public bool GoogleEnabled => google.Enabled;

    public IActionResult OnGet() => IsLoggedIn ? LocalRedirect(SafeReturnUrl(ReturnUrl)) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        UserName = UserName?.Trim();
        Email = Email?.Trim();
        DisplayName = DisplayName?.Trim();

        if (UsernameRules.Validate(UserName) is { } userError) Errors[nameof(UserName)] = userError;
        if (!IsValidEmail(Email)) Errors[nameof(Email)] = "val.email";
        if (DisplayName is { Length: > 60 }) Errors[nameof(DisplayName)] = "val.displayname";
        if (string.IsNullOrEmpty(Password) || Password.Length < 8) Errors[nameof(Password)] = "val.password";
        else if (Password != ConfirmPassword) Errors[nameof(ConfirmPassword)] = "val.password.mismatch";
        if (HasErrors) return Page();

        var user = new AppUser
        {
            UserName = UserName,
            Email = Email,
            DisplayName = string.IsNullOrEmpty(DisplayName) ? UserName! : DisplayName,
        };
        var result = await users.CreateAsync(user, Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) Map(error);
            if (!HasErrors) Errors["Form"] = "common.error";
            return Page();
        }

        await users.AddToRoleAsync(user, Roles.User);
        await signIn.SignInAsync(user, isPersistent: true);
        return LocalRedirect(SafeReturnUrl(ReturnUrl));
    }

    private void Map(IdentityError error)
    {
        switch (error.Code)
        {
            case "DuplicateUserName": Errors[nameof(UserName)] = "val.username.taken"; break;
            case "InvalidUserName": Errors[nameof(UserName)] = "val.username"; break;
            case "DuplicateEmail": Errors[nameof(Email)] = "val.email.taken"; break;
            case "InvalidEmail": Errors[nameof(Email)] = "val.email"; break;
            default:
                if (error.Code.StartsWith("Password", StringComparison.Ordinal)) Errors[nameof(Password)] = "val.password";
                break;
        }
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256) return false;
        try { return new MailAddress(email).Address == email; }
        catch (FormatException) { return false; }
    }
}
