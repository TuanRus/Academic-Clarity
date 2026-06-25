namespace ScientificTrendTracker.Models.DTOs.Bookmark;

/// <summary>Request t?o bookmark m?i</summary>
public class BookmarkRequestDto
{
    /// <summary>"paper" ho?c "keyword"</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>PaperId n?u TargetType = "paper"</summary>
    public string? PaperId { get; set; }

    /// <summary>KeywordId n?u TargetType = "keyword"</summary>
    public string? KeywordId { get; set; }
}

/// <summary>Response tr? v? 1 bookmark</summary>
public class BookmarkResponseDto
{
    public int BookmarkId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string? PaperId { get; set; }
    public string? KeywordId { get; set; }
    public DateTime CreatedAt { get; set; }
}



