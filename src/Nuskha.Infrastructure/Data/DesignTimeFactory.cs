using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nuskha.Infrastructure.Data;

/// <summary>Lets `dotnet ef` work without starting the web app.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<NuskhaDbContext>
{
    public NuskhaDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("NUSKHA_CONNECTION")
                 ?? "Host=localhost;Port=5432;Database=nuskha;Username=nuskha;Password=nuskha";
        var options = new DbContextOptionsBuilder<NuskhaDbContext>().UseNpgsql(cs).Options;
        return new NuskhaDbContext(options);
    }
}
