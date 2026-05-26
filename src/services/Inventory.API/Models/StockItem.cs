namespace Inventory.API.Models;

public class StockItem
{
    public Guid Id { get; set; }
    public Guid PartId { get; set; }
    public string ArticleNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Reserved { get; set; }
    public int Available => Quantity - Reserved; // доступно для заказа
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}