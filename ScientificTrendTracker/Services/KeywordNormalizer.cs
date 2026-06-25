namespace ScientificTrendTracker.Services
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Chuẩn hóa chuỗi keyword để so khớp nhất quán toàn hệ thống.
    /// Keyword lưu trong DB dạng lowercase + DẤU CÁCH (vd "big data"), nên người dùng
    /// gõ "Big Data" / "big data" / "big-data" (slug OpenAlex) đều quy về cùng một dạng.
    /// </summary>
    public static class KeywordNormalizer
    {
        public static string Normalize(string raw)
        {
            var s = (raw ?? string.Empty).ToLower().Replace('-', ' ');
            // gộp mọi khoảng trắng liên tiếp thành 1 dấu cách + trim
            return Regex.Replace(s, @"\s+", " ").Trim();
        }
    }
}
