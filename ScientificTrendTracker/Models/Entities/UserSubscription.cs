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

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}




