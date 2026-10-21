namespace Atlas.Licensing.Api;

public sealed class LibraryUpsertRequest
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string? CatalogId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Features { get; set; } = [];
    public List<string> Requirements { get; set; } = [];
}

public sealed class LibraryPlanUpsertRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Cycle { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}

public sealed class LibraryDocUpsertRequest
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Markdown { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
}

public sealed record LibraryPlanView(Guid Id, string Code, string Name, decimal Price, string Cycle, bool Active);
public sealed record LibraryReleaseView(Guid Id, string Version, string Sha256, long SizeBytes, bool SignaturePresent,
    DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt, bool IsActive);
public sealed record LibraryDocView(Guid Id, string Slug, string Title, string Summary, string Group, int Position,
    string Markdown, string Status);
public sealed record LibraryView(Guid Id, string Slug, string Name, string ProductId, string ModuleCode,
    string? CatalogId, string Summary, string Description, IReadOnlyList<string> Features,
    IReadOnlyList<string> Requirements, string Status, string? ImageUrl, DateTimeOffset? PublishedAt,
    IReadOnlyList<LibraryPlanView> Plans, IReadOnlyList<LibraryDocView> Docs, IReadOnlyList<LibraryReleaseView> Releases);

public sealed record LibraryDownload(string Path, string DownloadName, string Sha256);
