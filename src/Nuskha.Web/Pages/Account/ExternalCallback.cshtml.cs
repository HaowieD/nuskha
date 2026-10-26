using System.Security.Claims;

namespace Nuskha.Web.Pages.Account;

/// <summary>Finishes Google sign-in: signs in a linked account, links to an existing verified account, or creates a new one.</summary>
[EnableRateLimiting("auth")]
public class ExternalCallbackModel(SignInManager<AppUser> signIn, UserManager<AppUser> users) : AppPageModel
{
    public async Task<IActionResult> OnGetAsync(string? returnUrl, string? remoteError)
    {
        var target = SafeReturnUrl(returnUrl);
        if (remoteError is not null) return Fail("auth.google.failed");

        var info = await signIn.GetExternalLoginInfoAsync();
        if (info is null) return Fail("auth.google.failed");

        var existing = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
        if (existing.Succeeded) return LocalRedirect(target);
        if (existing.IsLockedOut) return Fail("auth.locked");

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email)) return Fail("auth.google.noemail");

        var user = await users.FindByEmailAsync(email);
        if (user is not null)
        {
            // Linking to a password account whose e-mail was never verified would let anyone who pre-registered
            // somebody else's address keep access after the real owner signs in with Google. Refuse instead.
            if (!user.EmailConfirmed) return Fail("auth.google.exists");
            if (!(await users.AddLoginAsync(user, info)).Succeeded) return Fail("auth.google.failed");
        }
        else
        {
            var name = info.Principal.FindFirstValue(ClaimTypes.Name)?.Trim();
            var userName = await UniqueUserNameAsync(email.Split('@')[0]);
            user = new AppUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                DisplayName = string.IsNullOrEmpty(name) ? userName : (name.Length > 60 ? name[..60] : name),
            };
            if (!(await users.CreateAsync(user)).Succeeded) return Fail("auth.google.failed");
            await users.AddToRoleAsync(user, Roles.User);
            if (!(await users.AddLoginAsync(user, info)).Succeeded) return Fail("auth.google.failed");
        }

        await signIn.SignInAsync(user, isPersistent: true, info.LoginProvider);
        return LocalRedirect(target);
    }

    private IActionResult Fail(string messageKey)
    {
        TempData["AuthError"] = messageKey;
        return RedirectToPage("/Account/Login");
    }

    private async Task<string> UniqueUserNameAsync(string seed)
    {
        var baseName = UsernameRules.Suggest(seed);
        for (var i = 0; i < 200; i++)
        {
            var suffix = i == 0 ? "" : i.ToString();
            var candidate = baseName[..Math.Min(baseName.Length, 24 - suffix.Length)] + suffix;
            if (UsernameRules.Validate(candidate) is null && await users.FindByNameAsync(candidate) is null) return candidate;
        }
        return "reader" + Guid.NewGuid().ToString("N")[..10];
    }
}
