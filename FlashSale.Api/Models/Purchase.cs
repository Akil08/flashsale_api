namespace FlashSale.Api.Models;

public class Purchase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FlashSaleId { get; set; }
    public FlashSale FlashSale { get; set; } = null!;

    public Guid BuyerId { get; set; }
    public User Buyer { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
