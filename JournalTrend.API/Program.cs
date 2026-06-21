using JournalTrend.Infrastructure;
using JournalTrend.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace JournalTrend.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // =========================================================
            // GROUP DI BLOCK 1: DATABASE ENGINE (MYSQL VIA TAILSCALE)
            // =========================================================
            var mySqlConnectionStr = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<DataContext>(options =>
                options.UseMySql(mySqlConnectionStr,
                    new MySqlServerVersion(new Version(8, 0, 46)),
                    mySqlOptions => mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,                         // Tự động thử lại tối đa 5 lần
                        maxRetryDelay: TimeSpan.FromSeconds(5),   // Mỗi lần thử lại cách nhau 5 giây
                        errorNumbersToAdd: null
                    )));

            // =========================================================
            // GROUP DI BLOCK 2: BUSINESS SERVICES INTERFACES
            // =========================================================
            builder.Services.AddScoped<JournalTrend.Services.Interfaces.IAuthService, JournalTrend.Services.Implementations.AuthService>();
            builder.Services.AddScoped<JournalTrend.Services.Interfaces.INotificationService, JournalTrend.Services.Implementations.NotificationService>();

            // =========================================================
            // GROUP DI BLOCK 3: CACHE & REAL-TIME PLUGINS
            // =========================================================
            builder.Services.AddMemoryCache();
            builder.Services.AddSignalR();
            builder.Services.AddControllers();

            // =========================================================
            // GROUP DI BLOCK 4: SWAGGER DOCUMENTATION CONFIGURATION
            // =========================================================
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

            // =========================================================
            // GROUP DI BLOCK 5: SECURITY CORES & JWT MATRIX RULES
            // =========================================================
            var secretKey = builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("SecretKey is missing!");
            // =========================================================
            // SỬA ĐOẠN ĐĂNG KÝ ADDJWTBEARER TRONG PROGRAM.CS
            // =========================================================
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

                // BỒI THÊM ĐOẠN SỰ KIỆN NÀY ĐỂ SIGNALR ĐỌC ĐƯỢC TOKEN QUA ĐƯỜNG URL
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.Request.Path;

                        // Chốt chặn TẠI SAO: Nếu request đang gõ cửa đường ống Hub SignalR, 
                        // tự động bốc chuỗi token từ URL nạp vào ngữ cảnh để hệ thống giải mã xác thực danh tính.
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub/notifications"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            var app = builder.Build();

            // ====================================================================
            // ĐẶT MIDDLEWARE CHẶN LỖI LÊN ĐẦU PIPELINE THEO ĐÚNG MỤC 8 QUY TẮC MỚI
            // ====================================================================
            app.UseMiddleware<GlobalExceptionMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHub<JournalTrend.API.Hubs.NotificationHub>("/hub/notifications");

            app.Run();
        }
    }
}