using ScientificTrendTracker.Models.DTOs;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Idea Overlap Checker (premium): trích keyword từ abstract dán vào → so với corpus →
    /// trả về bài trùng nhiều keyword nhất + mức CẢNH BÁO SỚM. Abstract xử lý in-memory, KHÔNG lưu DB.
    /// </summary>
    public interface IIdeaOverlapService
    {
        /// <param name="abstractText">Đoạn abstract người dùng dán (in-memory).</param>
        /// <param name="topN">Số bài trùng tối đa trả về.</param>
        /// <param name="ct">CancellationToken để áp timeout cho lời gọi AI (Gemini).</param>
        Task<OverlapResultDto> CheckOverlapAsync(string abstractText, int topN = 10, CancellationToken ct = default);
    }
}
