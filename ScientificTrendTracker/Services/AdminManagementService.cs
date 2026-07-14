using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Lớp thực thi các dịch vụ nghiệp vụ quản trị người dùng, gói cước và giao dịch tài chính của Admin.
    /// </summary>
    public class AdminManagementService : IAdminManagementService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminManagementService> _logger;

        /// <summary>
        /// Khởi tạo dịch vụ quản lý Admin bằng cách tiêm DbContext và Logger.
        /// </summary>
        /// <param name="context">AppDbContext - DI kết nối DB MySQL.</param>
        /// <param name="logger">ILogger - DI phục vụ ghi log hệ thống.</param>
        public AdminManagementService(AppDbContext context, ILogger<AdminManagementService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách người dùng phân trang kết hợp tìm kiếm và bộ lọc.
        /// </summary>
        public async Task<PagedResultDto<AdminUserSummaryDto>> GetUsersPagedAsync(AdminUserQueryDto query)
        {
            var queryable = _context.Users.AsNoTracking().AsQueryable();

            // 1. Áp dụng bộ lọc tìm kiếm theo tên hoặc email
            if (!string.IsNullOrWhiteSpace(query.SearchKeyword))
            {
                var kw = query.SearchKeyword.Trim().ToLower();
                queryable = queryable.Where(u => u.Email.ToLower().Contains(kw) || u.Fullname.ToLower().Contains(kw));
            }

            // 2. Lọc theo vai trò (RoleId)
            if (query.RoleId.HasValue)
            {
                queryable = queryable.Where(u => u.RoleId == query.RoleId.Value);
            }

            // 3. Lọc theo trạng thái hoạt động (IsActive)
            if (query.IsActive.HasValue)
            {
                queryable = queryable.Where(u => u.IsActive == query.IsActive.Value);
            }

            // 4. Lọc theo gói cước (PlanId)
            if (query.PlanId.HasValue)
            {
                if (query.PlanId.Value == 0) // Lọc người dùng sử dụng gói miễn phí (không có gói ACTIVE)
                {
                    queryable = queryable.Where(u => !_context.UserSubscriptions
                        .Any(s => s.UserId == u.UserId && s.Status == "ACTIVE" && (s.EndsAt == null || s.EndsAt > DateTime.UtcNow)));
                }
                else
                {
                    queryable = queryable.Where(u => _context.UserSubscriptions
                        .Any(s => s.UserId == u.UserId && s.Status == "ACTIVE" && s.PlanId == query.PlanId.Value && (s.EndsAt == null || s.EndsAt > DateTime.UtcNow)));
                }
            }

            // Lấy tổng số lượng bản ghi khớp điều kiện
            int totalCount = await queryable.CountAsync();

            // Phân trang và lấy thông tin chi tiết gói đăng ký hiện tại (nếu có) trực tiếp dưới DB
            var usersData = await queryable
                .OrderByDescending(u => u.CreateAt)
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(u => new
                {
                    User = u,
                    ActiveSub = _context.UserSubscriptions
                        .Where(s => s.UserId == u.UserId && s.Status == "ACTIVE")
                        .OrderByDescending(s => s.StartedAt)
                        .Select(s => new
                        {
                            s.PlanId,
                            PlanName = s.Plan != null ? s.Plan.PlanName : "Free",
                            s.EndsAt
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var items = usersData.Select(x =>
            {
                var u = x.User;
                var sub = x.ActiveSub;

                string roleName = u.RoleId switch
                {
                    1 => "Admin",
                    2 => "Lecturer",
                    3 => "Student",
                    4 => "Regular User",
                    _ => "Regular User"
                };

                DateTime? endsAt = sub?.EndsAt;
                string planName = sub?.PlanName ?? "Free";
                bool isPlanExpired = true;
                int remainingDays = 0;

                if (endsAt.HasValue)
                {
                    isPlanExpired = endsAt.Value < DateTime.UtcNow;
                    if (!isPlanExpired)
                    {
                        remainingDays = (endsAt.Value - DateTime.UtcNow).Days;
                    }
                }

                return new AdminUserSummaryDto
                {
                    UserId = u.UserId,
                    Email = u.Email,
                    Fullname = u.Fullname,
                    Institution = u.Institution,
                    RoleId = u.RoleId,
                    RoleName = roleName,
                    AccountTag = u.AccountTag,
                    IsActive = u.IsActive,
                    PlanName = planName,
                    EndsAt = endsAt,
                    IsPlanExpired = isPlanExpired,
                    RemainingDays = remainingDays,
                    CreateAt = u.CreateAt,
                    LastLoginAt = u.LastLoginAt
                };
            }).ToList();

            return new PagedResultDto<AdminUserSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = query.PageIndex,
                PageSize = query.PageSize
            };
        }

        /// <summary>
        /// Lấy chi tiết thông tin cá nhân của một người dùng.
        /// </summary>
        public async Task<UserPersonalDetailDto?> GetUserPersonalDetailAsync(int userId)
        {
            var u = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
            if (u == null) return null;

            string roleName = u.RoleId switch
            {
                1 => "Admin",
                2 => "Lecturer",
                3 => "Student",
                4 => "Regular User",
                _ => "Regular User"
            };

            return new UserPersonalDetailDto
            {
                UserId = u.UserId,
                Email = u.Email,
                Fullname = u.Fullname,
                RoleId = u.RoleId,
                RoleName = roleName,
                Institution = u.Institution,
                AccountTag = u.AccountTag,
                IsActive = u.IsActive,
                CreateAt = u.CreateAt,
                LastLoginAt = u.LastLoginAt
            };
        }

        /// <summary>
        /// Lấy lịch sử các gói đăng ký dịch vụ của User.
        /// </summary>
        public async Task<List<UserSubscriptionHistoryDto>> GetUserSubscriptionHistoryAsync(int userId)
        {
            return await _context.UserSubscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new UserSubscriptionHistoryDto
                {
                    SubscriptionId = s.SubscriptionId,
                    PlanName = s.Plan != null ? s.Plan.PlanName : "Unknown",
                    PriceAmount = s.Plan != null ? s.Plan.PriceAmount : 0,
                    DurationDays = s.Plan != null ? s.Plan.DurationDays : 0,
                    Status = s.Status,
                    StartedAt = s.StartedAt,
                    EndsAt = s.EndsAt,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();
        }

        /// <summary>
        /// Lấy lịch sử các giao dịch nạp tiền của User.
        /// </summary>
        public async Task<List<TransactionSummaryDto>> GetUserTransactionHistoryAsync(int userId)
        {
            return await _context.Transactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TransactionSummaryDto
                {
                    TransactionId = t.TransactionId,
                    UserId = t.UserId,
                    UserFullName = t.User != null ? t.User.Fullname : string.Empty,
                    UserEmail = t.User != null ? t.User.Email : string.Empty,
                    PlanName = t.Plan != null ? t.Plan.PlanName : string.Empty,
                    OrderCode = t.OrderCode,
                    OriginalAmount = t.OriginalAmount,
                    DiscountAmount = t.DiscountAmount,
                    FinalAmount = t.FinalAmount,
                    PaymentMethod = t.PaymentMethod,
                    Status = t.Status,
                    GatewayOrderId = t.GatewayOrderId,
                    Notes = t.Notes,
                    CreatedAt = t.CreatedAt,
                    PaidAt = t.PaidAt
                })
                .ToListAsync();
        }

        /// <summary>
        /// Bật/Tắt trạng thái hoạt động (Khóa/Mở khóa) của tài khoản User.
        /// </summary>
        public async Task<bool> ToggleUserActiveStatusAsync(int adminId, string adminEmail, int userId, string ipAddress)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng có ID {UserId} để cập nhật trạng thái hoạt động.", userId);
                return false;
            }

            // Chặn tuyệt đối hành động khóa tài khoản Admin khác
            if (user.RoleId == 1)
            {
                _logger.LogWarning("Admin ID {AdminId} cố gắng khóa tài khoản Admin ID {TargetId}. Hành động bị từ chối.", adminId, userId);
                return false;
            }

            // Đảo ngược trạng thái hoạt động
            user.IsActive = !user.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            string actionName = user.IsActive ? "UNBAN_USER" : "BAN_USER";
            string description = user.IsActive 
                ? $"Mo khoa tai khoan cho User ID {userId} ({user.Email})"
                : $"Khoa tai khoan cua User ID {userId} ({user.Email})";

            var log = new AdminActivityLog
            {
                AdminId = adminId,
                AdminEmail = adminEmail,
                Action = actionName,
                Description = description,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };

            await _context.AdminActivityLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminEmail} đã thực hiện {Action} thành công trên User ID {TargetId}.", adminEmail, actionName, userId);
            return true;
        }

        /// <summary>
        /// Thay đổi gói cước dịch vụ thủ công cho người dùng.
        /// </summary>
        public async Task<bool> ChangeUserPlanManualAsync(int adminId, string adminEmail, int userId, int planId, string ipAddress)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null || user.RoleId == 1)
            {
                _logger.LogWarning("Không tìm thấy người dùng hoặc tài khoản là Admin.");
                return false;
            }

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive);
            if (plan == null)
            {
                _logger.LogWarning("Không tìm thấy gói cước ID {PlanId} hoặc gói cước đã bị khóa.", planId);
                return false;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Hủy kích hoạt tất cả các gói cước ACTIVE cũ của người dùng
                var currentActiveSubs = await _context.UserSubscriptions
                    .Where(s => s.UserId == userId && s.Status == "ACTIVE")
                    .ToListAsync();

                foreach (var sub in currentActiveSubs)
                {
                    sub.Status = "EXPIRED";
                    sub.UpdatedAt = DateTime.UtcNow;
                }

                // 2. Tạo một gói đăng ký mới cho người dùng
                var newSub = new UserSubscription
                {
                    UserId = userId,
                    PlanId = planId,
                    Status = "ACTIVE",
                    StartedAt = DateTime.UtcNow,
                    EndsAt = DateTime.UtcNow.AddDays(plan.DurationDays),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.UserSubscriptions.AddAsync(newSub);

                // 3. Nâng cấp vai trò của người dùng lên Researcher (RoleId = 2) nếu không phải Lecturer/Student
                if (user.RoleId == 4) // Regular User
                {
                    user.RoleId = 2; // Nâng cấp lên Premium (Researcher)
                }
                user.UpdatedAt = DateTime.UtcNow;

                // 4. Ghi log hoạt động của Admin
                var log = new AdminActivityLog
                {
                    AdminId = adminId,
                    AdminEmail = adminEmail,
                    Action = "CHANGE_PLAN",
                    Description = $"Doi goi cuoc thu cong cho User ID {userId} ({user.Email}) sang goi {plan.PlanName} (Thoi han: {plan.DurationDays} ngay)",
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.AdminActivityLogs.AddAsync(log);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Admin {AdminEmail} đổi gói cước thủ công thành công cho User ID {UserId} sang gói {PlanName}.", adminEmail, userId, plan.PlanName);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi xảy ra khi đổi gói cước thủ công cho User ID {UserId}.", userId);
                return false;
            }
        }

        /// <summary>
        /// Lấy tất cả các gói cước đang hoạt động trên hệ thống.
        /// </summary>
        public async Task<List<SubscriptionPlan>> GetActiveSubscriptionPlansAsync()
        {
            return await _context.SubscriptionPlans
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.PriceAmount)
                .ToListAsync();
        }

        /// <summary>
        /// Liệt kê danh sách toàn bộ các giao dịch nạp tiền trên hệ thống (kèm phân trang, lọc).
        /// </summary>
        public async Task<PagedResultDto<TransactionSummaryDto>> GetTransactionsPagedAsync(TransactionQueryDto query)
        {
            var queryable = _context.Transactions.AsNoTracking().AsQueryable();

            // 1. Tìm kiếm theo Email, Họ tên hoặc Mã đơn hàng (OrderCode)
            if (!string.IsNullOrWhiteSpace(query.SearchKeyword))
            {
                var kw = query.SearchKeyword.Trim().ToLower();
                if (long.TryParse(kw, out long code))
                {
                    queryable = queryable.Where(t => t.OrderCode == code || t.User!.Email.ToLower().Contains(kw) || t.User!.Fullname.ToLower().Contains(kw));
                }
                else
                {
                    queryable = queryable.Where(t => t.User!.Email.ToLower().Contains(kw) || t.User!.Fullname.ToLower().Contains(kw));
                }
            }

            // 2. Lọc theo trạng thái giao dịch
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                queryable = queryable.Where(t => t.Status == query.Status.Trim().ToUpper());
            }

            // 3. Lọc theo thời gian từ ngày
            if (query.FromDate.HasValue)
            {
                queryable = queryable.Where(t => t.CreatedAt >= query.FromDate.Value);
            }

            // 4. Lọc theo thời gian đến ngày
            if (query.ToDate.HasValue)
            {
                queryable = queryable.Where(t => t.CreatedAt <= query.ToDate.Value);
            }

            int totalCount = await queryable.CountAsync();

            var items = await queryable
                .OrderByDescending(t => t.CreatedAt)
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(t => new TransactionSummaryDto
                {
                    TransactionId = t.TransactionId,
                    UserId = t.UserId,
                    UserFullName = t.User != null ? t.User.Fullname : string.Empty,
                    UserEmail = t.User != null ? t.User.Email : string.Empty,
                    PlanName = t.Plan != null ? t.Plan.PlanName : string.Empty,
                    OrderCode = t.OrderCode,
                    OriginalAmount = t.OriginalAmount,
                    DiscountAmount = t.DiscountAmount,
                    FinalAmount = t.FinalAmount,
                    PaymentMethod = t.PaymentMethod,
                    Status = t.Status,
                    GatewayOrderId = t.GatewayOrderId,
                    Notes = t.Notes,
                    CreatedAt = t.CreatedAt,
                    PaidAt = t.PaidAt
                })
                .ToListAsync();

            return new PagedResultDto<TransactionSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = query.PageIndex,
                PageSize = query.PageSize
            };
        }

        /// <summary>
        /// Phê duyệt giao dịch thủ công khi Webhook lỗi, kích hoạt gói cước tương ứng.
        /// </summary>
        public async Task<bool> ApproveTransactionManualAsync(int adminId, string adminEmail, int transactionId, string ipAddress)
        {
            var transactionRecord = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId);
            if (transactionRecord == null)
            {
                _logger.LogWarning("Không tìm thấy giao dịch ID {TransactionId} để phê duyệt thủ công.", transactionId);
                return false;
            }

            // Tránh duyệt trùng lặp nếu giao dịch đã xử lý thành công trước đó
            if (transactionRecord.Status == "SUCCESS")
            {
                _logger.LogWarning("Giao dịch ID {TransactionId} đã thành công từ trước. Không cần duyệt lại.", transactionId);
                return true;
            }

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == transactionRecord.PlanId && p.IsActive);
            if (plan == null)
            {
                _logger.LogError("Lỗi đối soát: Không tìm thấy gói cước ID {PlanId} được liên kết với giao dịch.", transactionRecord.PlanId);
                return false;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == transactionRecord.UserId);
            if (user == null)
            {
                _logger.LogError("Lỗi đối soát: Không tìm thấy người dùng ID {UserId} liên kết với giao dịch.", transactionRecord.UserId);
                return false;
            }

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Cập nhật trạng thái giao dịch trong DB
                transactionRecord.Status = "SUCCESS";
                transactionRecord.PaidAt = DateTime.UtcNow;
                transactionRecord.Notes = $"Duyet thanh toan thu cong boi Admin {adminEmail} vao luc {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss} UTC.";

                // 2. Vô hiệu hóa các gói cước ACTIVE cũ
                var currentActiveSubs = await _context.UserSubscriptions
                    .Where(s => s.UserId == transactionRecord.UserId && s.Status == "ACTIVE")
                    .ToListAsync();

                foreach (var sub in currentActiveSubs)
                {
                    sub.Status = "EXPIRED";
                    sub.UpdatedAt = DateTime.UtcNow;
                }

                // 3. Kích hoạt gói cước mới cho user
                var newSub = new UserSubscription
                {
                    UserId = transactionRecord.UserId,
                    PlanId = transactionRecord.PlanId,
                    Status = "ACTIVE",
                    StartedAt = DateTime.UtcNow,
                    EndsAt = DateTime.UtcNow.AddDays(plan.DurationDays),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.UserSubscriptions.AddAsync(newSub);

                // 4. Thăng cấp Role thành Researcher (RoleId = 2) nếu không phải Admin/Lecturer
                if (user.RoleId == 4)
                {
                    user.RoleId = 2; // Thăng cấp lên Premium
                }
                user.UpdatedAt = DateTime.UtcNow;

                // 5. Ghi nhận log thao tác của Admin
                var log = new AdminActivityLog
                {
                    AdminId = adminId,
                    AdminEmail = adminEmail,
                    Action = "MANUAL_APPROVE_PAYMENT",
                    Description = $"Duyet giao dich thu cong cho giao dich ID {transactionId} (Ma don: {transactionRecord.OrderCode}) cho User ID {transactionRecord.UserId} ({user.Email}). So tien: {transactionRecord.FinalAmount}đ.",
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.AdminActivityLogs.AddAsync(log);
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("Admin {AdminEmail} duyệt thủ công thành công giao dịch ID {TransactionId} cho User ID {UserId}.", adminEmail, transactionId, transactionRecord.UserId);
                return true;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi xảy ra khi duyệt giao dịch thủ công ID {TransactionId}.", transactionId);
                return false;
            }
        }
    }
}
