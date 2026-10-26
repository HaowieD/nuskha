namespace Nuskha.Web.Pages.Social;

[Authorize]
[EnableRateLimiting("write")]
public class LikeModel(SocialService social) : PageModel
{
    public async Task<IActionResult> OnPostAsync(int type, Guid id, CancellationToken ct)
    {
        if (!Enum.IsDefined((TargetType)type)) return BadRequest();
        var t = (TargetType)type;
        var state = await social.ToggleLikeAsync(User.GetUserId()!.Value, t, id, ct);
        if (state is null) return NotFound();
        return Partial("_LikeButton", new LikeButtonModel(t, id, state.Count, state.Liked));
    }
}
