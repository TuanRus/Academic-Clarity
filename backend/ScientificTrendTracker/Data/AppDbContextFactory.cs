using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ScientificTrendTracker.Data
{
    /// <summary>
    /// Design-time factory cho EF Core CLI (migrations add / database update).
    /// Đọc ConnectionStrings:DefaultConnection từ appsettings(.Development).json / biến môi trường,
    /// nên `dotnet ef database update` chạy thẳng không cần truyền --connection.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Không tìm thấy ConnectionStrings:DefaultConnection. Chạy lệnh ef trong thư mục project " +
                    "và đảm bảo appsettings(.Development).json tồn tại.");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
