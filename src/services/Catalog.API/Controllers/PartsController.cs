using Catalog.API.Data;
using Catalog.API.Models;
using Catalog.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PartsController : ControllerBase
{
    private readonly CatalogDbContext _db;
    private readonly ICacheService _cache;
    private readonly ISearchService _search;
    private readonly ILogger<PartsController> _logger;
    
    private const string AllPartsCacheKey = "parts:all";
    private static string PartCacheKey(Guid id) => $"parts:{id}";

    public PartsController(CatalogDbContext db, ICacheService cache, ISearchService search, ILogger<PartsController> logger)
    {
        _db = db;
        _cache = cache;
        _search = search;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        // Сначала смотрим в кэш
        var cached = await _cache.GetAsync<List<Part>>(AllPartsCacheKey);
        if (cached is not null)
            return Ok(cached);
        
        var parts = await _db.Parts
            .Where(p => p.IsActive)
            .ToListAsync(ct);
        
        // Кладём в кэш на 5 минут
        await _cache.SetAsync(AllPartsCacheKey, parts, TimeSpan.FromMinutes(5));

        return Ok(parts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var cached = await _cache.GetAsync<Part>(PartCacheKey(id));
        if (cached is not null)
            return Ok(cached);
        
        var part = await _db.Parts
            .Include(p => p.Compatibilities)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (part is null)
            return NotFound();

        await _cache.SetAsync(PartCacheKey(id), part, TimeSpan.FromMinutes(10));
        
        return Ok(part);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePartDto dto, CancellationToken ct)
    {
        var part = new Part
        {
            Id = Guid.NewGuid(),
            ArticleNumber = dto.ArticleNumber,
            Name = dto.Name,
            Description = dto.Description,
            Brand = dto.Brand,
            Category = dto.Category,
            Price = dto.Price
        };

        _db.Parts.Add(part);
        await _db.SaveChangesAsync(ct);

        // Инвалидируем кэш списка — он устарел
        await _cache.RemoveAsync(AllPartsCacheKey);
        await _search.IndexPartAsync(part);  // ← индексируем в ES
        
        _logger.LogInformation("Part created: {ArticleNumber}", part.ArticleNumber);

        return CreatedAtAction(nameof(GetById), new { id = part.Id }, part);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreatePartDto dto, CancellationToken ct)
    {
        var part = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (part is null)
            return NotFound();

        part.Name = dto.Name;
        part.Description = dto.Description;
        part.Brand = dto.Brand;
        part.Category = dto.Category;
        part.Price = dto.Price;

        await _db.SaveChangesAsync(ct);
        
        // Инвалидируем оба ключа
        await _cache.RemoveAsync(PartCacheKey(id));
        await _cache.RemoveAsync(AllPartsCacheKey);

        return Ok(part);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var part = await _db.Parts.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (part is null)
            return NotFound();

        // Soft delete
        part.IsActive = false;
        await _db.SaveChangesAsync(ct);
        
        await _cache.RemoveAsync(PartCacheKey(id));
        await _cache.RemoveAsync(AllPartsCacheKey);

        return NoContent();
    }
    
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Search query cannot be empty");

        var results = await _search.SearchAsync(q);
        return Ok(results);
    }
    
    [HttpPost("reindex")]
    public async Task<IActionResult> Reindex(CancellationToken ct)
    {
        var parts = await _db.Parts
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        // foreach (var part in parts)
        //     await _search.IndexPartAsync(part);
        //
        // _logger.LogInformation("Reindexed {Count} parts", parts.Count);
        await _search.BulkIndexAsync(parts);
        return Ok(new { indexed = parts.Count });
    }
}

// DTO прямо здесь для простоты, потом вынесем
public record CreatePartDto(
    string ArticleNumber,
    string Name,
    string? Description,
    string Brand,
    string Category,
    decimal Price
);