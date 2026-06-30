using System.Text.Json.Serialization;

namespace ScientificTrendTracker.Services
{
    // Các model nội bộ để deserialize JSON response của OpenAlex (chỉ dùng trong OpenAlexService).
    // Tách khỏi OpenAlexService.cs cho gọn — KHÔNG dùng ở nơi khác (internal).

    internal class OpenAlexResponse
    {
        [JsonPropertyName("results")]
        public List<OpenAlexRawWork> Results { get; set; }
    }

    internal class OpenAlexRawWork
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("doi")]
        public string Doi { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("publication_year")]
        public int? PublicationYear { get; set; }

        [JsonPropertyName("publication_date")]
        public string PublicationDate { get; set; }

        [JsonPropertyName("cited_by_count")]
        public int CitedByCount { get; set; }

        [JsonPropertyName("abstract_inverted_index")]
        public Dictionary<string, List<int>> AbstractInvertedIndex { get; set; }

        [JsonPropertyName("primary_location")]
        public OpenAlexRawLocation PrimaryLocation { get; set; }

        [JsonPropertyName("authorships")]
        public List<OpenAlexRawAuthorship> Authorships { get; set; }

        [JsonPropertyName("primary_topic")]
        public OpenAlexRawTopic PrimaryTopic { get; set; }

        [JsonPropertyName("open_access")]
        public OpenAlexRawOpenAccess OpenAccess { get; set; }

        [JsonPropertyName("keywords")]
        public List<OpenAlexRawKeyword> Keywords { get; set; }
    }

    internal class OpenAlexRawKeyword
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } // dạng "https://openalex.org/keywords/<slug>"

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    internal class OpenAlexRawTopic
    {
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("subfield")]
        public OpenAlexRawNamed Subfield { get; set; }

        [JsonPropertyName("field")]
        public OpenAlexRawNamed Field { get; set; }

        [JsonPropertyName("domain")]
        public OpenAlexRawNamed Domain { get; set; }
    }

    internal class OpenAlexRawNamed
    {
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }
    }

    internal class OpenAlexRawOpenAccess
    {
        [JsonPropertyName("oa_status")]
        public string OaStatus { get; set; }
    }

    internal class OpenAlexRawLocation
    {
        [JsonPropertyName("source")]
        public OpenAlexRawSource Source { get; set; }
    }

    internal class OpenAlexRawSource
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("issn_l")]
        public string IssnL { get; set; }

        [JsonPropertyName("issn")]
        public List<string> Issn { get; set; }
    }

    internal class OpenAlexRawAuthorship
    {
        [JsonPropertyName("author")]
        public OpenAlexRawAuthor Author { get; set; }

        [JsonPropertyName("institutions")]
        public List<OpenAlexRawInstitution> Institutions { get; set; }
    }

    internal class OpenAlexRawAuthor
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }
    }

    internal class OpenAlexRawInstitution
    {
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }
    }
}
