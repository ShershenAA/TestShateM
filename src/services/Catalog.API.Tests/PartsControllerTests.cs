using Catalog.API.Controllers;
using Catalog.API.Data;
using Catalog.API.Models;
using Catalog.API.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Catalog.API.Tests;

public class PartsControllerTests
{
    private readonly CatalogDbContext _db;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<ISearchService> _searchMock;
    private readonly Mock<ILogger<PartsController>> _loggerMock;
    private readonly PartsController _controller;

    public PartsControllerTests()
    {
        // InMemory БД — не нужен реальный PostgreSQL
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new CatalogDbContext(options);
        _cacheMock = new Mock<ICacheService>();
        _searchMock = new Mock<ISearchService>();
        _loggerMock = new Mock<ILogger<PartsController>>();

        _controller = new PartsController(_db, _cacheMock.Object, _searchMock.Object, _loggerMock.Object);
    }

    // ── GetAll ────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WhenCacheHit_ReturnsCachedParts()
    {
        // Arrange
        var cachedParts = new List<Part>
        {
            new() { Id = Guid.NewGuid(), ArticleNumber = "ART-001", Name = "Brake Pad", Brand = "Bosch", Category = "Brakes", Price = 100 }
        };

        _cacheMock
            .Setup(c => c.GetAsync<List<Part>>("parts:all"))
            .ReturnsAsync(cachedParts);

        // Act
        var result = await _controller.GetAll(CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var parts = ok.Value.Should().BeAssignableTo<List<Part>>().Subject;
        parts.Should().HaveCount(1);
        parts[0].ArticleNumber.Should().Be("ART-001");

        // БД не должна вызываться при cache hit
        _cacheMock.Verify(c => c.GetAsync<List<Part>>("parts:all"), Times.Once);
    }

    [Fact]
    public async Task GetAll_WhenCacheMiss_ReturnsFromDb()
    {
        // Arrange — кэш пустой
        _cacheMock
            .Setup(c => c.GetAsync<List<Part>>("parts:all"))
            .ReturnsAsync((List<Part>?)null);

        // Добавляем данные в InMemory БД
        _db.Parts.AddRange(
            new Part { Id = Guid.NewGuid(), ArticleNumber = "ART-001", Name = "Brake Pad", Brand = "Bosch", Category = "Brakes", Price = 100, IsActive = true },
            new Part { Id = Guid.NewGuid(), ArticleNumber = "ART-002", Name = "Oil Filter", Brand = "Mann", Category = "Filters", Price = 50, IsActive = true },
            new Part { Id = Guid.NewGuid(), ArticleNumber = "ART-003", Name = "Old Part", Brand = "Unknown", Category = "Other", Price = 10, IsActive = false }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.GetAll(CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var parts = ok.Value.Should().BeAssignableTo<List<Part>>().Subject;

        // IsActive = false не должна попасть в результат
        parts.Should().HaveCount(2);
        parts.Should().AllSatisfy(p => p.IsActive.Should().BeTrue());

        // Должен записать в кэш
        _cacheMock.Verify(
            c => c.SetAsync("parts:all", It.IsAny<List<Part>>(), It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    // ── GetById ───────────────────────────────────────────

    [Fact]
    public async Task GetById_WhenExists_ReturnsPart()
    {
        // Arrange
        var partId = Guid.NewGuid();
        var part = new Part
        {
            Id = partId,
            ArticleNumber = "ART-001",
            Name = "Brake Pad",
            Brand = "Bosch",
            Category = "Brakes",
            Price = 100,
            IsActive = true
        };

        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        _cacheMock
            .Setup(c => c.GetAsync<Part>($"parts:{partId}"))
            .ReturnsAsync((Part?)null);

        // Act
        var result = await _controller.GetById(partId, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = ok.Value.Should().BeOfType<Part>().Subject;
        returned.Id.Should().Be(partId);
        returned.ArticleNumber.Should().Be("ART-001");
    }

    [Fact]
    public async Task GetById_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        _cacheMock
            .Setup(c => c.GetAsync<Part>(It.IsAny<string>()))
            .ReturnsAsync((Part?)null);

        // Act
        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── Create ────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidDto_ReturnsCreatedPart()
    {
        // Arrange
        var dto = new CreatePartDto(
            ArticleNumber: "ART-NEW",
            Name: "New Part",
            Description: "Test description",
            Brand: "TestBrand",
            Category: "TestCategory",
            Price: 299.99m
        );

        // Act
        var result = await _controller.Create(dto, CancellationToken.None);

        // Assert
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var part = created.Value.Should().BeOfType<Part>().Subject;
        part.ArticleNumber.Should().Be("ART-NEW");
        part.Price.Should().Be(299.99m);
        part.IsActive.Should().BeTrue();

        // Проверяем что реально сохранилось в БД
        var inDb = await _db.Parts.FindAsync(part.Id);
        inDb.Should().NotBeNull();
        inDb!.ArticleNumber.Should().Be("ART-NEW");

        // Кэш должен быть инвалидирован
        _cacheMock.Verify(c => c.RemoveAsync("parts:all"), Times.Once);

        // Elasticsearch должен проиндексировать
        _searchMock.Verify(s => s.IndexPartAsync(It.IsAny<Part>()), Times.Once);
    }

    // ── Delete (soft delete) ──────────────────────────────

    [Fact]
    public async Task Delete_ExistingPart_SetsIsActiveFalse()
    {
        // Arrange
        var part = new Part
        {
            Id = Guid.NewGuid(),
            ArticleNumber = "ART-DEL",
            Name = "To Delete",
            Brand = "Brand",
            Category = "Cat",
            Price = 50,
            IsActive = true
        };

        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.Delete(part.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        // Soft delete — запись остаётся но IsActive = false
        var inDb = await _db.Parts.FindAsync(part.Id);
        inDb.Should().NotBeNull();
        inDb!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NonExistingPart_ReturnsNotFound()
    {
        // Act
        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}