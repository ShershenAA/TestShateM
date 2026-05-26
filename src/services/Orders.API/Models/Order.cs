namespace Orders.API.Models;

public class Order
{
    public Guid Id { get; set; }
    public Guid DealerId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? Comment { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid PartId { get; set; }
    public string ArticleNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => UnitPrice * Quantity;
}

public enum OrderStatus
{
    Pending = 0,      // создан, ждёт подтверждения
    Confirmed = 1,    // подтверждён (Inventory зарезервировал товар)
    Rejected = 2,     // отклонён (нет на складе)
    Shipped = 3,      // отгружен
    Delivered = 4,    // доставлен
    Cancelled = 5     // отменён
}