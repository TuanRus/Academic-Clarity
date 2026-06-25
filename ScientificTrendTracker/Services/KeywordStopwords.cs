namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Stoplist keyword generic dùng CHUNG cho mọi nguồn keyword (OpenAlex nền + AI/LLM).
    /// Mục tiêu: loại từ quá chung chung (1-2 chữ vô định hướng) làm nhiễu mindmap/trend.
    /// So khớp theo dạng đã chuẩn hoá (lowercase + dấu cách) qua KeywordNormalizer.
    /// </summary>
    public static class KeywordStopwords
    {
        public static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
        {
            // --- Từ "viết bài/nghiên cứu" generic (model hay trả nhầm) ---
            "study", "studies", "result", "results", "method", "methods", "approach", "approaches",
            "paper", "papers", "research", "analysis", "model", "models", "system", "systems",
            "data", "using", "based", "novel", "proposed", "performance", "problem", "problems",
            "application", "applications", "framework", "frameworks", "technique", "techniques",
            "experiment", "experiments", "evaluation", "review", "survey", "overview", "introduction",

            // --- Từ generic 1 chữ từ taxonomy OpenAlex (quan sát thực tế làm nhiễu mindmap) ---
            "benchmark", "feature", "context", "task", "tasks", "graph", "process", "image", "images",
            "set", "field", "object", "objects", "key", "code", "leverage", "inference", "representation",
            "computer science", "algorithm", "algorithms", "function", "functions", "value", "values",
            "structure", "design", "implementation", "architecture", "computation", "information",
            "knowledge", "quality", "control", "domain", "domains", "concept", "concepts", "scheme",
            "metric", "metrics", "baseline", "module", "component", "components", "factor", "state",
            "sample", "samples", "input", "output", "label", "labels", "node", "edge", "vector", "matrix"
        };

        /// <summary>True nếu keyword (đã chuẩn hoá) là generic/nhiễu → nên loại.</summary>
        public static bool IsGeneric(string normalizedKeyword)
            => Generic.Contains(normalizedKeyword);
    }
}
