using FlashSale.Api.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;


namespace FlashSale.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<FlashSale> FlashSales { get; set; }
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User constraints
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // FlashSale constraints
        modelBuilder.Entity<FlashSale>()
            .Property(f => f.RowVersion)
            .IsRowVersion(); // Maps to PostgreSQL's hidden 'xmin' column

        // Purchase constraints
        modelBuilder.Entity<Purchase>()
            .HasIndex(p => new { p.BuyerId, p.FlashSaleId })
            .IsUnique()
            .HasDatabaseName("IX_Purchases_BuyerId_FlashSaleId");

        // IdempotencyRecord constraints
        modelBuilder.Entity<IdempotencyRecord>()
            .HasKey(i => i.Key);
            
        modelBuilder.Entity<IdempotencyRecord>()
            .HasIndex(i => new { i.Key, i.UserId })
            .IsUnique();
    }
}
