using System.Collections.Generic;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Models.Entities;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Giao diện định nghĩa các nghiệp vụ quản trị người dùng, gói dịch vụ và giao dịch tài chính dành cho Admin.
    /// </summary>
    public interface IAdminManagementService
    {
        /// <summary>
        /// Truy vấn danh sách người dùng phân trang kết hợp tìm kiếm và lọc.
        /// </summary>
        /// <param name="query">AdminUserQueryDto - DTO lọc đầu vào chứa từ khóa, vai trò, trạng thái, gói cước và phân trang.</param>
        /// <returns>Đối tượng PagedResultDto chứa danh sách người dùng tóm tắt và tổng số lượng tìm thấy.</returns>
        Task<PagedResultDto<AdminUserSummaryDto>> GetUsersPagedAsync(AdminUserQueryDto query);

        /// <summary>
        /// Lấy chi tiết thông tin cá nhân của một người dùng cụ thể.
        /// </summary>
        /// <param name="userId">int - Mã định danh người dùng cần lấy chi tiết.</param>
        /// <returns>DTO chi tiết thông tin cá nhân của User, hoặc null nếu không tìm thấy.</returns>
        Task<UserPersonalDetailDto?> GetUserPersonalDetailAsync(int userId);

        /// <summary>
        /// Lấy lịch sử đăng ký các gói cước dịch vụ của một người dùng cụ thể.
        /// </summary>
        /// <param name="userId">int - Mã định danh người dùng cần lấy lịch sử gói.</param>
        /// <returns>Danh sách DTO lịch sử gói cước của người dùng.</returns>
        Task<List<UserSubscriptionHistoryDto>> GetUserSubscriptionHistoryAsync(int userId);

        /// <summary>
        /// Lấy lịch sử các giao dịch nạp tiền của một người dùng cụ thể.
        /// </summary>
        /// <param name="userId">int - Mã định danh người dùng cần lấy lịch sử giao dịch.</param>
        /// <returns>Danh sách DTO tóm tắt giao dịch của người dùng.</returns>
        Task<List<TransactionSummaryDto>> GetUserTransactionHistoryAsync(int userId);

        /// <summary>
        /// Bật/Tắt trạng thái hoạt động của tài khoản người dùng (Khóa/Mở khóa tài khoản).
        /// </summary>
        /// <param name="adminId">int - ID của Admin thực hiện hành động.</param>
        /// <param name="adminEmail">string - Email của Admin thực hiện hành động để lưu log.</param>
        /// <param name="userId">int - ID của người dùng bị tác động.</param>
        /// <param name="ipAddress">string - Địa chỉ IP máy của Admin thực hiện.</param>
        /// <returns>True nếu thay đổi trạng thái thành công, False nếu tài khoản không tồn tại hoặc là Admin.</returns>
        Task<bool> ToggleUserActiveStatusAsync(int adminId, string adminEmail, int userId, string ipAddress);

        /// <summary>
        /// Thay đổi gói cước thủ công cho người dùng, kích hoạt gói cước tương ứng và cập nhật quyền hạn.
        /// </summary>
        /// <param name="adminId">int - ID của Admin thực hiện hành động.</param>
        /// <param name="adminEmail">string - Email của Admin thực hiện hành động.</param>
        /// <param name="userId">int - ID của người dùng cần đổi gói cước.</param>
        /// <param name="planId">int - ID của gói cước mới muốn kích hoạt.</param>
        /// <param name="ipAddress">string - IP máy Admin thực hiện.</param>
        /// <returns>True nếu thay đổi thành công, ngược lại là False.</returns>
        Task<bool> ChangeUserPlanManualAsync(int adminId, string adminEmail, int userId, int planId, string ipAddress);

        /// <summary>
        /// Lấy danh sách toàn bộ các gói cước đang hoạt động trong hệ thống.
        /// </summary>
        /// <returns>Danh sách thực thể SubscriptionPlan đang hoạt động.</returns>
        Task<List<SubscriptionPlan>> GetActiveSubscriptionPlansAsync();

        /// <summary>
        /// Truy vấn danh sách toàn bộ các giao dịch nạp tiền trong hệ thống kết hợp phân trang, tìm kiếm và lọc.
        /// </summary>
        /// <param name="query">TransactionQueryDto - DTO lọc chứa từ khóa, trạng thái, khoảng thời gian và phân trang.</param>
        /// <returns>Đối tượng PagedResultDto chứa danh sách tóm tắt giao dịch.</returns>
        Task<PagedResultDto<TransactionSummaryDto>> GetTransactionsPagedAsync(TransactionQueryDto query);

        /// <summary>
        /// Duyệt thủ công một giao dịch nạp tiền (khi webhook bị lỗi), kích hoạt gói cước tương ứng và nâng cấp vai trò.
        /// </summary>
        /// <param name="adminId">int - ID của Admin thực hiện duyệt.</param>
        /// <param name="adminEmail">string - Email của Admin thực hiện duyệt.</param>
        /// <param name="transactionId">int - ID giao dịch cần duyệt thủ công.</param>
        /// <param name="ipAddress">string - IP máy Admin thực hiện.</param>
        /// <returns>True nếu phê duyệt thành công, ngược lại là False.</returns>
        Task<bool> ApproveTransactionManualAsync(int adminId, string adminEmail, int transactionId, string ipAddress);
    }
}
