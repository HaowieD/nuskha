namespace Nuskha.Web.Pages.Social;

/// <summary>Status posts: the only thing people write on a profile now. Replies and likes happen on the post itself.</summary>
[Authorize]
[EnableRateLimiting("write")]
public class StatusModel(FeedService feed) : PageModel
{
    /// <param name="to">User name of the profile owner when writing on someone else's profile; empty for your own.</param>
    public async Task<IActionResult> OnPostAsync(string? body, string? to, CancellationToken ct)
    {
        var me = User.GetUserId()!.Value;
        var id = await feed.PostStatusAsync(me, body, to, ct);
        if (id is null) return BadRequest();
        if (!Request.Headers.ContainsKey("HX-Request")) return LocalRedirect("/");

        var item = await feed.GetOneAsync(id.Value, me, ct);
        return item is null ? NotFound() : Partial("_ActivityItem", item);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await feed.DeleteStatusAsync(id, User.GetUserId()!.Value, User.CanModerate(), ct);
        // An empty 200 makes htmx replace the post's element with nothing.
        return deleted ? Content("", "text/html") : NotFound();
    }
}
