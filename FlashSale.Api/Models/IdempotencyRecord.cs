namespace FlashSale.Api.Models;

public class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string ResponseBody { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
