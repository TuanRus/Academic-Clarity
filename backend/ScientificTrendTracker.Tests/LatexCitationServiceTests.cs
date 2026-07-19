using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services;

namespace ScientificTrendTracker.Tests
{
    /// <summary>
    /// Test LatexCitationService: các helper thuần (BuildKey/EscapeLatex/FormatAuthors)
    /// và luồng GenerateCitationAsync đầy đủ trên EF InMemory.
    /// </summary>
    public class LatexCitationServiceTests
    {
        // ---------- BuildKey ----------

        [Theory]
        [InlineData("John Smith", "Some Title", 2023, "Smith2023")]
        [InlineData("Nguyễn Văn Đức", "Some Title", 2024, "Duc2024")] // bỏ dấu + Đ→D
        [InlineData(null, "Attention Is All You Need", 2017, "Attention2017")] // không tác giả → từ đầu title
        public void BuildKey_TaoKeyDungChuan(string author, string title, int? year, string expected)
        {
            Assert.Equal(expected, LatexCitationService.BuildKey(author, title, year));
        }

        [Fact]
        public void BuildKey_KhongCoNam_KhongThemHauTo()
        {
            Assert.Equal("Smith", LatexCitationService.BuildKey("John Smith", "T", null));
        }

        // ---------- EscapeLatex ----------

        [Theory]
        [InlineData("AT&T 100% $5 #1 a_b", @"AT\&T 100\% \$5 \#1 a\_b")]
        [InlineData("f{x}", @"f\{x\}")]
        [InlineData("a~b^c", @"a\textasciitilde{}b\textasciicircum{}c")]
        public void EscapeLatex_EscapeKyTuDacBiet(string input, string expected)
        {
            Assert.Equal(expected, LatexCitationService.EscapeLatex(input));
        }

        [Fact]
        public void EscapeLatex_Backslash_KhongPhaVoBraceCuaChinhNo()
        {
            // Regression: escape backslash TRƯỚC {} từng sinh ra \textbackslash\{\} sai.
            Assert.Equal(@"a\textbackslash{}b", LatexCitationService.EscapeLatex(@"a\b"));
        }

        // ---------- FormatAuthorsForBibitem ----------

        [Fact]
        public void FormatAuthors_MotTacGia() =>
            Assert.Equal("A One", LatexCitationService.FormatAuthorsForBibitem(new List<string> { "A One" }));

        [Fact]
        public void FormatAuthors_BaTacGia_DungAnd() =>
            Assert.Equal("A, B and C", LatexCitationService.FormatAuthorsForBibitem(new List<string> { "A", "B", "C" }));

        [Fact]
        public void FormatAuthors_NhieuHonBa_EtAl() =>
            Assert.Equal("A, B, C et al", LatexCitationService.FormatAuthorsForBibitem(new List<string> { "A", "B", "C", "D", "E" }));

        // ---------- GenerateCitationAsync (EF InMemory) ----------

        private static AppDbContext NewDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private static void SeedPaper(AppDbContext db)
        {
            var journal = new Journal { JournalId = "J1", JournalName = "Journal of AI & Trends" };
            var paper = new ResearchPaper
            {
                PaperId = "P1",
                Title = "Deep Learning for Trend Forecasting: 100% Accuracy?",
                PublicationYear = 2023,
                Doi = "10.1000/test_doi",
                SourceUrl = "https://example.org/p1",
                JournalId = "J1",
                Journal = journal,
                OpenAlexId = "W1",
            };
            var a1 = new Author { AuthorId = 1, FullName = "Trần Thị Bình" };
            var a2 = new Author { AuthorId = 2, FullName = "John Smith" };
            db.Journals.Add(journal);
            db.ResearchPapers.Add(paper);
            db.Authors.AddRange(a1, a2);
            // AuthorOrder đảo ngược thứ tự insert → verify service sort theo AuthorOrder.
            db.PaperAuthors.AddRange(
                new PaperAuthor { PaperId = "P1", AuthorId = 2, AuthorOrder = 2, Author = a2, Paper = paper },
                new PaperAuthor { PaperId = "P1", AuthorId = 1, AuthorOrder = 1, Author = a1, Paper = paper });
            db.SaveChanges();
        }

        [Fact]
        public async Task GenerateCitation_PaperKhongTonTai_TraVeNull()
        {
            using var db = NewDb();
            var svc = new LatexCitationService(db);
            Assert.Null(await svc.GenerateCitationAsync("khong-co"));
        }

        [Fact]
        public async Task GenerateCitation_DungKey_DungThuTuTacGia_EscapeTitle()
        {
            using var db = NewDb();
            SeedPaper(db);
            var svc = new LatexCitationService(db);

            var c = await svc.GenerateCitationAsync("P1");

            Assert.NotNull(c);
            // Key từ tác giả AuthorOrder=1 (Trần Thị Bình → Binh) + năm.
            Assert.Equal("Binh2023", c.BibtexKey);
            // Thứ tự tác giả theo AuthorOrder, không theo thứ tự insert.
            Assert.Contains("author = {Trần Thị Bình and John Smith}", c.Bibtex);
            // Title có % và : → % phải được escape trong BibTeX.
            Assert.Contains(@"100\% Accuracy?", c.Bibtex);
            Assert.Contains("journal = {Journal of AI \\& Trends}", c.Bibtex);
            Assert.Contains("year = {2023}", c.Bibtex);
            Assert.Contains(@"doi = {10.1000/test\_doi}", c.Bibtex);
        }

        [Fact]
        public async Task GenerateCitation_Bibitem_DayDuThanhPhan()
        {
            using var db = NewDb();
            SeedPaper(db);
            var svc = new LatexCitationService(db);

            var c = await svc.GenerateCitationAsync("P1");

            Assert.StartsWith(@"\bibitem{Binh2023} ", c.Bibitem);
            Assert.Contains("Trần Thị Bình and John Smith", c.Bibitem);
            Assert.Contains(@"\textit{Journal of AI \& Trends}", c.Bibitem);
            Assert.Contains("2023.", c.Bibitem);
            Assert.Contains(@"DOI: 10.1000/test\_doi", c.Bibitem);
        }
    }
}
