using System;
using System.Collections.Generic;

namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// Chứa thông tin tóm tắt của người dùng phục vụ trang quản lý danh sách của Admin.
    /// </summary>
    public class AdminUserSummaryDto
    {
        /// <summary>
        /// Mã định danh của người dùng.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Địa chỉ email người dùng.
        /// </summary>
        public string Email { get; set; } = null!;

        /// <summary>
        /// Họ và tên của người dùng.
        /// </summary>
        public string Fullname { get; set; } = null!;

        /// <summary>
        /// Trường học, cơ quan hoặc tổ chức công tác.
        /// </summary>
        public string? Institution { get; set; }

        /// <summary>
        /// Mã vai trò người dùng (1-Admin, 2-Lecturer, 3-Student, 4-Regular User).
        /// </summary>
        public int RoleId { get; set; }

        /// <summary>
        /// Tên vai trò hiển thị.
        /// </summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// Đánh dấu email có đuôi học thuật hay không (ưu đãi tự động).
        /// </summary>
        public bool AccountTag { get; set; }

        /// <summary>
        /// Trạng thái hoạt động (true: Đang hoạt động, false: Bị khóa/Banned).
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Tên gói dịch vụ hiện tại đang hoạt động (Ví dụ: Free, Premium...).
        /// </summary>
        public string PlanName { get; set; } = "Free";

        /// <summary>
        /// Ngày hết hạn gói dịch vụ (nếu có).
        /// </summary>
        public DateTime? EndsAt { get; set; }

        /// <summary>
        /// Đánh dấu gói dịch vụ hiện tại đã hết hạn hay chưa.
        /// </summary>
        public bool IsPlanExpired { get; set; } = true;

        /// <summary>
        /// Số ngày còn lại của gói dịch vụ.
        /// </summary>
        public int RemainingDays { get; set; } = 0;

        /// <summary>
        /// Ngày tham gia hệ thống.
        /// </summary>
        public DateTime CreateAt { get; set; }

        /// <summary>
        /// Ngày đăng nhập hệ thống lần cuối.
        /// </summary>
        public DateTime? LastLoginAt { get; set; }
    }

    /// <summary>
    /// Bộ lọc tìm kiếm danh sách người dùng dành cho Admin.
    /// </summary>
    public class AdminUserQueryDto
    {
        /// <summary>
        /// Từ khóa tìm kiếm theo tên hoặc email của người dùng.
        /// </summary>
        public string? SearchKeyword { get; set; }

        /// <summary>
        /// Lọc theo vai trò (RoleId: 1-Admin, 2-Lecturer, 3-Student, 4-Regular User).
        /// </summary>
        public int? RoleId { get; set; }

        /// <summary>
        /// Lọc theo trạng thái hoạt động (true: Active, false: Banned).
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Lọc theo gói dịch vụ (PlanId).
        /// </summary>
        public int? PlanId { get; set; }

        /// <summary>
        /// Số thứ tự trang cần lấy (bắt đầu từ 1).
        /// </summary>
        public int PageIndex { get; set; } = 1;

        /// <summary>
        /// Số phần tử trên mỗi trang.
        /// </summary>
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// Đối tượng bọc kết quả phân trang dùng chung.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của các phần tử trong danh sách.</typeparam>
    public class PagedResultDto<T>
    {
        /// <summary>
        /// Danh sách các phần tử dữ liệu của trang hiện tại.
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Tổng số phần tử tìm thấy trong cơ sở dữ liệu.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Trang hiện tại.
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// Kích thước trang.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Tổng số trang tính toán được.
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// DTO chứa thông tin cá nhân chi tiết của người dùng phục vụ cho việc cập nhật hoặc hiển thị.
    /// </summary>
    public class UserPersonalDetailDto
    {
        /// <summary>
        /// ID người dùng.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Địa chỉ email.
        /// </summary>
        public string Email { get; set; } = null!;

        /// <summary>
        /// Họ và tên đầy đủ.
        /// </summary>
        public string Fullname { get; set; } = null!;

        /// <summary>
        /// Vai trò người dùng (RoleId).
        /// </summary>
        public int RoleId { get; set; }

        /// <summary>
        /// Tên vai trò hiển thị.
        /// </summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// Cơ quan, trường học công tác.
        /// </summary>
        public string? Institution { get; set; }

        /// <summary>
        /// Cờ ưu đãi email học thuật.
        /// </summary>
        public bool AccountTag { get; set; }

        /// <summary>
        /// Trạng thái hoạt động.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Ngày tạo tài khoản.
        /// </summary>
        public DateTime CreateAt { get; set; }

        /// <summary>
        /// Lần đăng nhập cuối.
        /// </summary>
        public DateTime? LastLoginAt { get; set; }
    }

    /// <summary>
    /// Lịch sử đăng ký và thay đổi gói cước của một người dùng.
    /// </summary>
    public class UserSubscriptionHistoryDto
    {
        /// <summary>
        /// ID bản ghi đăng ký gói.
        /// </summary>
        public int SubscriptionId { get; set; }

        /// <summary>
        /// Tên gói cước đăng ký.
        /// </summary>
        public string PlanName { get; set; } = string.Empty;

        /// <summary>
        /// Mức giá của gói cước.
        /// </summary>
        public decimal PriceAmount { get; set; }

        /// <summary>
        /// Số ngày hiệu lực của gói cước.
        /// </summary>
        public int DurationDays { get; set; }

        /// <summary>
        /// Trạng thái của gói (ACTIVE, EXPIRED, CANCELLED).
        /// </summary>
        public string Status { get; set; } = "ACTIVE";

        /// <summary>
        /// Thời điểm bắt đầu kích hoạt gói.
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Thời điểm kết thúc/hết hạn gói.
        /// </summary>
        public DateTime? EndsAt { get; set; }

        /// <summary>
        /// Thời điểm tạo bản ghi.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO tóm tắt thông tin giao dịch hiển thị trên danh sách quản trị giao dịch.
    /// </summary>
    public class TransactionSummaryDto
    {
        /// <summary>
        /// ID giao dịch trong DB.
        /// </summary>
        public int TransactionId { get; set; }

        /// <summary>
        /// ID người mua.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Tên người mua.
        /// </summary>
        public string UserFullName { get; set; } = string.Empty;

        /// <summary>
        /// Email người mua.
        /// </summary>
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// Tên gói cước đã đăng ký.
        /// </summary>
        public string PlanName { get; set; } = string.Empty;

        /// <summary>
        /// Mã đơn hàng đồng bộ với cổng thanh toán (OrderCode).
        /// </summary>
        public long OrderCode { get; set; }

        /// <summary>
        /// Số tiền gốc của gói cước.
        /// </summary>
        public decimal OriginalAmount { get; set; }

        /// <summary>
        /// Số tiền được chiết khấu.
        /// </summary>
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// Số tiền thực trả cuối cùng.
        /// </summary>
        public decimal FinalAmount { get; set; }

        /// <summary>
        /// Phương thức thanh toán (VietQR, Momo, v.v.).
        /// </summary>
        public string PaymentMethod { get; set; } = "VietQR";

        /// <summary>
        /// Trạng thái thanh toán (PENDING, SUCCESS, FAILED, EXPIRED).
        /// </summary>
        public string Status { get; set; } = "PENDING";

        /// <summary>
        /// Mã giao dịch trả về từ cổng đối tác (Reference).
        /// </summary>
        public string? GatewayOrderId { get; set; }

        /// <summary>
        /// Ghi chú từ hệ thống hoặc Admin.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Ngày tạo hóa đơn/yêu cầu thanh toán.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Ngày thanh toán thành công.
        /// </summary>
        public DateTime? PaidAt { get; set; }
    }

    /// <summary>
    /// DTO chứa bộ lọc tìm kiếm giao dịch của Admin.
    /// </summary>
    public class TransactionQueryDto
    {
        /// <summary>
        /// Tìm kiếm theo Email, Họ tên hoặc Mã đơn hàng (OrderCode).
        /// </summary>
        public string? SearchKeyword { get; set; }

        /// <summary>
        /// Lọc theo trạng thái giao dịch (PENDING, SUCCESS, FAILED, EXPIRED).
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Lọc theo thời gian từ ngày.
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Lọc theo thời gian đến ngày.
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Số trang cần lấy.
        /// </summary>
        public int PageIndex { get; set; } = 1;

        /// <summary>
        /// Kích thước trang.
        /// </summary>
        public int PageSize { get; set; } = 10;
    }
}
