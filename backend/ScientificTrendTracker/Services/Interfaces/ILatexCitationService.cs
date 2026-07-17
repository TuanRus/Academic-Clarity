using ScientificTrendTracker.Models.DTOs;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Sinh citation (BibTeX + \bibitem) từ paper trong corpus cho module LaTeX Writer.
    /// Chỉ ĐỌC các bảng sẵn có (ResearchPapers/Journals/Authors) — không ghi DB.
    /// </summary>
    public interface ILatexCitationService
    {
        /// <summary>
        /// Sinh citation cho 1 paper.
        /// </summary>
        /// <param name="paperId">string - NGUỒN: Route parameter từ Controller - ID paper trong corpus.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>CitationDto (key + bibtex + bibitem), hoặc null nếu paper không tồn tại.</returns>
        Task<CitationDto> GenerateCitationAsync(string paperId, CancellationToken ct = default);
    }
}
