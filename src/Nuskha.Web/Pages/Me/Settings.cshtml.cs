namespace Nuskha.Web.Pages.Me;

[EnableRateLimiting("write")]
[RequestSizeLimit(6 * 1024 * 1024)] // avatar + banner, 2 MB each plus form overhead
public class SettingsModel(UserManager<AppUser> users, SignInManager<AppUser> signIn, UploadService uploads) : AppPageModel
{
    [BindProperty] public string? DisplayName { get; set; }
    [BindProperty] public string? Bio { get; set; }
    // Unchecked checkboxes post nothing, so false is the correct default for a POST; OnGet fills the stored value.
    [BindProperty] public bool ListsArePublic { get; set; }
    [BindProperty] public bool RemoveAvatar { get; set; }
    [BindProperty] public IFormFile? Avatar { get; set; }
    [BindProperty] public bool RemoveBanner { get; set; }
    [BindProperty] public IFormFile? Banner { get; set; }

    public string UserName { get; private set; } = "";
    public string? AvatarPath { get; private set; }
    public string? BannerPath { get; private set; }
    public bool HasPassword { get; private set; }
    public bool GoogleLinked { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await CurrentAsync();
        if (user is null) return Challenge();
        DisplayName = user.DisplayName;
        Bio = user.Bio;
        ListsArePublic = user.ListsArePublic;
        await FillAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostProfileAsync(CancellationToken ct)
    {
        var user = await CurrentAsync();
        if (user is null) return Challenge();

        DisplayName = DisplayName?.Trim();
        Bio = TextUtil.NullIfBlank(Bio);
        if (string.IsNullOrEmpty(DisplayName)) Errors[nameof(DisplayName)] = "val.required";
        else if (DisplayName.Length > 60) Errors[nameof(DisplayName)] = "val.displayname";
        if (Bio is { Length: > 2000 }) Errors[nameof(Bio)] = "val.toolong";

        string? newAvatar = null;
        if (!HasErrors && Avatar is { Length: > 0 })
        {
            newAvatar = await uploads.SaveImageAsync(Avatar, "avatars", ct);
            if (newAvatar is null) Errors[nameof(Avatar)] = "val.image";
        }
        string? newBanner = null;
        if (!HasErrors && Banner is { Length: > 0 })
        {
            newBanner = await uploads.SaveImageAsync(Banner, "banners", ct);
            if (newBanner is null) Errors[nameof(Banner)] = "val.image";
        }
        if (HasErrors)
        {
            // Do not leave a freshly stored avatar behind when the banner (or anything else) was rejected.
            if (newAvatar is not null) uploads.Delete(newAvatar);
            await FillAsync(user);
            return Page();
        }

        user.DisplayName = DisplayName!;
        user.Bio = Bio;
        user.ListsArePublic = ListsArePublic;
        var oldBanner = user.BannerPath;
        if (newBanner is not null) user.BannerPath = newBanner;
        else if (RemoveBanner) user.BannerPath = null;
        var oldAvatar = user.AvatarPath;
        if (newAvatar is not null) user.AvatarPath = newAvatar;
        else if (RemoveAvatar) user.AvatarPath = null;

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            Errors["Form"] = "common.error";
            await FillAsync(user);
            return Page();
        }
        if (oldAvatar is not null && oldAvatar != user.AvatarPath) uploads.Delete(oldAvatar);
        if (oldBanner is not null && oldBanner != user.BannerPath) uploads.Delete(oldBanner);

        TempData["Flash"] = "settings.saved";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPasswordAsync(string? currentPassword, string? newPassword, string? confirmPassword)
    {
        var user = await CurrentAsync();
        if (user is null) return Challenge();
        DisplayName = user.DisplayName;
        Bio = user.Bio;
        ListsArePublic = user.ListsArePublic;
        await FillAsync(user);

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8) Errors["NewPassword"] = "val.password";
        else if (newPassword != confirmPassword) Errors["ConfirmPassword"] = "val.password.mismatch";
        if (HasPassword && string.IsNullOrEmpty(currentPassword)) Errors["CurrentPassword"] = "val.required";
        if (HasErrors) return Page();

        var result = HasPassword
            ? await users.ChangePasswordAsync(user, currentPassword!, newPassword!)
            : await users.AddPasswordAsync(user, newPassword!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                if (error.Code == "PasswordMismatch") Errors["CurrentPassword"] = "auth.invalid";
                else if (error.Code.StartsWith("Password", StringComparison.Ordinal)) Errors["NewPassword"] = "val.password";
            }
            if (!HasErrors) Errors["Form"] = "common.error";
            return Page();
        }

        await signIn.RefreshSignInAsync(user);
        TempData["Flash"] = "settings.password.saved";
        return RedirectToPage();
    }

    private async Task<AppUser?> CurrentAsync() => CurrentUserId is Guid id ? await users.FindByIdAsync(id.ToString()) : null;

    private async Task FillAsync(AppUser user)
    {
        UserName = user.UserName!;
        AvatarPath = user.AvatarPath;
        BannerPath = user.BannerPath;
        HasPassword = await users.HasPasswordAsync(user);
        GoogleLinked = (await users.GetLoginsAsync(user)).Any(l => l.LoginProvider == "Google");
    }
}
