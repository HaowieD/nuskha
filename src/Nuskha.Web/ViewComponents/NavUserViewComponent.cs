using Microsoft.AspNetCore.Mvc;

namespace Nuskha.Web.ViewComponents;

public record NavUserVm(bool IsAuthenticated, string? UserName, string? DisplayName, string? AvatarPath, int Unread, bool IsStaff, bool GoogleEnabled);

public class NavUserViewComponent(NuskhaDbContext db, GoogleAuthState google) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = HttpContext.User;
        var id = user.GetUserId();
        if (id is null || user.Identity?.IsAuthenticated != true)
            return View(new NavUserVm(false, null, null, null, 0, false, google.Enabled));

        var me = await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new { u.UserName, u.DisplayName, u.AvatarPath }).FirstOrDefaultAsync();
        var unread = await db.Notifications.CountAsync(n => n.UserId == id && !n.IsRead);
        return View(new NavUserVm(true, me?.UserName, me?.DisplayName, me?.AvatarPath, unread, user.IsStaff(), google.Enabled));
    }
}
