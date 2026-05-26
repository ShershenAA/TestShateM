using Catalog.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Part> Parts => Set<Part>();
    public DbSet<PartCompatibility> PartCompatibilities => Set<PartCompatibility>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Part>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.ArticleNumber).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Brand).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Category).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
            entity.HasIndex(p => p.ArticleNumber).IsUnique();
        });

        modelBuilder.Entity<PartCompatibility>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasOne(p => p.Part)
                .WithMany(p => p.Compatibilities)
                .HasForeignKey(p => p.PartId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}