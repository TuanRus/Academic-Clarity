namespace JournalTrend.Core.DTOs
{
    /// <summary>
    /// Gói dữ liệu yêu cầu đảo trạng thái theo dõi 
    /// cho một chủ đề nghiên cứu hoặc tạp chí khoa học.
    /// </summary>
    public record ToggleFollowDto
    {
        /// <summary>
        /// Loại đối tượng cần tương tác: "topic" hoặc "journal".
        /// </summary>
        public string TargetType { get; init; } = null!;

        /// <summary>
        /// Mã định danh duy nhất của Topic hoặc Journal tương ứng.
        /// </summary>
        public string TargetId { get; init; } = null!;
    }

    /// <summary>
    /// Gói dữ liệu phản hồi kết quả sau khi đảo trạng thái theo dõi.
    /// </summary>
    public record FollowResultDto
    {
        /// <summary>
        /// Trạng thái sau xử lý: true nếu đang theo dõi, false nếu đã hủy.
        /// </summary>
        public bool IsFollowing { get; init; }

        /// <summary>
        /// Tổng số lượng học giả đang theo dõi đối tượng này tại thời điểm hiện tại.
        /// </summary>
        public int TotalFollowers { get; init; }
    }
}