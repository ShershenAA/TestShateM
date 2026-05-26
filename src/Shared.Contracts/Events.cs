namespace Shared.Contracts;

// Orders.API публикует → Inventory.API и Notifications.API слушают
public record OrderCreated(
    Guid OrderId,
    Guid DealerId,
    List<OrderItemDto> Items,
    decimal TotalAmount,
    DateTime CreatedAt
);

// Inventory.API публикует → Notifications.API слушает
public record OrderConfirmed(
    Guid OrderId,
    Guid DealerId,
    DateTime ConfirmedAt
);

// Inventory.API публикует если товара нет
public record OrderRejected(
    Guid OrderId,
    Guid DealerId,
    string Reason,
    DateTime RejectedAt
);

// Вспомогательный DTO внутри события
public record OrderItemDto(
    Guid PartId,
    string ArticleNumber,
    int Quantity,
    decimal UnitPrice
);