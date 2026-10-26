namespace Nuskha.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class ForgotPasswordModel(UserManager<AppUser> users, AccountEmails emails) : AppPageModel
{
    [BindProperty] public string? Email { get; set; }
    public bool Sent { get; private set; }
    public bool EmailEnabled => emails.Enabled;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!emails.Enabled) return Page();
        Email = Email?.Trim();
        if (string.IsNullOrEmpty(Email)) { Errors[nameof(Email)] = "val.required"; return Page(); }

        // Same neutral answer whether or not the address is registered.
        var user = await users.FindByEmailAsync(Email);
        if (user is not null && !await users.IsLockedOutAsync(user))
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            await emails.SendPasswordResetAsync(user, token, ct);
        }
        Sent = true;
        return Page();
    }
}
