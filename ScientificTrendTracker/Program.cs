using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Middleware;
using ScientificTrendTracker.BackgroundServices;
using ScientificTrendTracker.Services;
using ScientificTrendTracker.Services.Interfaces;
#pragma warning disable SKEXP0070

namespace ScientificTrendTracker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();

            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();
            }

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
            builder.Services.AddSingleton<IKeywordReprocessService, KeywordReprocessService>();
            builder.Services.AddScoped<IBookmarkService, BookmarkService>();
            builder.Services.AddScoped<ISearchHistoryService, SearchHistoryService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
            builder.Services.AddScoped<IApiSyncLogService, ApiSyncLogService>();
            builder.Services.AddScoped<IAdminActivityLogService, AdminActivityLogService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<IPaperService, PaperService>();
            builder.Services.AddScoped<IKeywordService, KeywordService>();
            
            // Background Services
            builder.Services.AddHostedService<WeeklySyncService>();

            var app = builder.Build();

            // Middleware — GlobalExceptionMiddleware phải đặt trước MapControllers
            app.UseMiddleware<GlobalExceptionMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // Phục vụ file tĩnh trong wwwroot (trang demo mind map) — cùng origin nên không lỗi CORS
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
