using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Middleware;
using ScientificTrendTracker.BackgroundServices;
using ScientificTrendTracker.Services;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();

            // Swagger bật cho mọi môi trường để cả profile local lẫn remote đều có UI test API.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                // Nạp mô tả từ XML doc comment (summary -> note hiển thị trên Swagger).
                var xml = Path.Combine(AppContext.BaseDirectory,
                    $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
                if (File.Exists(xml)) c.IncludeXmlComments(xml, includeControllerXmlComments: true);
            });

            // CORS — cho phép FE (React/Vite) gọi API. Origin lấy từ config "Cors:AllowedOrigins"
            // (mảng string trong appsettings); mặc định cổng dev của Vite nếu chưa cấu hình.
            const string CorsPolicy = "FrontendCors";
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:5173", "http://localhost:3000" };
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicy, policy =>
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            // Database
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)),
                    // DB ở NAS qua mạng → bật retry tự động khi kết nối chập chờn (transient failure)
                    mySqlOptions => mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null)));

            // HttpClient
            builder.Services.AddHttpClient<IOpenAlexService, OpenAlexService>(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ScientificTrendTracker/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Services
            builder.Services.AddScoped<IOpenAlexService, OpenAlexService>();
            builder.Services.AddScoped<IKeywordExtractionService, KeywordExtractionService>();
            builder.Services.AddScoped<IScimagoImportService, ScimagoImportService>();
            builder.Services.AddScoped<IGraphBuilderService, GraphBuilderService>();
            builder.Services.AddScoped<ITrendService, TrendService>();
            builder.Services.AddScoped<ISyncOrchestratorService, SyncOrchestratorService>();
            builder.Services.AddScoped<IIdeaOverlapService, IdeaOverlapService>();
            builder.Services.AddSingleton<IKeywordReprocessService, KeywordReprocessService>();
            builder.Services.AddScoped<IBookmarkService, BookmarkService>();
            builder.Services.AddScoped<ISearchHistoryService, SearchHistoryService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
            builder.Services.AddScoped<IApiSyncLogService, ApiSyncLogService>();
            builder.Services.AddScoped<IAdminActivityLogService, AdminActivityLogService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            
            // Background Services
            builder.Services.AddHostedService<WeeklySyncService>();

            var app = builder.Build();

            // Middleware — GlobalExceptionMiddleware phải đặt trước MapControllers
            app.UseMiddleware<GlobalExceptionMiddleware>();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            // CORS phải đặt trước UseAuthorization và MapControllers
            app.UseCors(CorsPolicy);

            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
