namespace FlashSale.Api.Models;

public enum SaleStatus
{
    Scheduled,
    Open,
    Closed
}

public class FlashSale
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public int SoldCount { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Scheduled;

    public uint RowVersion { get; set; }
}
