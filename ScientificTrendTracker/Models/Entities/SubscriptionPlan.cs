using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities;

/// <summary>
/// Định nghĩa các gói dịch vụ phân quyền có trong hệ thống (Free, Monthly, Yearly).
/// </summary>
[Table("SubscriptionPlans")]
public class SubscriptionPlan
{
    [Key]
    public int PlanId { get; set; }

    [Required]
    [MaxLength(100)]
    public string PlanName { get; set; } = string.Empty;

    public decimal PriceAmount { get; set; }
    public int DurationDays { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}




