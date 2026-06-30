using System;

namespace ScientificTrendTracker.Models.DTOs.Subscription;

/// <summary>
/// DTO gửi từ Frontend lên để đăng ký một gói dịch vụ mới.
/// </summary>
public class SubscribeRequestDto
{
    /// <summary>
    /// Số nguyên Int - NGUỒN: FE truyền qua Body JSON - ID của gói dịch vụ muốn đăng ký.
    /// </summary>
    public int PlanId { get; set; }
}

/// <summary>
/// DTO chứa thông tin phản hồi về trạng thái Premium của người dùng.
/// </summary>
public class SubscriptionStatusResponseDto
{
    /// <summary>
    /// Boolean - NGUỒN: DB tính toán - Xác định tài khoản có đang trong thời gian Premium hoạt động hay không.
    /// </summary>
    public bool IsPremiumActive { get; set; }

    /// <summary>
    /// Số nguyên Int (Nullable) - NGUỒN: DB truy vấn - ID gói đăng ký hiện tại (null nếu chưa đăng ký).
    /// </summary>
    public int? PlanId { get; set; }

    /// <summary>
    /// Chuỗi String - NGUỒN: DB truy vấn - Tên gói dịch vụ hiện tại (mặc định "Free" nếu chưa đăng ký).
    /// </summary>
    public string PlanName { get; set; } = "Free";

    /// <summary>
    /// Chuỗi String - NGUỒN: DB tính toán - Trạng thái gói dịch vụ ("ACTIVE", "EXPIRED", "INACTIVE").
    /// </summary>
    public string Status { get; set; } = "INACTIVE";

    /// <summary>
    /// DateTime (Nullable) - NGUỒN: DB truy vấn - Thời gian bắt đầu kích hoạt gói.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// DateTime (Nullable) - NGUỒN: DB truy vấn - Thời điểm hết hạn gói dịch vụ.
    /// </summary>
    public DateTime? EndsAt { get; set; }
}

/// <summary>
/// DTO thông tin gói dịch vụ hiển thị trên bảng giá cho người dùng lựa chọn.
/// </summary>
public class SubscriptionPlanDto
{
    /// <summary>
    /// Số nguyên Int - NGUỒN: DB truy vấn - ID của gói dịch vụ.
    /// </summary>
    public int PlanId { get; set; }

    /// <summary>
    /// Chuỗi String - NGUỒN: DB truy vấn - Tên của gói dịch vụ.
    /// </summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Số thập phân Decimal - NGUỒN: DB truy vấn - Giá tiền của gói dịch vụ.
    /// </summary>
    public decimal PriceAmount { get; set; }

    /// <summary>
    /// Số nguyên Int - NGUỒN: DB truy vấn - Số ngày hiệu lực của gói dịch vụ.
    /// </summary>
    public int DurationDays { get; set; }
}

/// <summary>
/// DTO chứa thông tin để Admin tạo mới một gói dịch vụ.
/// </summary>
public class CreateSubscriptionPlanDto
{
    /// <summary>
    /// Chuỗi String - NGUỒN: FE truyền lên qua Body - Tên của gói cước mới.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Số thập phân Decimal - NGUỒN: FE truyền lên qua Body - Giá tiền của gói cước mới.
    /// </summary>
    public decimal PriceAmount { get; set; }

    /// <summary>
    /// Số nguyên Int - NGUỒN: FE truyền lên qua Body - Số ngày hiệu lực của gói cước mới.
    /// </summary>
    public int DurationDays { get; set; }
}

/// <summary>
/// DTO chứa thông tin để Admin cập nhật một gói dịch vụ đang có.
/// </summary>
public class UpdateSubscriptionPlanDto
{
    /// <summary>
    /// Chuỗi String - NGUỒN: FE truyền lên qua Body - Tên của gói cước cần cập nhật.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Số thập phân Decimal - NGUỒN: FE truyền lên qua Body - Giá tiền của gói cước.
    /// </summary>
    public decimal PriceAmount { get; set; }

    /// <summary>
    /// Số nguyên Int - NGUỒN: FE truyền lên qua Body - Số ngày hiệu lực của gói cước.
    /// </summary>
    public int DurationDays { get; set; }

    /// <summary>
    /// Boolean - NGUỒN: FE truyền lên qua Body - Trạng thái hoạt động của gói cước (true = hoạt động, false = khóa).
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO một dòng lịch sử giao dịch (mỗi bản ghi UserSubscription = 1 lần mua gói).
/// </summary>
public class TransactionRowDto
{
    public int SubscriptionId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO chứa đầy đủ cấu hình gói dịch vụ phục vụ cho công tác quản trị của Admin.
/// </summary>
public class AdminSubscriptionPlanDto
{
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal PriceAmount { get; set; }
    public int DurationDays { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
