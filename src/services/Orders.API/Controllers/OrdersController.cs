using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.API.Data;
using Orders.API.Models;
using Shared.Contracts;

namespace Orders.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrdersDbContext db, IPublishEndpoint publish, ILogger<OrdersController> logger)
    {
        _db = db;
        _publish = publish;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return Ok(orders.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return NotFound();

        return Ok(ToDto(order));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, CancellationToken ct)
    {
        // Собираем заказ
        var order = new Order
        {
            Id = Guid.NewGuid(),
            DealerId = dto.DealerId,
            Comment = dto.Comment,
            Items = dto.Items.Select(i => new OrderItem
            {
                Id = Guid.NewGuid(),
                PartId = i.PartId,
                ArticleNumber = i.ArticleNumber,
                PartName = i.PartName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Публикуем событие в RabbitMQ — Inventory и Notifications подхватят
        await _publish.Publish(new OrderCreated(
            OrderId: order.Id,
            DealerId: order.DealerId,
            Items: order.Items.Select(i => new OrderItemDto(
                i.PartId,
                i.ArticleNumber,
                i.Quantity,
                i.UnitPrice)).ToList(),
            TotalAmount: order.TotalAmount,
            CreatedAt: order.CreatedAt
        ), ct);

        _logger.LogInformation("Order created: {OrderId}, total: {Total}", order.Id, order.TotalAmount);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, ToDto(order));
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return NotFound();

        if (order.Status != OrderStatus.Pending)
            return BadRequest($"Cannot cancel order with status {order.Status}");

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Order cancelled: {OrderId}", order.Id);

        return Ok(order);
    }
    
    private static OrderDto ToDto(Order o) => new(
        o.Id, o.DealerId, o.Status, o.TotalAmount, o.CreatedAt, o.Comment,
        o.Items.Select(i => new OrderItemDto2(
            i.PartId, i.ArticleNumber, i.PartName, i.Quantity, i.UnitPrice, i.TotalPrice
        )).ToList()
    );
}

public record CreateOrderDto(
    Guid DealerId,
    string? Comment,
    List<CreateOrderItemDto> Items
);

public record CreateOrderItemDto(
    Guid PartId,
    string ArticleNumber,
    string PartName,
    int Quantity,
    decimal UnitPrice
);
public record OrderDto(
    Guid Id,
    Guid DealerId,
    OrderStatus Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    string? Comment,
    List<OrderItemDto2> Items
);

public record OrderItemDto2(
    Guid PartId,
    string ArticleNumber,
    string PartName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

