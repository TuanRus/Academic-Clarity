using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities
{
    /// <summary>
    /// Thực thể đại diện cho bảng lịch sử giao dịch nạp tiền mua gói dịch vụ trong hệ thống.
    /// </summary>
    [Table("Transactions")]
    public class Transaction
    {
        /// <summary>
        /// Mã định danh duy nhất của giao dịch (Khóa chính tự tăng).
        /// </summary>
        [Key]
        [Column("transaction_id")]
        public int TransactionId { get; set; }

        /// <summary>
        /// Mã định danh người dùng thực hiện giao dịch (Khóa ngoại).
        /// </summary>
        [Column("user_id")]
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Đối tượng thực thể người dùng thực hiện giao dịch.
        /// </summary>
        public User? User { get; set; }

        /// <summary>
        /// Mã định danh gói cước đăng ký trong giao dịch (Khóa ngoại).
        /// </summary>
        [Column("plan_id")]
        [Required]
        public int PlanId { get; set; }

        /// <summary>
        /// Đối tượng thực thể gói cước được đăng ký.
        /// </summary>
        public SubscriptionPlan? Plan { get; set; }

        /// <summary>
        /// Mã hóa đơn duy nhất của hệ thống, đồng bộ với OrderCode gửi sang cổng PayOS.
        /// </summary>
        [Column("order_code")]
        [Required]
        public long OrderCode { get; set; }

        /// <summary>
        /// Số tiền gốc chưa giảm giá của gói cước đăng ký.
        /// </summary>
        [Column("original_amount", TypeName = "decimal(18,2)")]
        [Required]
        public decimal OriginalAmount { get; set; }

        /// <summary>
        /// Số tiền được chiết khấu ưu đãi (ví dụ: chiết khấu học giả).
        /// </summary>
        [Column("discount_amount", TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        /// <summary>
        /// Số tiền thanh toán cuối cùng của hóa đơn sau khi áp dụng chiết khấu.
        /// </summary>
        [Column("final_amount", TypeName = "decimal(18,2)")]
        [Required]
        public decimal FinalAmount { get; set; }

        /// <summary>
        /// Phương thức thanh toán (ví dụ: VietQR, VNPAY, Momo...).
        /// </summary>
        [Column("payment_method")]
        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "VietQR";

        /// <summary>
        /// Trạng thái xử lý giao dịch (PENDING, SUCCESS, FAILED, EXPIRED).
        /// </summary>
        [Column("status")]
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "PENDING";

        /// <summary>
        /// Mã tham chiếu giao dịch trả về từ cổng thanh toán đối tác (Reference code).
        /// </summary>
        [Column("gateway_order_id")]
        [MaxLength(255)]
        public string? GatewayOrderId { get; set; }

        /// <summary>
        /// Ghi chú bổ sung (dùng để lưu vết các hành động như Admin phê duyệt thủ công).
        /// </summary>
        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// Thời điểm khởi tạo yêu cầu thanh toán (Giờ UTC).
        /// </summary>
        [Column("created_at")]
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Thời điểm giao dịch được ghi nhận thanh toán thành công (Giờ UTC).
        /// </summary>
        [Column("paid_at")]
        public DateTime? PaidAt { get; set; }
    }
}
