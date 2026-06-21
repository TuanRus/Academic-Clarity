using JournalTrend.Core.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace JournalTrend.API.Middleware
{
    /// <summary>
    /// Chốt chặn tối cao đặt tại đầu đường ống dẫn để bảo vệ Server, nuốt trọn mọi exception sập nguồn và ép về định dạng JSON chung.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "M Middleware bắt được lỗi nghiêm trọng hệ thống: {Message}", ex.Message);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                var failureEnvelope = ApiResponse<object>.Fail(500, "Hệ thống trục trặc máy chủ nghiêm trọng. Vui lòng thử lại sau hoặc liên hệ nhóm DEV!");

                var jsonSerializationOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var jsonResult = JsonSerializer.Serialize(failureEnvelope, jsonSerializationOptions);

                await context.Response.WriteAsync(jsonResult);
            }
        }
    }
}