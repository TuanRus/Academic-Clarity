using System.Collections.Generic;

namespace JournalTrend.Core.DTOs
{
    /// <summary>
    /// Lớp vỏ bọc chuẩn hóa mọi kết quả API trả ra cho Frontend theo cấu trúc PascalCase cố định.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của gói Payload nằm bên trong thuộc tính Data.</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>Cờ trạng thái thông báo phản hồi thành công (true) hoặc thất bại (false).</summary>
        public bool Success { get; set; }

        /// <summary>Mã định danh trạng thái HTTP Status Code tương ứng.</summary>
        public int StatusCode { get; set; }

        /// <summary>Lời nhắn thông báo bằng tiếng Việt mô tả kết quả xử lý nghiệp vụ.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Hộp chứa gói dữ liệu thô trả về cho Frontend khai thác.</summary>
        public T? Data { get; set; }

        /// <summary>Danh sách chi tiết các lỗi kiểm định dữ liệu đầu vào nếu có.</summary>
        public List<string>? Errors { get; set; }

        /// <summary>
        /// Hàm nhà máy static sản sinh nhanh hộp phản hồi thành công nguyên khuôn.
        /// </summary>
        public static ApiResponse<T> Ok(T? data, string message, int statusCode = 200)
            => new() { Success = true, StatusCode = statusCode, Message = message, Data = data, Errors = null };

        /// <summary>
        /// Hàm nhà máy static sản sinh hộp phản hồi thất bại kèm mã định danh lỗi.
        /// </summary>
        public static ApiResponse<T> Fail(int statusCode, string message, List<string>? errors = null)
            => new() { Success = false, StatusCode = statusCode, Message = message, Data = default, Errors = errors };
    }
}