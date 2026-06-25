using ScientificTrendTracker.Models.Common;

namespace ScientificTrendTracker.Middleware
{
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
                _logger.LogError(ex, "Unhandled exception tại {Path}", context.Request.Path);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var response = ApiResponse<object>.Fail(500, "Lỗi hệ thống. Vui lòng thử lại sau.");
                await context.Response.WriteAsJsonAsync(response);
            }
        }
    }
}
