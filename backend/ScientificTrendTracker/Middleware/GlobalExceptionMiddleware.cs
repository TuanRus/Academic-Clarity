using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.BackgroundServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;

namespace ScientificTrendTracker.Middleware
{
    /// <summary>
    /// Middleware xử lý và bắt ngoại lệ toàn cục cho ứng dụng.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        /// <summary>
        /// Khởi tạo middleware xử lý lỗi toàn cục.
        /// </summary>
        /// <param name="next">RequestDelegate - DI - Middleware tiếp theo trong pipeline.</param>
        /// <param name="logger">ILogger - System - Ghi log lỗi.</param>
        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Thực thi lọc và bắt các lỗi phát sinh trong HTTP pipeline.
        /// </summary>
        /// <param name="context">HttpContext - System - Ngữ cảnh HTTP request.</param>
        /// <returns>Đối tượng Task biểu diễn tiến trình bất đồng bộ.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (BreachDetectedException ex)
            {
                _logger.LogWarning(ex, "Security breach detected at {Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var response = ApiResponse<object>.Fail(401, ex.Message);
                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception tại {Path}", context.Request.Path);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var response = ApiResponse<object>.Fail(500, "Internal server error. Please try again later.");
                await context.Response.WriteAsJsonAsync(response);
            }
        }
    }
}
