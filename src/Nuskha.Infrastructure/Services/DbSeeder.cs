using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nuskha.Domain;
using Nuskha.Domain.Entities;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, bool isDevelopment)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<NuskhaDbContext>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");
        var config = sp.GetRequiredService<IConfiguration>();

        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
        else if (isDevelopment)
        {
            log.LogWarning("No EF migrations found; using EnsureCreated (development only). Run: dotnet ef migrations add Initial");
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            throw new InvalidOperationException("No EF migrations found. Generate them first: dotnet ef migrations add Initial -p src/Nuskha.Infrastructure");
        }

        // Import runs that were in flight when the process stopped can never finish.
        await db.ImportRuns.Where(r => r.FinishedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.FinishedAt, DateTime.UtcNow).SetProperty(r => r.Error, "Interrupted by restart"));

        var roles = sp.GetRequiredService<RoleManager<AppRole>>();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new AppRole(role));

        if (!await db.ForumCategories.AnyAsync())
        {
            db.ForumCategories.AddRange(
                new ForumCategory { Slug = "general", Name = "ზოგადი განხილვა", NameEn = "General", SortOrder = 1,
                    Description = "ყველაფერი წიგნებზე და კითხვაზე", DescriptionEn = "Anything about books and reading" },
                new ForumCategory { Slug = "recommendations", Name = "რეკომენდაციები", NameEn = "Recommendations", SortOrder = 2,
                    Description = "რა წავიკითხო შემდეგ?", DescriptionEn = "What should I read next?" },
                new ForumCategory { Slug = "new-books", Name = "ახალი წიგნები და გამოცემები", NameEn = "New books & releases", SortOrder = 3,
                    Description = "სიახლეები გამომცემლობებიდან", DescriptionEn = "News from publishers" },
                new ForumCategory { Slug = "site-feedback", Name = "საიტის შესახებ", NameEn = "Site feedback", SortOrder = 4,
                    Description = "შეცდომები, იდეები, წიგნის დამატების მოთხოვნები", DescriptionEn = "Bugs, ideas and book-add requests" });
            await db.SaveChangesAsync();
        }

        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            var users = sp.GetRequiredService<UserManager<AppUser>>();
            var admin = await users.FindByEmailAsync(email);
            if (admin is null)
            {
                admin = new AppUser
                {
                    UserName = config["Seed:AdminUserName"] ?? "admin",
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = "Admin",
                };
                var created = await users.CreateAsync(admin, password);
                if (!created.Succeeded)
                {
                    log.LogError("Could not create seed admin: {Errors}", string.Join("; ", created.Errors.Select(e => e.Description)));
                    return;
                }
            }
            if (!await users.IsInRoleAsync(admin, Roles.Admin))
                await users.AddToRoleAsync(admin, Roles.Admin);
        }
    }
}
