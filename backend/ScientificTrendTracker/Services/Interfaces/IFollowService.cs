using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Giao diện định nghĩa các nghiệp vụ tương tác hành vi Theo dõi (Follow).
    /// </summary>
    public interface IFollowService
    {
        /// <summary>
        /// Thực hiện đảo ngược trạng thái theo dõi (Bật/Tắt) dựa trên gói dữ liệu DTO.
        /// </summary>
        /// <param name="userId">int - DB/Token - ID duy nhất của người dùng thực hiện Toggle.</param>
        /// <param name="request">ToggleFollowDto - FE - Gói dữ liệu chứa thông tin loại đối tượng và ID đối tượng cần Toggle.</param>
        /// <returns>
        /// Trả về DTO kết quả thô kèm số lượng follower; 
        /// Trả về null nếu loại mục tiêu không hợp lệ hoặc không tìm thấy ID.
        /// </returns>
        Task<FollowResultDto?> ToggleFollowAsync(
            int userId,
            ToggleFollowDto request);

        /// <summary>Lấy danh sách các mục (topic/journal) người dùng đang theo dõi.</summary>
        Task<System.Collections.Generic.List<FollowedItemDto>> GetMyFollowsAsync(int userId);
    }
}