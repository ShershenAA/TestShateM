using Inventory.API.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace Inventory.API.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly InventoryDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(InventoryDbContext db, IPublishEndpoint publish, ILogger<OrderCreatedConsumer> logger)
    {
        _db = db;
        _publish = publish;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var order = context.Message;
        _logger.LogInformation("Received OrderCreated: {OrderId}", order.OrderId);

        // Проверяем наличие всех позиций заказа
        foreach (var item in order.Items)
        {
            var stock = await _db.StockItems
                .FirstOrDefaultAsync(s => s.PartId == item.PartId);

            if (stock is null || stock.Available < item.Quantity)
            {
                // Товара нет — отклоняем весь заказ
                await _publish.Publish(new OrderRejected(
                    OrderId: order.OrderId,
                    DealerId: order.DealerId,
                    Reason: stock is null
                        ? $"Part {item.ArticleNumber} not found in stock"
                        : $"Not enough stock for {item.ArticleNumber}: available {stock.Available}, requested {item.Quantity}",
                    RejectedAt: DateTime.UtcNow
                ));

                _logger.LogWarning("Order {OrderId} rejected", order.OrderId);
                return;
            }
        }

        // Всё есть — резервируем
        foreach (var item in order.Items)
        {
            var stock = await _db.StockItems
                .FirstOrDefaultAsync(s => s.PartId == item.PartId);

            stock!.Reserved += item.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Публикуем подтверждение
        await _publish.Publish(new OrderConfirmed(
            OrderId: order.OrderId,
            DealerId: order.DealerId,
            ConfirmedAt: DateTime.UtcNow
        ));

        _logger.LogInformation("Order {OrderId} confirmed, stock reserved", order.OrderId);
    }
}