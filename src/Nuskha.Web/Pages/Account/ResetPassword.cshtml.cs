namespace Nuskha.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class ResetPasswordModel(UserManager<AppUser> users) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public Guid UserId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Code { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? ConfirmPassword { get; set; }

    public bool Done { get; private set; }
    public bool Invalid { get; private set; }

    public void OnGet() => Invalid = UserId == Guid.Empty || AccountEmails.DecodeToken(Code) is null;

    public async Task<IActionResult> OnPostAsync()
    {
        var token = AccountEmails.DecodeToken(Code);
        if (UserId == Guid.Empty || token is null) { Invalid = true; return Page(); }

        if (string.IsNullOrEmpty(Password) || Password.Length < 8) Errors[nameof(Password)] = "val.password";
        else if (Password != ConfirmPassword) Errors[nameof(ConfirmPassword)] = "val.password.mismatch";
        if (HasErrors) return Page();

        var user = await users.FindByIdAsync(UserId.ToString());
        if (user is null) { Invalid = true; return Page(); }

        var result = await users.ResetPasswordAsync(user, token, Password!);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.Ordinal)))
                Errors[nameof(Password)] = "val.password";
            else
                Invalid = true;
            return Page();
        }

        // Following the mailed link proves control of the mailbox, so the address counts as confirmed.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await users.UpdateAsync(user);
        }
        Done = true;
        return Page();
    }
}
