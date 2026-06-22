using JournalTrend.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace JournalTrend.Infrastructure
{
    /// <summary>
    /// Trạm điều phối trung tâm kết nối thực thể C# dội xuống MySQL Server qua Tailscale.
    /// </summary>
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        /// <summary>
        /// Tập hợp dữ liệu quản lý thông tin tài khoản người dùng.
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Tập hợp dữ liệu quản lý các vết băm mã Token bảo mật của phiên đăng nhập.
        /// </summary>
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

        /// <summary>
        /// Tập hợp dữ liệu quản lý phân quyền hệ thống.
        /// </summary>
        public DbSet<Role> Roles { get; set; }

        /// <summary>
        /// Tập hợp dữ liệu quản lý lịch sử theo dõi (Follow) chuyên mục và tạp chí của người dùng.
        /// </summary>
        public DbSet<FollowedItem> FollowedItems { get; set; }

        /// <summary>
        /// Tập hợp dữ liệu quản lý hộp chuông thông báo cá nhân hóa.
        /// </summary>
        public DbSet<Notification> Notifications { get; set; }

        /// <summary>
        /// Tập hợp dữ liệu quản lý thông tin các chủ đề nghiên cứu khoa học.
        /// </summary>
        public DbSet<ResearchTopic> ResearchTopics { get; set; }

        /// <summary>
        /// Tập hợp dữ liệu quản lý thông tin các tạp chí khoa học.
        /// </summary>
        public DbSet<Journal> Journals { get; set; }

        /// <summary>
        /// Thực hiện cấu hình các ràng buộc, chỉ mục và mối quan hệ giữa các thực thể hệ thống thông qua Fluent API.
        /// </summary>
        /// <param name="modelBuilder">Bộ dựng mô hình thực thể của EF Core.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================================================================
            // CẤU HÌNH CŨ CỦA HỆ THỐNG - GIỮ NGUYÊN VẸN BẢO TOÀN LOGIC ĐÃ NGHIỆM THU
            // =========================================================================

            // ÉP LUẬT CHẶN XÓA LAN ĐỂ TRÁNH MẤT DẤU VẾT LOG HỆ THỐNG THEO MỤC 7 QUY TẮC
            modelBuilder.Entity<UserRefreshToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // THIẾT LẬP CÁC CHỈ MỤC INDEX ĐỂ RÚT NGẮN THỜI GIAN TRUY VẤN XUYÊN VPN TAILSCALE
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<UserRefreshToken>()
                .HasIndex(t => t.JwtId)
                .IsUnique();

            modelBuilder.Entity<FollowedItem>()
                .HasIndex(f => new { f.TargetType, f.JournalId });

            modelBuilder.Entity<FollowedItem>()
                .HasIndex(f => new { f.TargetType, f.TopicId });


            // =========================================================================
            // CẤU HÌNH BỔ SUNG MỚI - THIẾT LẬP MỐI QUAN HỆ KHÓA NGOẠI TƯỜNG MINH
            // =========================================================================
            modelBuilder.Entity<FollowedItem>(entity =>
            {
                // 1. Cấu hình mối quan hệ 1-N với bảng ResearchTopics thông qua khóa ngoại TopicId
                entity.HasOne(f => f.ResearchTopic)
                      .WithMany()
                      .HasForeignKey(f => f.TopicId)
                      .OnDelete(DeleteBehavior.Restrict); // Chốt chặn bảo vệ: Cấm xóa lan làm mất dữ liệu lịch sử hệ thống

                // 2. Cấu hình mối quan hệ 1-N với bảng Journals thông qua khóa ngoại JournalId
                entity.HasOne(f => f.Journal)
                      .WithMany()
                      .HasForeignKey(f => f.JournalId)
                      .OnDelete(DeleteBehavior.Restrict); // Chốt chặn bảo vệ: Chặn xóa dây chuyền ngoài ý muốn
            });
        }
    }
}