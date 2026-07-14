using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Middleware;
using ScientificTrendTracker.BackgroundServices;
using ScientificTrendTracker.Services;
using ScientificTrendTracker.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using PayOS;

#pragma warning disable SKEXP0070

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
                options.UseMySql(connectionString,
                    new MySqlServerVersion(new Version(8, 0, 46)),
                    // Bật retry tự động khi kết nối qua mạng chập chờn (transient failure)
                    mySqlOptions => mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,                         // Tự động thử lại tối đa 5 lần
                        maxRetryDelay: TimeSpan.FromSeconds(5),   // Mỗi lần thử lại cách nhau 5 giây
                        errorNumbersToAdd: null
                    )));

            builder.Services.AddScoped<IOpenAlexService, OpenAlexService>();
            builder.Services.AddScoped<IKeywordExtractionService, KeywordExtractionService>();
            builder.Services.AddScoped<IScimagoImportService, ScimagoImportService>();
            builder.Services.AddScoped<IGraphBuilderService, GraphBuilderService>();
            builder.Services.AddScoped<ITrendService, TrendService>();
            builder.Services.AddScoped<ISyncOrchestratorService, SyncOrchestratorService>();
            builder.Services.AddScoped<IIdeaOverlapService, IdeaOverlapService>();
            // Idea Check dùng Gemini (2 API key luân phiên + failover), fallback Ollama — có HttpClient riêng.
            builder.Services.AddHttpClient<IIdeaKeywordExtractor, GeminiIdeaKeywordExtractor>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60); // trần cứng; timeout mỗi key cấu hình qua Gemini:TimeoutSeconds
            });
            builder.Services.AddSingleton<IKeywordReprocessService, KeywordReprocessService>();
            builder.Services.AddSingleton<ISyncProgressTracker, SyncProgressTracker>();
            builder.Services.AddSingleton<ITopicBackfillService, TopicBackfillService>();
            builder.Services.AddScoped<IBookmarkService, BookmarkService>();
            builder.Services.AddScoped<ISearchHistoryService, SearchHistoryService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
            builder.Services.AddScoped<IApiSyncLogService, ApiSyncLogService>();
            builder.Services.AddScoped<IAdminActivityLogService, AdminActivityLogService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<IPaperService, PaperService>();
            builder.Services.AddScoped<IKeywordService, KeywordService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IFollowService, FollowService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            
            // Đăng ký PayOSClient làm Singleton service để sử dụng cổng thanh toán PayOS
            var payOSSettings = builder.Configuration.GetSection("PayOS");
            PayOSClient payOS = new PayOSClient(
                payOSSettings["ClientId"] ?? throw new ArgumentNullException("PayOS:ClientId"),
                payOSSettings["ApiKey"] ?? throw new ArgumentNullException("PayOS:ApiKey"),
                payOSSettings["ChecksumKey"] ?? throw new ArgumentNullException("PayOS:ChecksumKey")
            );
            builder.Services.AddSingleton(payOS);
            
            builder.Services.AddMemoryCache();
            builder.Services.AddSignalR();

            // Cấu hình HttpClient tích hợp sẵn Timeout và Headers cho OpenAlex Service
            builder.Services.AddHttpClient<IOpenAlexService, OpenAlexService>(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ScientificTrendTracker/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(options =>
                {
                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "Bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description = "Nhập theo cú pháp: Bearer [Chuỗi_Token_Của_Bạn]"
                    });

                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                            },
                            Array.Empty<string>()
                        }
                    });
                });
            }

            var secretKey = builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("SecretKey is missing!");
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub/notifications"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
            builder.Services.AddHostedService<WeeklySyncService>();

            var app = builder.Build();

            // Dọn nhật ký sync "mồ côi": khi backend KHỞI ĐỘNG, mọi log còn "running" chắc chắn đã chết
            // (tiến trình sync không thể sống sót qua lần restart) → đánh dấu "failed" để không kẹt GENERATING.
            using (var startupScope = app.Services.CreateScope())
            {
                try
                {
                    var db = startupScope.ServiceProvider.GetRequiredService<ScientificTrendTracker.Data.AppDbContext>();
                    var stale = db.ApiSyncLogs.Where(l => l.Status == "running").ToList();
                    foreach (var l in stale)
                    {
                        l.Status = "failed";
                        l.SyncFinishedAt = DateTime.UtcNow;
                        l.ErrorMessage = "Tiến trình sync bị gián đoạn (backend khởi động lại giữa chừng).";
                    }
                    if (stale.Count > 0) db.SaveChanges();
                }
                catch { /* không chặn khởi động nếu DB tạm lỗi */ }

                // Bảo đảm cột author_id tồn tại (DB không auto-migrate). Idempotent: chỉ ALTER khi thiếu cột.
                try
                {
                    var db = startupScope.ServiceProvider.GetRequiredService<ScientificTrendTracker.Data.AppDbContext>();
                    var exists = db.Database
                        .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'FollowedItems' AND COLUMN_NAME = 'author_id'")
                        .AsEnumerable().FirstOrDefault();
                    if (exists == 0)
                        db.Database.ExecuteSqlRaw("ALTER TABLE FollowedItems ADD COLUMN author_id INT NULL");
                }
                catch { /* không chặn khởi động */ }

                // Bảo đảm cột PaidAmount (số tiền thực trả sau ưu đãi edu) tồn tại. Idempotent.
                try
                {
                    var db = startupScope.ServiceProvider.GetRequiredService<ScientificTrendTracker.Data.AppDbContext>();
                    var exists = db.Database
                        .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UserSubscriptions' AND COLUMN_NAME = 'PaidAmount'")
                        .AsEnumerable().FirstOrDefault();
                    if (exists == 0)
                        db.Database.ExecuteSqlRaw("ALTER TABLE UserSubscriptions ADD COLUMN PaidAmount DECIMAL(18,2) NULL");
                }
                catch { /* không chặn khởi động */ }
            }

            // ====================================================================
            // PIPELINE MIDDLEWARE & ROUTING CONFIGURATION
            // ====================================================================
            // Middleware chặn/xử lý lỗi toàn cục đặt đầu pipeline
            app.UseMiddleware<GlobalExceptionMiddleware>();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            // CORS phải đặt trước UseAuthorization và MapControllers
            app.UseCors(CorsPolicy);

            // Phục vụ file tĩnh trong wwwroot (không lỗi CORS vì cùng Origin)
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}