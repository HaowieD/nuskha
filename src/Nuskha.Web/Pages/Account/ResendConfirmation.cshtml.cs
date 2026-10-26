namespace Nuskha.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class ResendConfirmationModel(UserManager<AppUser> users, AccountEmails emails) : AppPageModel
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

        // The answer is the same whether or not the address exists, so this form cannot be used to probe for accounts.
        var user = await users.FindByEmailAsync(Email);
        if (user is { EmailConfirmed: false })
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            await emails.SendConfirmationAsync(user, token, ct);
        }
        Sent = true;
        return Page();
    }
}
