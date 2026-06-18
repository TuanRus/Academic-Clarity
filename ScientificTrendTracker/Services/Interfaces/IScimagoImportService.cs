namespace ScientificTrendTracker.Services.Interfaces
{
    public interface IScimagoImportService
    {
        /// <summary>
        /// Đọc file CSV SCImago và upsert dữ liệu Q-rank vào bảng Journals.
        /// Chỉ cập nhật các journal đã tồn tại trong DB (match theo ISSN).
        /// Journal chưa có trong DB sẽ bị bỏ qua vì chưa có paper nào thuộc journal đó.
        /// </summary>
        /// <param name="csvStream">
        /// Stream - AdminController nhận từ IFormFile.OpenReadStream() -
        /// Stream của file CSV SCImago do admin upload. Không đóng stream trong hàm này.
        /// </param>
        /// <returns>
        /// ScimagoImportResult - Object kết quả import.
        /// Bao gồm các thuộc tính:
        /// - TotalRowsRead (int): Tổng số dòng đọc được từ CSV (không tính header)
        /// - UpdatedCount (int): Số journal đã được cập nhật Q-rank thành công
        /// - SkippedCount (int): Số dòng bỏ qua do ISSN không khớp hoặc dữ liệu thiếu
        /// </returns>
        Task<ScimagoImportResult> ImportFromCsvAsync(Stream csvStream);
    }

    public class ScimagoImportResult
    {
        public int TotalRowsRead { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
    }
}
