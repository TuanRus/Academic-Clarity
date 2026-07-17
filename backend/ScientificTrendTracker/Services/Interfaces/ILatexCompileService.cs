using ScientificTrendTracker.Models.DTOs;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Compile LaTeX → PDF bằng cách proxy tới dịch vụ công cộng texlive.net (latexcgi của David Carlisle).
    /// Backend KHÔNG cài TeX — chỉ chuyển tiếp nguồn .tex và trả PDF/log về.
    /// (Browser không gọi thẳng texlive.net được vì redirect 301 của nó thiếu header CORS.)
    /// </summary>
    public interface ILatexCompileService
    {
        /// <summary>
        /// Compile 1 tài liệu LaTeX đơn file.
        /// </summary>
        /// <param name="source">string - Nội dung file .tex từ editor của FE.</param>
        /// <param name="ct">CancellationToken - ASP.NET Core runtime.</param>
        /// <returns>Pdf (base64) khi thành công; ngược lại Pdf=null và Log chứa log lỗi TeX.</returns>
        Task<LatexCompileResultDto> CompileAsync(string source, CancellationToken ct = default);
    }
}
