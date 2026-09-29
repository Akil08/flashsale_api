using FlashSale.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    
    // Explicitly point to the Models namespace to prevent class/namespace collision
    public DbSet<Models.FlashSale> FlashSales { get; set; }
    
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Models.FlashSale>()
            .Property(f => f.RowVersion)
            .IsRowVersion(); // Maps to PostgreSQL's hidden 'xmin' column

        modelBuilder.Entity<Purchase>()
            .HasIndex(p => new { p.BuyerId, p.FlashSaleId })
            .IsUnique()
            .HasDatabaseName("IX_Purchases_BuyerId_FlashSaleId");

        modelBuilder.Entity<IdempotencyRecord>()
            .HasKey(i => i.Key);
            
        modelBuilder.Entity<IdempotencyRecord>()
            .HasIndex(i => new { i.Key, i.UserId })
            .IsUnique();
    }
}