using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScientificTrendTracker.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            // Design-time connection string — dùng cho migrations, không cần NAS online
            var connectionString = "Server=localhost;Port=3306;Database=ScientificTrendTracker;Uid=root;Pwd=placeholder;";
            optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
