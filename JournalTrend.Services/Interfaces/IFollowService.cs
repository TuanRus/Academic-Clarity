using System.Threading.Tasks;
using JournalTrend.Core.DTOs;

namespace JournalTrend.Services.Interfaces
{
    /// <summary>
    /// Giao diện định nghĩa các nghiệp vụ tương tác hành vi Theo dõi (Follow).
    /// </summary>
    public interface IFollowService
    {
        /// <summary>
        /// Thực hiện đảo ngược trạng thái theo dõi (Bật/Tắt) dựa trên gói dữ liệu DTO[cite: 54].
                    /// </summary>
                    /// <returns>
                    /// Trả về DTO kết quả thô kèm số lượng follower; 
        /// Trả về null nếu loại mục tiêu không hợp lệ hoặc không tìm thấy ID[cite: 107, 237].
                    /// </returns>
        Task<FollowResultDto?> ToggleFollowAsync(
            int userId,
            ToggleFollowDto request);
    }
}