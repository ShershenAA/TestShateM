using FluentAssertions;
using Inventory.API.Consumers;
using Inventory.API.Data;
using Inventory.API.Models;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts;

namespace Inventory.API.Tests;

public class OrderCreatedConsumerTests : IAsyncLifetime
{
    private readonly InventoryDbContext _db;
    private readonly Mock<IPublishEndpoint> _publishMock;
    private readonly Mock<ILogger<OrderCreatedConsumer>> _loggerMock;
    private readonly OrderCreatedConsumer _consumer;

    public OrderCreatedConsumerTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new InventoryDbContext(options);
        _publishMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();

        _consumer = new OrderCreatedConsumer(_db, _publishMock.Object, _loggerMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _db.DisposeAsync();

    // ── Helpers ───────────────────────────────────────────

    private async Task<StockItem> AddStockItem(Guid partId, string articleNumber, int quantity)
    {
        var item = new StockItem
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            ArticleNumber = articleNumber,
            PartName = "Test Part",
            Quantity = quantity,
            Reserved = 0
        };
        _db.StockItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private static OrderCreated CreateOrderMessage(Guid partId, string articleNumber, int quantity)
    {
        return new OrderCreated(
            OrderId: Guid.NewGuid(),
            DealerId: Guid.NewGuid(),
            Items: new List<OrderItemDto>
            {
                new(partId, articleNumber, quantity, 100m)
            },
            TotalAmount: quantity * 100m,
            CreatedAt: DateTime.UtcNow
        );
    }

    // ── Тесты ─────────────────────────────────────────────

    [Fact]
    public async Task Consume_WhenStockSufficient_ReservesStockAndPublishesConfirmed()
    {
        // Arrange
        var partId = Guid.NewGuid();
        await AddStockItem(partId, "ART-001", quantity: 10);

        var message = CreateOrderMessage(partId, "ART-001", quantity: 3);
        var context = Mock.Of<ConsumeContext<OrderCreated>>(c => c.Message == message);

        // Act
        await _consumer.Consume(context);

        // Assert — товар зарезервирован
        var stock = await _db.StockItems.FirstAsync(s => s.PartId == partId);
        stock.Reserved.Should().Be(3);
        stock.Available.Should().Be(7);

        // Опубликовано подтверждение
        _publishMock.Verify(
            p => p.Publish(
                It.Is<OrderConfirmed>(e => e.OrderId == message.OrderId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Отказ не публиковался
        _publishMock.Verify(
            p => p.Publish(It.IsAny<OrderRejected>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_WhenStockInsufficient_PublishesRejected()
    {
        // Arrange — на складе 2, заказывают 5
        var partId = Guid.NewGuid();
        await AddStockItem(partId, "ART-002", quantity: 2);

        var message = CreateOrderMessage(partId, "ART-002", quantity: 5);
        var context = Mock.Of<ConsumeContext<OrderCreated>>(c => c.Message == message);

        // Act
        await _consumer.Consume(context);

        // Assert — резерв не изменился
        var stock = await _db.StockItems.FirstAsync(s => s.PartId == partId);
        stock.Reserved.Should().Be(0);

        // Опубликован отказ
        _publishMock.Verify(
            p => p.Publish(
                It.Is<OrderRejected>(e => e.OrderId == message.OrderId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Подтверждение не публиковалось
        _publishMock.Verify(
            p => p.Publish(It.IsAny<OrderConfirmed>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_WhenPartNotFoundInStock_PublishesRejected()
    {
        // Arrange — товара вообще нет на складе
        var message = CreateOrderMessage(Guid.NewGuid(), "ART-MISSING", quantity: 1);
        var context = Mock.Of<ConsumeContext<OrderCreated>>(c => c.Message == message);

        // Act
        await _consumer.Consume(context);

        // Assert
        _publishMock.Verify(
            p => p.Publish(
                It.Is<OrderRejected>(e =>
                    e.OrderId == message.OrderId &&
                    e.Reason.Contains("not found")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WhenExactStockAvailable_ReservesSuccessfully()
    {
        // Arrange — заказывают ровно столько сколько есть
        var partId = Guid.NewGuid();
        await AddStockItem(partId, "ART-003", quantity: 5);

        var message = CreateOrderMessage(partId, "ART-003", quantity: 5);
        var context = Mock.Of<ConsumeContext<OrderCreated>>(c => c.Message == message);

        // Act
        await _consumer.Consume(context);

        // Assert
        var stock = await _db.StockItems.FirstAsync(s => s.PartId == partId);
        stock.Reserved.Should().Be(5);
        stock.Available.Should().Be(0);

        _publishMock.Verify(
            p => p.Publish(It.IsAny<OrderConfirmed>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_MultipleItems_AllReservedOrAllRejected()
    {
        // Arrange — заказ из двух позиций, вторая не влезает
        var partId1 = Guid.NewGuid();
        var partId2 = Guid.NewGuid();

        await AddStockItem(partId1, "ART-A", quantity: 10);
        await AddStockItem(partId2, "ART-B", quantity: 1); // мало

        var message = new OrderCreated(
            OrderId: Guid.NewGuid(),
            DealerId: Guid.NewGuid(),
            Items: new List<OrderItemDto>
            {
                new(partId1, "ART-A", 3, 100m),
                new(partId2, "ART-B", 5, 200m) // запрашиваем 5, есть только 1
            },
            TotalAmount: 1300m,
            CreatedAt: DateTime.UtcNow
        );

        var context = Mock.Of<ConsumeContext<OrderCreated>>(c => c.Message == message);

        // Act
        await _consumer.Consume(context);

        // Assert — весь заказ отклонён, резервы не тронуты
        var stock1 = await _db.StockItems.FirstAsync(s => s.PartId == partId1);
        var stock2 = await _db.StockItems.FirstAsync(s => s.PartId == partId2);

        stock1.Reserved.Should().Be(0);
        stock2.Reserved.Should().Be(0);

        _publishMock.Verify(
            p => p.Publish(It.IsAny<OrderRejected>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}