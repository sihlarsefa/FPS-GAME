using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Harekat.Infrastructure.Persistence;

/// <summary>dotnet ef migrations için tasarım zamanı fabrikası (MSSQL varsayılan).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HarekatDbContext>
{
    public HarekatDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("HAREKAT_SQL_CONNECTION")
                 ?? "Server=localhost;Database=Harekat;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<HarekatDbContext>()
            .UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(HarekatDbContext).Assembly.FullName))
            .Options;

        return new HarekatDbContext(options);
    }
}
