using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Models.Entities;

namespace ScientificTrendTracker.Data
{
    public class AppDbContext : DbContext
    {
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
        }
    }
}
