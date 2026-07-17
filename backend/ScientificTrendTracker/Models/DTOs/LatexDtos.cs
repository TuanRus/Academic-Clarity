namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// Citation sinh từ 1 paper trong corpus để chèn vào tài liệu LaTeX (module LaTeX Writer).
    /// </summary>
    public class CitationDto
    {
        /// <summary>Key dùng trong \cite{...} và \bibitem{...}, ví dụ "Smith2023".</summary>
        public string BibtexKey { get; set; }

        /// <summary>Entry BibTeX đầy đủ (@article{...}) để user copy dùng ngoài app.</summary>
        public string Bibtex { get; set; }

        /// <summary>Dòng \bibitem{key} ... để chèn thẳng vào môi trường thebibliography
        /// (tránh phải chạy pass bibtex khi compile trong browser).</summary>
        public string Bibitem { get; set; }
    }

    /// <summary>Body của POST api/latex/compile.</summary>
    public class LatexCompileRequest
    {
        /// <summary>Nội dung file .tex (đơn file).</summary>
        public string Content { get; set; }
    }

    /// <summary>Kết quả compile: luôn trả 200, phân biệt thành công/thất bại qua Pdf null hay không.</summary>
    public class LatexCompileResultDto
    {
        /// <summary>PDF mã hoá base64; null nếu compile thất bại.</summary>
        public string Pdf { get; set; }

        /// <summary>Log TeX (chỉ set khi thất bại) để FE hiển thị cho user sửa lỗi.</summary>
        public string Log { get; set; }
    }
}
