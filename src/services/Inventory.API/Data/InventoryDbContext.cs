using Inventory.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<StockItem> StockItems => Set<StockItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.ArticleNumber).HasMaxLength(50).IsRequired();
            entity.Property(s => s.PartName).HasMaxLength(200).IsRequired();
            entity.HasIndex(s => s.PartId).IsUnique();
            entity.Ignore(s => s.Available); // вычисляемое поле
        });
    }
}