using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Models.Entities;

namespace ScientificTrendTracker.Data
{
    /// <summary>
    /// Bối cảnh cơ sở dữ liệu Entity Framework Core (DbContext) kết nối và ánh xạ tới cơ sở dữ liệu MySQL của dự án.
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// Khởi tạo bối cảnh cơ sở dữ liệu với các cấu hình kết nối.
        /// </summary>
        /// <param name="options">DbContextOptions&lt;AppDbContext&gt; - DI - Các tham số cấu hình kết nối database.</param>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ResearchPaper> ResearchPapers { get; set; }
        public DbSet<Journal> Journals { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Keyword> Keywords { get; set; }
        public DbSet<PaperAuthor> PaperAuthors { get; set; }
        public DbSet<PaperKeyword> PaperKeywords { get; set; }
        public DbSet<PaperCitation> PaperCitations { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }
        public DbSet<SearchHistory> SearchHistories { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<ApiDataSource> ApiDataSources { get; set; }
        public DbSet<ApiSyncLog> ApiSyncLogs { get; set; }
        public DbSet<AdminActivityLog> AdminActivityLogs { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<FollowedItem> FollowedItems { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ResearchTopic> ResearchTopics { get; set; }
        public DbSet<PaperTopic> PaperTopics { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PaperAuthor>()
                .HasKey(pa => new { pa.PaperId, pa.AuthorId });

            modelBuilder.Entity<PaperAuthor>()
                .HasOne(pa => pa.Paper)
                .WithMany(p => p.PaperAuthors)
                .HasForeignKey(pa => pa.PaperId);

            modelBuilder.Entity<PaperAuthor>()
                .HasOne(pa => pa.Author)
                .WithMany(a => a.PaperAuthors)
                .HasForeignKey(pa => pa.AuthorId);

            modelBuilder.Entity<PaperKeyword>()
                .HasKey(pk => new { pk.PaperId, pk.KeywordId });

            modelBuilder.Entity<PaperKeyword>()
                .HasOne(pk => pk.Paper)
                .WithMany(p => p.PaperKeywords)
                .HasForeignKey(pk => pk.PaperId);

            modelBuilder.Entity<PaperKeyword>()
                .HasOne(pk => pk.Keyword)
                .WithMany(k => k.PaperKeywords)
                .HasForeignKey(pk => pk.KeywordId);

            modelBuilder.Entity<PaperTopic>()
                .HasKey(pt => new { pt.PaperId, pt.TopicId });

            modelBuilder.Entity<PaperTopic>()
                .HasOne(pt => pt.Paper)
                .WithMany()
                .HasForeignKey(pt => pt.PaperId);

            modelBuilder.Entity<PaperTopic>()
                .HasOne(pt => pt.Topic)
                .WithMany()
                .HasForeignKey(pt => pt.TopicId);

            modelBuilder.Entity<PaperCitation>()
                .HasKey(pc => new { pc.CitingPaperId, pc.CitedPaperId });

            modelBuilder.Entity<PaperCitation>()
                .HasOne(pc => pc.CitingPaper)
                .WithMany(p => p.CitationsMade)
                .HasForeignKey(pc => pc.CitingPaperId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PaperCitation>()
                .HasOne(pc => pc.CitedPaper)
                .WithMany(p => p.CitationsReceived)
                .HasForeignKey(pc => pc.CitedPaperId)
                .OnDelete(DeleteBehavior.Restrict);

            // Map tên bảng cho các thực thể mới
            modelBuilder.Entity<Bookmark>().ToTable("Bookmarks");
            modelBuilder.Entity<SearchHistory>().ToTable("SearchHistories");
            modelBuilder.Entity<UserSubscription>().ToTable("UserSubscriptions");
            modelBuilder.Entity<SubscriptionPlan>().ToTable("SubscriptionPlans");
            modelBuilder.Entity<ApiDataSource>().ToTable("ApiDataSources");
            modelBuilder.Entity<ApiSyncLog>().ToTable("ApiSyncLogs");
            modelBuilder.Entity<AdminActivityLog>().ToTable("AdminActivityLogs");

            modelBuilder.Entity<UserRefreshToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Bổ sung khóa ngoại UserId/AdminId còn thiếu (các bảng này chỉ có cột int trơn,
            // chưa từng được khai báo navigation/FK nên trước đây không liên kết vật lý tới Users).
            modelBuilder.Entity<Bookmark>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SearchHistory>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AdminActivityLog>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.AdminId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne<Role>()
                .WithMany()
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FollowedItem>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSubscription>(entity =>
            {
                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Plan)
                      .WithMany()
                      .HasForeignKey(s => s.PlanId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(s => s.UserId);
            });

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
