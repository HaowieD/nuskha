namespace Nuskha.Web.Pages.Social;

[EnableRateLimiting("write")]
public class CommentsModel(SocialService social, CommentQueryService queries) : AppPageModel
{
    public Task<IActionResult> OnGetAsync(int type, Guid id, CancellationToken ct) => ThreadAsync(type, id, ct);

    public async Task<IActionResult> OnPostAddAsync(int type, Guid id, string? body, Guid? parentId, CancellationToken ct)
    {
        if (CurrentUserId is not Guid me) return Challenge();
        if (!Enum.IsDefined((TargetType)type)) return BadRequest();
        await social.AddCommentAsync(me, (TargetType)type, id, body, parentId, ct);
        return await ThreadAsync(type, id, ct);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int type, Guid id, Guid commentId, CancellationToken ct)
    {
        if (CurrentUserId is not Guid me) return Challenge();
        await social.DeleteCommentAsync(commentId, me, User.CanModerate(), ct);
        return await ThreadAsync(type, id, ct);
    }

    private async Task<IActionResult> ThreadAsync(int type, Guid id, CancellationToken ct)
    {
        if (!Enum.IsDefined((TargetType)type)) return BadRequest();
        var t = (TargetType)type;
        if (await social.ResolveTargetAsync(t, id, ct) is null) return NotFound();
        var model = new CommentThreadModel
        {
            Type = t,
            TargetId = id,
            ViewerId = CurrentUserId,
            IsModerator = User.CanModerate(),
            Comments = await queries.GetAsync(t, id, CurrentUserId, 200, ct),
        };
        return Partial("_CommentThread", model);
    }
}
