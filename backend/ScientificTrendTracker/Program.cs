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
            builder.Services.AddSingleton<IKeywordReprocessService, KeywordReprocessService>();
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