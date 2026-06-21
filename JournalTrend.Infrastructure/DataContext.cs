using JournalTrend.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace JournalTrend.Infrastructure
{
    /// <summary>Trạm điều phối trung tâm kết nối thực thể C# dội xuống MySQL Server qua Tailscale.</summary>
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<FollowedItem> FollowedItems { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
        }
    }
}