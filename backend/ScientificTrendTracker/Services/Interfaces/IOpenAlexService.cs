using ScientificTrendTracker.Models.Entities;

namespace ScientificTrendTracker.Services.Interfaces
{
    public interface IOpenAlexService
    {
        /// <summary>
        /// Gọi OpenAlex REST API để lấy danh sách bài báo Computer Science theo trang.
        /// Filter cứng: CS concept, năm >= 2022, cited_by_count >= 3. Dùng cho weekly sync.
        /// </summary>
        /// <param name="page">
        /// int - Caller truyền vào - Số trang cần fetch, bắt đầu từ 1.
        /// </param>
        /// <param name="pageSize">
        /// int - Caller truyền vào - Số bài mỗi trang, mặc định 200 (giới hạn tối đa của OpenAlex).
        /// </param>
        /// <returns>
        /// List&lt;OpenAlexPaper&gt; - Danh sách bài báo đã map từ JSON response của OpenAlex.
        /// Mỗi OpenAlexPaper bao gồm các thuộc tính:
        /// - Id (string): OpenAlex work ID dạng "https://openalex.org/W..."
        /// - Doi (string): DOI của bài báo, có thể null
        /// - Title (string): Tiêu đề bài báo
        /// - PublicationYear (int?): Năm xuất bản
        /// - PublicationDate (string): Ngày xuất bản dạng "yyyy-MM-dd", có thể null
        /// - CitedByCount (int): Số lượt trích dẫn
        /// - AbstractReconstructed (string): Abstract đã reconstruct từ inverted index, có thể null — KHÔNG lưu DB
        /// - PrimaryLocation.Source.IssnL (string): ISSN dùng để tra Q-rank từ SCImago
        /// - Authorships (List): Danh sách tác giả kèm thứ tự và affiliation
        /// Trả về list rỗng nếu hết trang hoặc API lỗi.
        /// </returns>
        Task<List<OpenAlexPaper>> FetchPapersAsync(
            int page, int pageSize = 200, int fromYear = 2022, int minCitedExclusive = 2,
            bool recentFirst = false, int toYear = 0);

        /// <summary>
        /// Fetch abstract của một bài báo cụ thể từ OpenAlex theo OpenAlexId.
        /// Dùng cho reprocess-keywords khi cần abstract nhưng không có trong DB.
        /// </summary>
        /// <param name="openAlexId">
        /// string - ResearchPaper.OpenAlexId trong DB (dạng "W1234567890", không có prefix URL).
        /// </param>
        /// <returns>
        /// string - Abstract đã reconstruct, hoặc null nếu không có abstract hoặc API lỗi.
        /// </returns>
        Task<string> FetchAbstractByIdAsync(string openAlexId);

        /// <summary>Lấy nhanh chỉ primary_topic.display_name của 1 bài. Dùng cho job backfill Topic.</summary>
        Task<string> FetchTopicByIdAsync(string openAlexId);

        /// <summary>
        /// Lấy chi tiết bổ sung của 1 bài từ OpenAlex trong MỘT request: abstract + primary_topic
        /// (topic/subfield/field/domain) + open_access status + danh sách institution của tác giả.
        /// Dùng cho màn Paper Detail. Các trường này lấy on-demand (Topic được lưu DB lúc sync, còn lại không).
        /// </summary>
        /// <param name="openAlexId">string - ResearchPaper.OpenAlexId (dạng "W..." hoặc URL OpenAlex).</param>
        /// <returns>OpenAlexWorkDetail - các trường có thể null nếu OpenAlex không có; null nếu API lỗi.</returns>
        Task<OpenAlexWorkDetail> FetchWorkDetailAsync(string openAlexId);

        /// <summary>
        /// Lấy TOÀN BỘ metadata của 1 bài từ OpenAlex để Admin thêm thủ công qua link/DOI/ID.
        /// Chấp nhận: URL "https://openalex.org/W...", ID trần "W...", URL "https://doi.org/10..." hoặc DOI trần "10...".
        /// </summary>
        /// <param name="idOrDoiOrUrl">string - Admin dán vào - Link OpenAlex, link DOI, ID work hoặc DOI.</param>
        /// <returns>OpenAlexPaper đã map (title/doi/năm/tạp chí/tác giả/keyword); null nếu không tìm thấy hoặc API lỗi.</returns>
        Task<OpenAlexPaper> FetchSingleWorkAsync(string idOrDoiOrUrl);

        /// <summary>
        /// Reconstruct full text abstract từ abstract_inverted_index trả về bởi OpenAlex.
        /// OpenAlex lưu dạng inverted index (word → [positions]) thay vì full text để tránh bản quyền.
        /// Abstract chỉ dùng trong memory để extract keyword, tuyệt đối KHÔNG lưu vào DB.
        /// </summary>
        /// <param name="invertedIndex">
        /// Dictionary&lt;string, List&lt;int&gt;&gt; - OpenAlex API trả về trong field "abstract_inverted_index" -
        /// Key là từ, Value là danh sách vị trí (index) của từ đó trong câu.
        /// </param>
        /// <returns>
        /// string - Full text abstract đã ghép lại theo thứ tự vị trí.
        /// Trả về null nếu invertedIndex null hoặc rỗng.
        /// </returns>
        string ReconstructAbstract(Dictionary<string, List<int>> invertedIndex);
    }

    /// <summary>
    /// DTO nội bộ map từ JSON response của OpenAlex API.
    /// Chỉ dùng trong tầng Service, không expose ra ngoài.
    /// </summary>
    public class OpenAlexPaper
    {
        public string Id { get; set; }
        public string Doi { get; set; }
        public string Title { get; set; }
        public int? PublicationYear { get; set; }
        public string PublicationDate { get; set; }
        public int CitedByCount { get; set; }
        public string AbstractReconstructed { get; set; }
        public OpenAlexLocation PrimaryLocation { get; set; }
        public List<OpenAlexAuthorship> Authorships { get; set; } = new();

        /// <summary>Chủ đề chính (primary_topic.display_name) — lưu vào ResearchPaper.Topic khi sync.</summary>
        public string Topic { get; set; }

        /// <summary>Keyword do OpenAlex gán (đã chuẩn hóa slug) — nguồn keyword chất lượng, không cần AI.</summary>
        public List<string> Keywords { get; set; } = new();
    }

    /// <summary>
    /// Chi tiết bổ sung lấy on-demand từ OpenAlex cho màn Paper Detail (không lưu DB).
    /// </summary>
    public class OpenAlexWorkDetail
    {
        public string Abstract { get; set; }
        public string Topic { get; set; }
        public string Subfield { get; set; }
        public string Field { get; set; }
        public string Domain { get; set; }
        public string OpenAccessStatus { get; set; }
        public List<string> Institutions { get; set; } = new();
    }

    public class OpenAlexLocation
    {
        public OpenAlexSource Source { get; set; }
    }

    public class OpenAlexSource
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string IssnL { get; set; }
        public List<string> Issn { get; set; } = new();
    }

    public class OpenAlexAuthorship
    {
        public OpenAlexAuthor Author { get; set; }
        public int AuthorPosition { get; set; }
        public List<OpenAlexInstitution> Institutions { get; set; } = new();
    }

    public class OpenAlexAuthor
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
    }

    public class OpenAlexInstitution
    {
        public string DisplayName { get; set; }
    }
}
