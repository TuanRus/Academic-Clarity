using System.Net;
using System.Text;
using ScientificTrendTracker.Services;

namespace ScientificTrendTracker.Tests
{
    /// <summary>
    /// Test LatexCompileService với HttpMessageHandler giả lập texlive.net:
    /// giao thức POST → 301 Location (.pdf/.log) → GET file kết quả,
    /// và 2 quirk multipart (boundary không nháy, field name có nháy).
    /// </summary>
    public class LatexCompileServiceTests
    {
        /// <summary>Handler giả: trả 301 về file .pdf hoặc .log tuỳ kịch bản, ghi lại request POST.</summary>
        private sealed class FakeTexliveHandler : HttpMessageHandler
        {
            private readonly string _resultPath;   // vd "/latexcgi/document_1.pdf"
            private readonly byte[] _resultBytes;
            private readonly string _resultContentType;

            public string CapturedContentTypeHeader;
            public string CapturedBody;

            public FakeTexliveHandler(string resultPath, byte[] resultBytes, string resultContentType)
            {
                _resultPath = resultPath;
                _resultBytes = resultBytes;
                _resultContentType = resultContentType;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (request.Method == HttpMethod.Post)
                {
                    CapturedContentTypeHeader = request.Content.Headers.TryGetValues("Content-Type", out var v)
                        ? string.Join(";", v) : "";
                    CapturedBody = await request.Content.ReadAsStringAsync(ct);
                    var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                    redirect.Headers.Location = new Uri(_resultPath, UriKind.Relative);
                    return redirect;
                }

                // GET file kết quả sau redirect.
                Assert.EndsWith(_resultPath, request.RequestUri.AbsolutePath);
                var res = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_resultBytes)
                };
                res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_resultContentType);
                return res;
            }
        }

        private static (LatexCompileService svc, FakeTexliveHandler handler) NewService(
            string resultPath, byte[] resultBytes, string contentType)
        {
            var handler = new FakeTexliveHandler(resultPath, resultBytes, contentType);
            return (new LatexCompileService(new HttpClient(handler)), handler);
        }

        [Fact]
        public async Task Compile_ThanhCong_TraPdfBase64()
        {
            var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7 fake");
            var (svc, _) = NewService("/latexcgi/document_1.pdf", pdfBytes, "application/pdf");

            var r = await svc.CompileAsync(@"\documentclass{article}...");

            Assert.NotNull(r.Pdf);
            Assert.Null(r.Log);
            Assert.Equal(pdfBytes, Convert.FromBase64String(r.Pdf));
        }

        [Fact]
        public async Task Compile_LoiTex_TraLogKhongTraPdf()
        {
            var log = Encoding.UTF8.GetBytes("! Undefined control sequence.\nl.3 \\undefinedmacro");
            var (svc, _) = NewService("/latexcgi/document_1.log", log, "text/plain");

            var r = await svc.CompileAsync("bad source");

            Assert.Null(r.Pdf);
            Assert.Contains("Undefined control sequence", r.Log);
        }

        [Fact]
        public async Task Compile_LogQuaDai_CatBotGiuPhanCuoi()
        {
            // Log 30k ký tự, lỗi nằm ở CUỐI — phần cuối phải được giữ lại sau khi cắt.
            var longLog = new string('x', 30000) + "END_OF_LOG_ERROR";
            var (svc, _) = NewService("/latexcgi/document_1.log", Encoding.UTF8.GetBytes(longLog), "text/plain");

            var r = await svc.CompileAsync("bad");

            Assert.True(r.Log.Length < 30000);
            Assert.Contains("END_OF_LOG_ERROR", r.Log);
            Assert.StartsWith("…(log truncated)…", r.Log);
        }

        [Fact]
        public async Task Compile_MultipartHopQuirkLatexcgi()
        {
            var (svc, handler) = NewService("/latexcgi/d.pdf", Encoding.ASCII.GetBytes("%PDF"), "application/pdf");
            await svc.CompileAsync("src");

            // Quirk 1: boundary trong header Content-Type KHÔNG được bọc nháy
            // (regex boundary=(\S+) của latexcgi sẽ bắt kèm nháy → "Bad form type").
            Assert.DoesNotContain("boundary=\"", handler.CapturedContentTypeHeader);
            Assert.Contains("multipart/form-data; boundary=", handler.CapturedContentTypeHeader);

            // Quirk 2: field name PHẢI có nháy — parser latexcgi match name="..." literal.
            Assert.Contains("name=\"filecontents[]\"", handler.CapturedBody);
            Assert.Contains("name=\"filename[]\"", handler.CapturedBody);
            Assert.Contains("document.tex", handler.CapturedBody);
            Assert.Contains("name=\"engine\"", handler.CapturedBody);
            Assert.Contains("pdflatex", handler.CapturedBody);
        }
    }
}
