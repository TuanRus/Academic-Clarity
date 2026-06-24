using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs.Subscription;

namespace ScientificTrendTracker.Services.Interfaces;

/// <summary>
/// Giao diện định nghĩa các nghiệp vụ liên quan đến đăng ký gói dịch vụ và kiểm tra thời hạn Premium.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Kiểm tra trạng thái Premium và lấy thông tin chi tiết gói đăng ký hiện tại của người dùng.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Tham số truyền vào từ Controller (ID người dùng).</param>
    /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp để hủy tác vụ.</param>
    /// <returns>
    /// Trả về đối tượng SubscriptionStatusResponseDto chứa trạng thái hoạt động và thời hạn gói.
    /// </returns>
    Task<SubscriptionStatusResponseDto> GetSubscriptionStatusAsync(int userId, CancellationToken ct);

    /// <summary>
    /// Đăng ký hoặc gia hạn gói dịch vụ mới cho người dùng.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Tham số truyền vào từ Controller (ID người dùng).</param>
    /// <param name="dto">SubscribeRequestDto - NGUỒN: Thông tin gói đăng ký từ FE.</param>
    /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp để hủy tác vụ.</param>
    /// <returns>
    /// Trả về true nếu đăng ký thành công, false nếu gói không khả dụng hoặc bị khóa.
    /// </returns>
    Task<bool> SubscribePlanAsync(int userId, SubscribeRequestDto dto, CancellationToken ct);

    /// <summary>
    /// Lấy danh sách các gói dịch vụ đang hoạt động trong hệ thống để hiển thị bảng giá.
    /// </summary>
    /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp để hủy tác vụ.</param>
    /// <returns>
    /// Trả về danh sách các gói dịch vụ đang khả dụng (SubscriptionPlanDto).
    /// </returns>
    Task<List<SubscriptionPlanDto>> GetActivePlansAsync(CancellationToken ct);

    /// <summary>
    /// Admin lấy toàn bộ danh sách gói cước trong hệ thống (kể cả gói đã khóa) để quản trị.
    /// </summary>
    /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp.</param>
    /// <returns>Danh sách đầy đủ cấu hình các gói cước AdminSubscriptionPlanDto.</returns>
    Task<List<AdminSubscriptionPlanDto>> GetAllPlansForAdminAsync(CancellationToken ct);

    /// <summary>
    /// Admin tạo mới một gói cước dịch vụ trong hệ thống.
    /// </summary>
    /// <param name="dto">CreateSubscriptionPlanDto - NGUỒN: Dữ liệu nhập từ Admin gửi qua Body.</param>
    /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
    /// <returns>Trả về True nếu tạo thành công, False nếu trùng tên gói.</returns>
    Task<bool> CreatePlanAsync(CreateSubscriptionPlanDto dto, CancellationToken ct);

    /// <summary>
    /// Admin cập nhật cấu hình thông tin của một gói cước đang có.
    /// </summary>
    /// <param name="planId">Số nguyên Int - NGUỒN: Route Parameter - ID gói cước.</param>
    /// <param name="dto">UpdateSubscriptionPlanDto - NGUỒN: Dữ liệu sửa đổi gửi qua Body.</param>
    /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
    /// <returns>Trả về True nếu cập nhật thành công, False nếu gói không tồn tại hoặc trùng tên với gói khác.</returns>
    Task<bool> UpdatePlanAsync(int planId, UpdateSubscriptionPlanDto dto, CancellationToken ct);

    /// <summary>
    /// Admin bật/tắt (khóa/mở khóa) trạng thái hoạt động của một gói cước dịch vụ.
    /// </summary>
    /// <param name="planId">Số nguyên Int - NGUỒN: Route Parameter - ID gói cước.</param>
    /// <param name="isActive">Boolean - Trạng thái hoạt động muốn chuyển (true = mở, false = khóa).</param>
    /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
    /// <returns>Trả về True nếu thực hiện thành công, False nếu gói cước không tồn tại.</returns>
    Task<bool> TogglePlanStatusAsync(int planId, bool isActive, CancellationToken ct);
}
