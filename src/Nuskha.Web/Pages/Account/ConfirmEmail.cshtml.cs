namespace Nuskha.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class ConfirmEmailModel(UserManager<AppUser> users) : AppPageModel
{
    public bool Success { get; private set; }

    public async Task OnGetAsync(Guid userId, string? code)
    {
        var token = AccountEmails.DecodeToken(code);
        var user = userId == Guid.Empty || token is null ? null : await users.FindByIdAsync(userId.ToString());
        if (user is null) return;
        // Idempotent: mail scanners often open links before the person does, so an already-confirmed address counts as success.
        Success = user.EmailConfirmed || (await users.ConfirmEmailAsync(user, token)).Succeeded;
    }
}
