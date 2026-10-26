namespace Nuskha.Web.Pages.Admin.Users;

/// <summary>User management. Moderators can lock/unlock ordinary accounts; only admins can change roles.</summary>
[Authorize(Policy = Policies.Moderate)]
public class IndexModel(NuskhaDbContext db, UserManager<AppUser> users) : AppPageModel
{
    public const int PageSize = 50;

    public class Row
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool Locked { get; set; }
        public List<string> RoleNames { get; set; } = [];
    }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public List<Row> Rows { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public static IEnumerable<string> StaffRoles => Roles.All.Where(r => r != Roles.User);

    public async Task OnGetAsync(CancellationToken ct)
    {
        PageNo = Math.Max(1, PageNo);
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var like = "%" + WebExtensions.EscapeLike(Q.Trim()) + "%";
            query = query.Where(u => EF.Functions.ILike(u.UserName!, like) || EF.Functions.ILike(u.Email!, like) || EF.Functions.ILike(u.DisplayName, like));
        }

        Total = await query.CountAsync(ct);
        Rows = await query.OrderByDescending(u => u.CreatedAt).Skip((PageNo - 1) * PageSize).Take(PageSize)
            .Select(u => new Row
            {
                Id = u.Id,
                UserName = u.UserName!,
                Email = u.Email ?? "",
                DisplayName = u.DisplayName,
                CreatedAt = u.CreatedAt,
                Locked = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
            }).ToListAsync(ct);

        var ids = Rows.Select(r => r.Id).ToList();
        var roleRows = await (from ur in db.UserRoles
                              join r in db.Roles on ur.RoleId equals r.Id
                              where ids.Contains(ur.UserId)
                              select new { ur.UserId, r.Name }).ToListAsync(ct);
        foreach (var row in Rows)
            row.RoleNames = roleRows.Where(x => x.UserId == row.Id).Select(x => x.Name!).ToList();
    }

    public async Task<IActionResult> OnPostRolesAsync(Guid id, string[]? roles, string? q)
    {
        if (!User.IsInRole(Roles.Admin)) return Forbid();
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var wanted = (roles ?? []).Where(r => StaffRoles.Contains(r)).ToHashSet();
        var current = await users.GetRolesAsync(user);

        // The site must always keep at least one administrator.
        if (current.Contains(Roles.Admin) && !wanted.Contains(Roles.Admin)
            && (await users.GetUsersInRoleAsync(Roles.Admin)).Count <= 1)
            return Flash("admin.error.lastadmin", q);

        var remove = current.Where(r => r != Roles.User && !wanted.Contains(r)).ToList();
        var add = wanted.Where(r => !current.Contains(r)).ToList();
        if (remove.Count > 0) await users.RemoveFromRolesAsync(user, remove);
        if (add.Count > 0) await users.AddToRolesAsync(user, add);
        // Make the new roles apply to the user's open sessions on the next security-stamp check.
        await users.UpdateSecurityStampAsync(user);
        return Flash("admin.saved", q);
    }

    public async Task<IActionResult> OnPostLockAsync(Guid id, string? q)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (user.Id == CurrentUserId) return Flash("admin.error.self", q);

        var targetRoles = await users.GetRolesAsync(user);
        if ((targetRoles.Contains(Roles.Admin) || targetRoles.Contains(Roles.Moderator)) && !User.IsInRole(Roles.Admin))
            return Forbid();

        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await users.UpdateSecurityStampAsync(user);
        return Flash("admin.saved", q);
    }

    public async Task<IActionResult> OnPostUnlockAsync(Guid id, string? q)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return Flash("admin.saved", q);
    }

    private IActionResult Flash(string key, string? q)
    {
        TempData["Flash"] = key;
        return RedirectToPage(new { q });
    }
}
