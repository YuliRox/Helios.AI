using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LumiRise.Api.Data;

public sealed class LumiRiseDbContextFactory : IDesignTimeDbContextFactory<LumiRiseDbContext>
{
    public LumiRiseDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=lumi_rise;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<LumiRiseDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new LumiRiseDbContext(optionsBuilder.Options);
    }
}
