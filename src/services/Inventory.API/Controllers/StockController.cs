using Inventory.API.Data;
using Inventory.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly InventoryDbContext _db;
    private readonly ILogger<StockController> _logger;

    public StockController(InventoryDbContext db, ILogger<StockController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var items = await _db.StockItems
            .OrderBy(s => s.ArticleNumber)
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{partId:guid}")]
    public async Task<IActionResult> GetByPartId(Guid partId, CancellationToken ct)
    {
        var item = await _db.StockItems
            .FirstOrDefaultAsync(s => s.PartId == partId, ct);

        if (item is null)
            return NotFound();

        return Ok(item);
    }

    // Добавить товар на склад
    [HttpPost]
    public async Task<IActionResult> AddStock([FromBody] AddStockDto dto, CancellationToken ct)
    {
        var existing = await _db.StockItems
            .FirstOrDefaultAsync(s => s.PartId == dto.PartId, ct);

        if (existing is not null)
        {
            // Уже есть — просто увеличиваем количество
            existing.Quantity += dto.Quantity;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Stock updated for {ArticleNumber}: +{Quantity}", existing.ArticleNumber, dto.Quantity);
            return Ok(existing);
        }

        // Новая позиция
        var item = new StockItem
        {
            Id = Guid.NewGuid(),
            PartId = dto.PartId,
            ArticleNumber = dto.ArticleNumber,
            PartName = dto.PartName,
            Quantity = dto.Quantity,
            Reserved = 0
        };

        _db.StockItems.Add(item);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Stock added for {ArticleNumber}: {Quantity} units", item.ArticleNumber, item.Quantity);
        return CreatedAtAction(nameof(GetByPartId), new { partId = item.PartId }, item);
    }

    // Скорректировать количество вручную
    [HttpPatch("{partId:guid}/adjust")]
    public async Task<IActionResult> Adjust(Guid partId, [FromBody] AdjustStockDto dto, CancellationToken ct)
    {
        var item = await _db.StockItems
            .FirstOrDefaultAsync(s => s.PartId == partId, ct);

        if (item is null)
            return NotFound();

        if (item.Quantity + dto.Delta < item.Reserved)
            return BadRequest($"Cannot reduce stock below reserved amount ({item.Reserved})");

        item.Quantity += dto.Delta;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Stock adjusted for {ArticleNumber}: delta {Delta}, new quantity {Quantity}",
            item.ArticleNumber, dto.Delta, item.Quantity);

        return Ok(item);
    }
}

public record AddStockDto(
    Guid PartId,
    string ArticleNumber,
    string PartName,
    int Quantity
);

// Delta может быть отрицательным — для уменьшения
public record AdjustStockDto(int Delta);