namespace API.Model;

public class IdempotencyRecord
{
    public long Id { get; set; }

    public string Key { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string Method { get; set; } = null!;

    public string Path { get; set; } = null!;

    public string Status { get; set; } = "Processing";

    public int? ResponseStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? ContentType { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}