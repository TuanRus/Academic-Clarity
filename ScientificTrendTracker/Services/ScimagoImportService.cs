using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    public class ScimagoImportService : IScimagoImportService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ScimagoImportService> _logger;

        // Tên cột trong file CSV SCImago (dùng semicolon làm delimiter)
        private const char CsvDelimiter = ';';
        private const string ColIssn = "Issn";
        private const string ColTitle = "Title";
        private const string ColQuartile = "SJR Best Quartile";
        private const string ColHIndex = "H index";
        private const string ColRankingYear = "Year"; // SCImago CSV có cột Year

        public ScimagoImportService(AppDbContext dbContext, ILogger<ScimagoImportService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

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
        public async Task<ScimagoImportResult> ImportFromCsvAsync(Stream csvStream)
        {
            var result = new ScimagoImportResult();

            using var reader = new StreamReader(csvStream, leaveOpen: true);

            var headerLine = await reader.ReadLineAsync();
            if (headerLine == null)
            {
                _logger.LogWarning("File CSV SCImago rỗng.");
                return result;
            }

            var headers = headerLine.Split(CsvDelimiter);
            var colIndex = BuildColumnIndex(headers);

            if (!colIndex.ContainsKey(ColIssn) || !colIndex.ContainsKey(ColQuartile))
            {
                _logger.LogError("CSV SCImago thiếu cột bắt buộc '{Issn}' hoặc '{Quartile}'.", ColIssn, ColQuartile);
                return result;
            }

            // Load toàn bộ ISSN trong DB vào memory để tránh N+1 query
            var journalsByIssn = await _dbContext.Journals
                .Where(j => j.IssnPrint != null || j.IssnElectronic != null)
                .ToListAsync();

            var issnLookup = journalsByIssn
                .SelectMany(j => new[]
                {
                    (Issn: NormalizeIssn(j.IssnPrint), Journal: j),
                    (Issn: NormalizeIssn(j.IssnElectronic), Journal: j)
                })
                .Where(x => x.Issn != null)
                .GroupBy(x => x.Issn)
                .ToDictionary(g => g.Key, g => g.First().Journal);

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                result.TotalRowsRead++;
                var cols = SplitCsvLine(line, CsvDelimiter);

                var rawIssn = GetColumn(cols, colIndex, ColIssn);
                var quartile = GetColumn(cols, colIndex, ColQuartile);

                if (string.IsNullOrWhiteSpace(rawIssn) || string.IsNullOrWhiteSpace(quartile))
                {
                    result.SkippedCount++;
                    continue;
                }

                // SCImago ISSN field có thể chứa nhiều ISSN cách nhau bởi dấu phẩy
                var issnList = rawIssn.Split(',')
                    .Select(NormalizeIssn)
                    .Where(i => i != null)
                    .ToList();

                var matched = issnList
                    .Select(issn => issnLookup.TryGetValue(issn, out var j) ? j : null)
                    .FirstOrDefault(j => j != null);

                if (matched == null)
                {
                    result.SkippedCount++;
                    continue;
                }

                matched.QuartileRank = quartile.Trim();

                if (colIndex.TryGetValue(ColHIndex, out var hCol))
                {
                    var hStr = GetColumn(cols, hCol);
                    if (int.TryParse(hStr, out var hIndex))
                        matched.HIndex = hIndex;
                }

                if (colIndex.TryGetValue(ColRankingYear, out var yearCol))
                {
                    var yearStr = GetColumn(cols, yearCol);
                    if (int.TryParse(yearStr, out var year))
                        matched.RankingYear = year;
                }

                result.UpdatedCount++;
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation(
                "SCImago import xong: {Total} dòng, {Updated} cập nhật, {Skipped} bỏ qua.",
                result.TotalRowsRead, result.UpdatedCount, result.SkippedCount);

            return result;
        }

        /// <summary>Tạo map "tên cột → vị trí" từ dòng header để đọc cột theo tên (không phụ thuộc thứ tự).</summary>
        /// <param name="headers">string[] - Tách từ dòng header CSV - Mảng tên cột.</param>
        /// <returns>Dictionary&lt;string,int&gt; - Tên cột (không phân biệt hoa thường) → chỉ số cột.</returns>
        private Dictionary<string, int> BuildColumnIndex(string[] headers)
        {
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
                index[headers[i].Trim('"').Trim()] = i;
            return index;
        }

        /// <summary>Lấy giá trị 1 cột theo TÊN cột (qua colIndex).</summary>
        /// <param name="cols">string[] - Caller truyền vào - Các ô của 1 dòng dữ liệu.</param>
        /// <param name="index">Dictionary&lt;string,int&gt; - BuildColumnIndex trả về - Map tên cột → vị trí.</param>
        /// <param name="colName">string - Caller truyền vào - Tên cột cần lấy.</param>
        /// <returns>string - Giá trị ô (đã trim nháy/khoảng trắng), null nếu không có cột.</returns>
        private string GetColumn(string[] cols, Dictionary<string, int> index, string colName)
        {
            if (!index.TryGetValue(colName, out var i)) return null;
            return GetColumn(cols, i);
        }

        /// <summary>Lấy giá trị 1 cột theo CHỈ SỐ.</summary>
        /// <param name="cols">string[] - Caller truyền vào - Các ô của 1 dòng dữ liệu.</param>
        /// <param name="index">int - Caller truyền vào - Vị trí cột.</param>
        /// <returns>string - Giá trị ô (đã trim), null nếu index ngoài phạm vi.</returns>
        private string GetColumn(string[] cols, int index)
        {
            if (index < 0 || index >= cols.Length) return null;
            return cols[index].Trim('"').Trim();
        }

        /// <summary>Chuẩn hóa ISSN về dạng "XXXX-XXXX" để so khớp với ISSN trong DB.</summary>
        /// <param name="issn">string - Caller truyền vào - ISSN thô từ CSV hoặc DB (có/không dấu gạch).</param>
        /// <returns>string - ISSN dạng "XXXX-XXXX", null nếu rỗng hoặc không đủ 8 ký tự.</returns>
        private string NormalizeIssn(string issn)
        {
            if (string.IsNullOrWhiteSpace(issn)) return null;
            // Chuẩn hóa về dạng XXXX-XXXX
            var clean = issn.Replace("-", "").Trim();
            if (clean.Length == 8)
                return $"{clean[..4]}-{clean[4..]}";
            return null;
        }

        /// <summary>
        /// Tách 1 dòng CSV thành các ô, tôn trọng dấu nháy kép (field bọc trong "..." có thể chứa delimiter bên trong).
        /// </summary>
        /// <param name="line">string - Caller truyền vào - Một dòng nội dung CSV.</param>
        /// <param name="delimiter">char - Caller truyền vào - Ký tự phân cách (SCImago dùng ';').</param>
        /// <returns>string[] - Mảng các ô đã tách.</returns>
        private string[] SplitCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            foreach (var ch in line)
            {
                if (ch == '"') { inQuotes = !inQuotes; continue; }
                if (ch == delimiter && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(ch);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}
