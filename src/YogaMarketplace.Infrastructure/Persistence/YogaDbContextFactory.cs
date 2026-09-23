using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YogaMarketplace.Infrastructure.Persistence;

public class YogaDbContextFactory : IDesignTimeDbContextFactory<YogaDbContext>
{
    public const string DesignTimeConnectionString =
        "Server=localhost,1433;Database=YogaMarketplace;User Id=sa;Password=YogaDev!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true";

    public YogaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<YogaDbContext>()
            .UseSqlServer(DesignTimeConnectionString)
            .Options;
        return new YogaDbContext(options);
    }
}
