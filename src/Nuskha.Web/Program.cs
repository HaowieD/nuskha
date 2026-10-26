using System.Threading.RateLimiting;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Nuskha.Domain;
using Nuskha.Domain.Entities;
using Nuskha.Infrastructure;
using Nuskha.Infrastructure.Data;
using Nuskha.Infrastructure.Services;
using Nuskha.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---- Data & domain services ----
builder.Services.AddNuskhaInfrastructure(config);

// ---- Identity ----
builder.Services.AddIdentity<AppUser, AppRole>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";
    o.Password.RequiredLength = 8;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
    o.Lockout.MaxFailedAccessAttempts = 8;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    o.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<NuskhaDbContext>()
.AddDefaultTokenProviders();

// Role changes made in the admin panel should reach already-signed-in users quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "nuskha.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.LoginPath = "/account/login";
    o.LogoutPath = "/account/logout";
    o.AccessDeniedPath = "/account/denied";
    o.ExpireTimeSpan = TimeSpan.FromDays(30);
    o.SlidingExpiration = true;
    // htmx requests cannot follow a login redirect meaningfully: ask the browser to navigate instead.
    o.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Headers.ContainsKey("HX-Request"))
        {
            ctx.Response.Headers["HX-Redirect"] = ctx.RedirectUri;
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

var googleId = config["Authentication:Google:ClientId"];
var googleSecret = config["Authentication:Google:ClientSecret"];
var googleEnabled = !string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret);
if (googleEnabled)
{
    builder.Services.AddAuthentication().AddGoogle(o =>
    {
        o.ClientId = googleId!;
        o.ClientSecret = googleSecret!;
        o.SignInScheme = IdentityConstants.ExternalScheme;
    });
}
builder.Services.AddSingleton(new GoogleAuthState(googleEnabled));

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Staff, p => p.RequireRole(Roles.Admin, Roles.Moderator, Roles.Editor))
    .AddPolicy(Policies.ManageCatalog, p => p.RequireRole(Roles.Admin, Roles.Editor))
    .AddPolicy(Policies.Moderate, p => p.RequireRole(Roles.Admin, Roles.Moderator))
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));

// Cookies must survive container restarts, otherwise everyone is logged out on each deploy.
var keysPath = config["DataProtection:Path"];
var dp = builder.Services.AddDataProtection().SetApplicationName("Nuskha");
if (!string.IsNullOrWhiteSpace(keysPath)) dp.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    string Key(HttpContext c) => c.User.GetUserId()?.ToString() ?? c.Connection.RemoteIpAddress?.ToString() ?? "anon";
    o.AddPolicy("auth", c => RateLimitPartition.GetFixedWindowLimiter(Key(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("write", c => RateLimitPartition.GetFixedWindowLimiter(Key(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddSingleton<ILoc, JsonLoc>();
builder.Services.AddSingleton<UploadService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Admin", Policies.Staff);
    o.Conventions.AuthorizeFolder("/Me");
});

var app = builder.Build();

await DbSeeder.SeedAsync(app.Services, app.Environment.IsDevelopment());

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

// Security headers. htmx is self-hosted and its indicator styles are disabled in the layout, so no inline script/style is needed.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["X-Frame-Options"] = "DENY";
    h["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; style-src 'self' https://fonts.googleapis.com; " +
        "font-src https://fonts.gstatic.com; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'";
    await next();
});

app.UseStaticFiles();

var uploads = app.Services.GetRequiredService<UploadService>();
Directory.CreateDirectory(uploads.RootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(uploads.RootPath)),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=2592000,immutable",
});

app.UseRouting();

var supportedCultures = new[] { "ka", "en" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("ka").AddSupportedCultures(supportedCultures).AddSupportedUICultures(supportedCultures));

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/healthz", () => Results.Ok("ok"));

app.MapGet("/culture/{culture}", (string culture, string? returnUrl, HttpContext ctx) =>
{
    if (supportedCultures.Contains(culture))
    {
        ctx.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });
    }
    return Results.LocalRedirect(WebExtensions.IsLocalUrl(returnUrl) ? returnUrl! : "/");
});

app.MapGet("/sitemap.xml", async (NuskhaDbContext db, IConfiguration cfg, HttpContext ctx) =>
{
    var baseUrl = (cfg["Site:BaseUrl"] ?? $"{ctx.Request.Scheme}://{ctx.Request.Host}").TrimEnd('/');
    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    var urls = new List<(string Path, DateTime? Modified)> { ("/", null), ("/books", null), ("/authors", null), ("/forum", null), ("/users", null) };

    urls.AddRange((await db.Books.Where(b => b.Status == BookStatus.Published).OrderByDescending(b => b.UpdatedAt).Take(40000)
        .Select(b => new { b.Slug, b.UpdatedAt }).ToListAsync()).Select(b => ($"/books/{b.Slug}", (DateTime?)b.UpdatedAt)));
    urls.AddRange((await db.Authors.OrderBy(a => a.Slug).Take(5000).Select(a => a.Slug).ToListAsync()).Select(s => ($"/authors/{s}", (DateTime?)null)));
    urls.AddRange((await db.ForumCategories.Select(c => c.Slug).ToListAsync()).Select(s => ($"/forum/{s}", (DateTime?)null)));
    urls.AddRange((await db.ForumThreads.OrderByDescending(t => t.LastPostAt).Take(5000).Select(t => new { t.Slug, t.LastPostAt }).ToListAsync())
        .Select(t => ($"/forum/thread/{t.Slug}", (DateTime?)t.LastPostAt)));

    var doc = new XDocument(new XElement(ns + "urlset", urls.Select(u => new XElement(ns + "url",
        new XElement(ns + "loc", baseUrl + u.Path),
        u.Modified is DateTime m ? new XElement(ns + "lastmod", m.ToString("yyyy-MM-dd")) : null))));
    return Results.Text(doc.ToString(SaveOptions.DisableFormatting), "application/xml");
});

app.Run();

public sealed record GoogleAuthState(bool Enabled);
