using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nuskha.Infrastructure.Data;
using Nuskha.Infrastructure.Import;
using Nuskha.Infrastructure.Services;

namespace Nuskha.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNuskhaInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        services.AddDbContext<NuskhaDbContext>(o => o.UseNpgsql(cs));

        services.AddScoped<SlugService>();
        services.AddScoped<ListService>();
        services.AddScoped<SocialService>();
        services.AddScoped<FeedService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<CommentQueryService>();

        services.Configure<ImportOptions>(config.GetSection(ImportOptions.Section));
        services.AddHttpClient("import").ConfigureHttpClient((sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(sp.GetRequiredService<IOptions<ImportOptions>>().Value.UserAgent);
        });
        services.AddScoped<OpenLibrarySource>();
        services.AddScoped<GoogleBooksSource>();
        services.AddScoped<BookSourceRegistry>();
        services.AddScoped<ImportService>();
        services.AddSingleton<ImportQueue>();
        services.AddHostedService<ImportWorker>();
        return services;
    }
}
