namespace Nuskha.Web.Pages.Social;

[Authorize]
[EnableRateLimiting("write")]
public class FollowModel(SocialService social, NuskhaDbContext db) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid userId, CancellationToken ct)
    {
        var following = await social.ToggleFollowAsync(User.GetUserId()!.Value, userId, ct);
        if (following is null) return BadRequest();
        var count = await db.Follows.CountAsync(f => f.FolloweeId == userId, ct);
        return Partial("_FollowButton", new FollowButtonModel(userId, following.Value, count));
    }
}
