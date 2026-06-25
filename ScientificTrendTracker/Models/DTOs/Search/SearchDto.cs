namespace ScientificTrendTracker.Models.DTOs.Search;

public class SearchHistoryRequestDto
{
    /// <summary>"keyword" | "author" | "journal" | "doi" | "openalex_id"</summary>
    public string SearchText { get; set; } = string.Empty;
    public string SearchType { get; set; } = "keyword";
}

public class SearchHistoryResponseDto
{
    public int SearchHistoryId { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public string SearchType { get; set; } = string.Empty;
    public DateTime SearchedAt { get; set; }
}

public class SearchSuggestionDto
{
    public string SearchText { get; set; } = string.Empty;
    public int Count { get; set; } // s? l?n tìm
}



