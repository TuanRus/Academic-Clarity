using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities;

/// <summary>
/// Theo dõi trạng thái gói đăng ký dịch vụ của người dùng.
/// </summary>
[Table("UserSubscriptions")]
public class UserSubscription
{
    [Key]
    public int SubscriptionId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int PlanId { get; set; }
    public SubscriptionPlan? Plan { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>Số tiền người dùng THỰC TRẢ cho đơn này (đã áp ưu đãi edu 50% nếu có).
    /// Null với dữ liệu cũ → khi đọc doanh thu sẽ fallback về Plan.PriceAmount.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? PaidAmount { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Mã đơn hàng duy nhất tương ứng từ PayOS để đối chiếu trạng thái giao dịch.
    /// </summary>
    public long? OrderCode { get; set; }
}




