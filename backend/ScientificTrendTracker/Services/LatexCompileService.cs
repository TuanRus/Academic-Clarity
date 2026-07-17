using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Proxy compile LaTeX qua https://texlive.net (latexcgi — dịch vụ mở của David Carlisle,
    /// learnlatex.org dùng production). Giao thức: POST multipart → 301 Location trỏ tới
    /// file kết quả (.pdf khi thành công, .log khi lỗi) → GET file đó.
    /// HttpClient của service này được cấu hình AllowAutoRedirect=false trong Program.cs
    /// để tự xử lý redirect (POST 301 auto-follow sẽ đổi method thành GET — tự xử lý rõ ràng hơn).
    /// </summary>
    public class LatexCompileService : ILatexCompileService
    {
        private const string Endpoint = "https://texlive.net/cgi-bin/latexcgi";
        private static readonly Uri BaseUri = new("https://texlive.net");

        private readonly HttpClient _http;

        public LatexCompileService(HttpClient http)
        {
            _http = http;
        }

        public async Task<LatexCompileResultDto> CompileAsync(string source, CancellationToken ct = default)
        {
            // 2 quirk của .NET so với parser Perl thủ công của latexcgi:
            // (1) .NET bọc boundary trong dấu nháy (boundary="...") — regex boundary=(\S+) của latexcgi
            //     sẽ bắt kèm nháy và không khớp dòng --... nào → phải tự set header không nháy.
            // (2) .NET không bọc nháy field name — latexcgi lại yêu cầu name="..." → tự thêm nháy.
            var boundary = "----AcademicClarity" + Guid.NewGuid().ToString("N");
            using var form = new MultipartFormDataContent(boundary)
            {
                { new StringContent(source ?? ""), "\"filecontents[]\"" },
                { new StringContent("document.tex"), "\"filename[]\"" },
                { new StringContent("pdflatex"), "\"engine\"" },
                { new StringContent("pdf"), "\"return\"" },
            };
            form.Headers.Remove("Content-Type");
            form.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);

            using var postRes = await _http.PostAsync(Endpoint, form, ct);

            // Thành công lẫn thất bại đều trả redirect tới file kết quả trên texlive.net.
            HttpResponseMessage finalRes;
            if ((int)postRes.StatusCode is >= 301 and <= 303 && postRes.Headers.Location != null)
            {
                var target = postRes.Headers.Location.IsAbsoluteUri
                    ? postRes.Headers.Location
                    : new Uri(BaseUri, postRes.Headers.Location);
                finalRes = await _http.GetAsync(target, ct);
            }
            else
            {
                finalRes = postRes; // phòng khi dịch vụ trả thẳng nội dung không redirect
            }

            try
            {
                var contentType = finalRes.Content.Headers.ContentType?.MediaType ?? "";
                if (finalRes.IsSuccessStatusCode && contentType.Contains("pdf"))
                {
                    var bytes = await finalRes.Content.ReadAsByteArrayAsync(ct);
                    return new LatexCompileResultDto { Pdf = Convert.ToBase64String(bytes) };
                }

                var log = await finalRes.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(log))
                    log = $"Compile service responded HTTP {(int)finalRes.StatusCode} without a TeX log.";
                // Log TeX có thể rất dài — cắt bớt phần đầu, giữ phần cuối (nơi chứa lỗi).
                if (log.Length > 20000) log = "…(log truncated)…\n" + log[^20000..];
                return new LatexCompileResultDto { Log = log };
            }
            finally
            {
                if (!ReferenceEquals(finalRes, postRes)) finalRes.Dispose();
            }
        }
    }
}
