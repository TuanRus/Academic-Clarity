namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// Một điểm trên đường trend theo thời gian (1 kỳ = 1 năm hoặc 1 tháng).
    /// </summary>
    public class TrendPointDto
    {
        /// <summary>Nhãn kỳ: "2024" (theo năm) hoặc "2024-03" (theo tháng).</summary>
        public string Period { get; set; }

        /// <summary>Số bài chứa entity (keyword/author/journal) trong kỳ đó.</summary>
        public int Count { get; set; }

        /// <summary>Tổng số bài (mọi entity) trong kỳ đó — dùng chuẩn hóa.</summary>
        public int PeriodTotal { get; set; }

        /// <summary>Tần suất tương đối = Count / PeriodTotal (0..1). Chỉ số trend chính.</summary>
        public double Share { get; set; }
    }

    /// <summary>
    /// Chuỗi thời gian trend của 1 entity (keyword/author/journal) + độ dốc + hướng.
    /// </summary>
    public class TrendSeriesDto
    {
        /// <summary>Loại entity: "keyword" / "author" / "journal".</summary>
        public string Dimension { get; set; }

        /// <summary>Giá trị entity đã tra (tên keyword/author/journal).</summary>
        public string Value { get; set; }

        /// <summary>Cách gộp kỳ: "year" hoặc "month".</summary>
        public string GroupBy { get; set; }

        /// <summary>Tổng số bài chứa entity trong toàn khoảng lọc.</summary>
        public int TotalPapers { get; set; }

        /// <summary>Dữ liệu theo từng kỳ, sắp tăng dần theo thời gian.</summary>
        public List<TrendPointDto> Series { get; set; } = new();

        /// <summary>Độ dốc hồi quy của Share theo kỳ (dương = đang lên).</summary>
        public double Slope { get; set; }

        /// <summary>"rising" / "falling" / "stable".</summary>
        public string Direction { get; set; }
    }

    /// <summary>
    /// Một dòng trong bảng xếp hạng top entity (keyword/author/journal) trong khoảng thời gian.
    /// </summary>
    public class TrendTopItemDto
    {
        /// <summary>Tên entity (keyword/author/journal).</summary>
        public string Name { get; set; }

        /// <summary>Số bài chứa entity trong khoảng lọc.</summary>
        public int Count { get; set; }

        /// <summary>Tổng số bài trong khoảng lọc — dùng chuẩn hóa.</summary>
        public int RangeTotal { get; set; }

        /// <summary>Tần suất tương đối = Count / RangeTotal (0..1).</summary>
        public double Share { get; set; }

        /// <summary>Độ dốc hồi quy của share theo năm trong khoảng (dương = đang lên). Dùng để sắp xếp tăng/giảm.</summary>
        public double Slope { get; set; }

        /// <summary>"rising" / "falling" / "stable" — diễn giải Slope.</summary>
        public string Direction { get; set; }
    }
}
