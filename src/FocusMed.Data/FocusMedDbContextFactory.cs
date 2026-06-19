using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FocusMed.Data;

public class FocusMedDbContextFactory : IDesignTimeDbContextFactory<FocusMedDbContext>
{
    public FocusMedDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FocusMedDbContext>();
        optionsBuilder.UseSqlite("Data Source=db/focusmed.db");
        return new FocusMedDbContext(optionsBuilder.Options);
    }
}
