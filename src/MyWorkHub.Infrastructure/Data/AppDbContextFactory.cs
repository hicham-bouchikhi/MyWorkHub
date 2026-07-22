using MyWorkHub.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyWorkHub.Infrastructure.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the context
/// without launching the app.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={AppPaths.DbPath}")
            .Options;

        return new AppDbContext(options);
    }
}
