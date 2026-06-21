namespace ScientificTrendTracker.Models.Common
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "Success")
            => new() { Success = true, StatusCode = 200, Message = message, Data = data };

        public static ApiResponse<T> Fail(int statusCode, string message)
            => new() { Success = false, StatusCode = statusCode, Message = message, Data = default };
    }
}
