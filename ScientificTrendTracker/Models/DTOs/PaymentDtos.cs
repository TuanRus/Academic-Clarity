namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// Gói dữ liệu yêu cầu khởi tạo liên kết thanh toán từ Frontend lên hệ thống.
    /// </summary>
    public record CreatePaymentLinkRequestDto
    {
        /// <summary>
        /// Mã định danh duy nhất của gói dịch vụ người dùng lựa chọn đăng ký.
        /// </summary>
        public int PlanId { get; init; }
    }

    /// <summary>
    /// Gói dữ liệu phản hồi chứa thông tin đường dẫn thanh toán dội về cho Frontend điều hướng.
    /// </summary>
    public record PaymentLinkResponseDto
    {
        /// <summary>
        /// Đường dẫn URL dẫn trực tiếp sang giao diện thanh toán hóa đơn của PayOS.
        /// </summary>
        public string PaymentUrl { get; init; } = string.Empty;

        /// <summary>
        /// Chuỗi mã hóa nội dung hoặc QR Code phục vụ hiển thị nếu Frontend cần xử lý ad-hoc.
        /// </summary>
        public string QrCode { get; init; } = string.Empty;

        /// <summary>
        /// Số tiền thực tế sau khi đã áp dụng các chính sách ưu đãi, giảm giá hệ thống.
        /// </summary>
        public decimal FinalAmount { get; init; }
    }

    /// <summary>
    /// Gói dữ liệu cấu trúc trần tiếp nhận thông tin phản hồi từ Webhook của đối tác PayOS.
    /// </summary>
    public record PayOSWebhookDto
    {
        /// <summary>
        /// Mã phản hồi trạng thái từ cổng thanh toán đối tác.
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// Lời nhắn hoặc thông báo trạng thái giao dịch đi kèm.
        /// </summary>
        public string Desc { get; init; } = string.Empty;

        /// <summary>
        /// Đối tượng chứa chi tiết lõi của giao dịch chuyển khoản từ khách hàng.
        /// </summary>
        public PayOSTransactionDataDto Data { get; init; } = null!;

        /// <summary>
        /// Chuỗi mã hóa chữ ký số (Signature) dùng để xác thực tính toàn vẹn, chống giả mạo gói tin.
        /// </summary>
        public string Signature { get; init; } = string.Empty;
    }

    /// <summary>
    /// Chi tiết lõi dữ liệu giao dịch chuyển khoản nằm bên trong gói tin Webhook.
    /// </summary>
    public record PayOSTransactionDataDto
    {
        /// <summary>
        /// Mã định danh đơn hàng duy nhất do hệ thống Backend tự sinh ra lúc tạo link.
        /// </summary>
        public long OrderCode { get; init; }

        /// <summary>
        /// Số tiền thực tế người dùng đã chuyển khoản thành công qua ngân hàng.
        /// </summary>
        public decimal Amount { get; init; }

        /// <summary>
        /// Nội dung chuyển khoản ghi nhận từ hệ thống ngân hàng (Chứa thông tin đối chiếu).
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Mã định danh giao dịch duy nhất của phía đối tác PayOS.
        /// </summary>
        public string Reference { get; init; } = string.Empty;
    }
}
