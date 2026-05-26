namespace Catalog.API.Models;

public class Part
{
    public Guid Id { get; set; }
    public string ArticleNumber { get; set; } = string.Empty; // артикул
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Совместимость с автомобилями
    public ICollection<PartCompatibility> Compatibilities { get; set; } = new List<PartCompatibility>();
}

public class PartCompatibility
{
    public Guid Id { get; set; }
    public Guid PartId { get; set; }
    public Part Part { get; set; } = null!;
    public string CarBrand { get; set; } = string.Empty;  // Toyota
    public string CarModel { get; set; } = string.Empty;  // Camry
    public int YearFrom { get; set; }
    public int YearTo { get; set; }
}