using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Orders.API.Controllers;
using Orders.API.Data;
using Orders.API.Models;
using Shared.Contracts;

namespace Orders.API.Tests;

public class OrdersControllerTests
{
    private readonly OrdersDbContext _db;
    private readonly Mock<IPublishEndpoint> _publishMock;
    private readonly Mock<ILogger<OrdersController>> _loggerMock;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OrdersDbContext(options);
        _publishMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<OrdersController>>();

        _controller = new OrdersController(_db, _publishMock.Object, _loggerMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────

    private static CreateOrderDto CreateValidOrderDto(int quantity = 2, decimal price = 100m)
    {
        return new CreateOrderDto(
            DealerId: Guid.NewGuid(),
            Comment: "Test order",
            Items: new List<CreateOrderItemDto>
            {
                new(
                    PartId: Guid.NewGuid(),
                    ArticleNumber: "ART-001",
                    PartName: "Brake Pad",
                    Quantity: quantity,
                    UnitPrice: price
                )
            }
        );
    }

    // ── GetAll ────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsAllOrders()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        _db.Orders.AddRange(
            new Order
            {
                Id = Guid.NewGuid(),
                DealerId = dealerId,
                Status = OrderStatus.Pending,
                TotalAmount = 200m,
                Items = new List<OrderItem>()
            },
            new Order
            {
                Id = Guid.NewGuid(),
                DealerId = dealerId,
                Status = OrderStatus.Confirmed,
                TotalAmount = 500m,
                Items = new List<OrderItem>()
            }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.GetAll(CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var orders = ok.Value.Should().BeAssignableTo<IEnumerable<OrderDto>>().Subject;
        orders.Should().HaveCount(2);
    }

    // ── GetById ───────────────────────────────────────────

    [Fact]
    public async Task GetById_WhenExists_ReturnsOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _db.Orders.Add(new Order
        {
            Id = orderId,
            DealerId = Guid.NewGuid(),
            Status = OrderStatus.Pending,
            TotalAmount = 300m,
            Items = new List<OrderItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PartId = Guid.NewGuid(),
                    ArticleNumber = "ART-001",
                    PartName = "Brake Pad",
                    Quantity = 3,
                    UnitPrice = 100m
                }
            }
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.GetById(orderId, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var order = ok.Value.Should().BeOfType<OrderDto>().Subject;
        order.Id.Should().Be(orderId);
        order.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_WhenNotExists_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── Create ────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidOrder_SavesAndPublishesEvent()
    {
        // Arrange
        var dto = CreateValidOrderDto(quantity: 3, price: 150m);

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert — статус 201
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var order = created.Value.Should().BeOfType<OrderDto>().Subject;

        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Should().Be(450m); // 3 * 150
        order.Items.Should().HaveCount(1);

        // Сохранилось в БД
        var inDb = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        inDb.Should().NotBeNull();
        inDb!.Items.Should().HaveCount(1);

        // Событие опубликовано в RabbitMQ
        _publishMock.Verify(
            p => p.Publish(
                It.Is<OrderCreated>(e =>
                    e.OrderId == order.Id &&
                    e.Items.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_CalculatesTotalCorrectly()
    {
        // Arrange — два товара разной цены и количества
        var dto = new CreateOrderDto(
            DealerId: Guid.NewGuid(),
            Comment: null,
            Items: new List<CreateOrderItemDto>
            {
                new(Guid.NewGuid(), "ART-A", "Part A", Quantity: 2, UnitPrice: 100m),
                new(Guid.NewGuid(), "ART-B", "Part B", Quantity: 3, UnitPrice: 200m)
            }
        );

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert — 2*100 + 3*200 = 800
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var order = created.Value.Should().BeOfType<OrderDto>().Subject;
        order.TotalAmount.Should().Be(800m);
    }

    // ── Cancel ────────────────────────────────────────────

    [Fact]
    public async Task Cancel_PendingOrder_SetsStatusCancelled()
    {
        // Arrange
        var order = new Order
        {
            Id = Guid.NewGuid(),
            DealerId = Guid.NewGuid(),
            Status = OrderStatus.Pending,
            TotalAmount = 200m,
            Items = new List<OrderItem>()
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.Cancel(order.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var inDb = await _db.Orders.FindAsync(order.Id);
        inDb!.Status.Should().Be(OrderStatus.Cancelled);
        inDb.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_ReturnsBadRequest()
    {
        // Arrange — нельзя отменить уже подтверждённый заказ
        var order = new Order
        {
            Id = Guid.NewGuid(),
            DealerId = Guid.NewGuid(),
            Status = OrderStatus.Confirmed,
            TotalAmount = 200m,
            Items = new List<OrderItem>()
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.Cancel(order.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        // Статус не изменился
        var inDb = await _db.Orders.FindAsync(order.Id);
        inDb!.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Cancel_NonExistingOrder_ReturnsNotFound()
    {
        // Act
        var result = await _controller.Cancel(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}