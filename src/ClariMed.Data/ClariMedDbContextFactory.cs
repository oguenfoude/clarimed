using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClariMed.Data;

public class ClariMedDbContextFactory : IDesignTimeDbContextFactory<ClariMedDbContext>
{
    public ClariMedDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClariMedDbContext>();
        optionsBuilder.UseSqlite("Data Source=db/clarimed.db");
        return new ClariMedDbContext(optionsBuilder.Options);
    }
}
