using System;
using Microsoft.EntityFrameworkCore;
using ProductStore.Api.Models;
using ProductStore.Api.Endpoints;
using productStore.Api.Models;

namespace productStore.Api.Data;

public class ProductStoreContext(DbContextOptions<ProductStoreContext> options)
: DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Genre> Genres => Set<Genre>();

    public DbSet<Color> Colors => Set<Color>();
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Command> Commands => Set<Command>();
    public DbSet<CommandItem> CommandItems => Set<CommandItem>();
    public DbSet<StoredImage> StoredImages => Set<StoredImage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

protected override void OnModelCreating(ModelBuilder b)
{
    base.OnModelCreating(b);
    b.Entity<AdminUser>(e =>
    {
        e.Property(u => u.Username).HasMaxLength(100);
        e.HasIndex(u => u.Username).IsUnique();
    });
    b.Entity<Command>(e =>
    {
        e.HasIndex(c => c.ReferenceNumber).IsUnique();
        e.HasIndex(c => c.CreatedAt);
        e.HasIndex(c => c.CreatedByAdminId);
        e.Property(c => c.CommissionAmount).HasPrecision(18, 2);
        e.Property(c => c.CommissionPaidAmount).HasPrecision(18, 2);
        e.Property(c => c.CommissionPaymentNote).HasMaxLength(1000);
        e.HasOne<ProductStore.Api.Models.AdminUser>()
            .WithMany()
            .HasForeignKey(c => c.CreatedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(c => c.PaymentReference)
            .IsUnique()
            .HasFilter("\"PaymentReference\" IS NOT NULL AND \"PaymentReference\" <> ''");
        e.HasIndex(c => c.TrackingNumber)
            .IsUnique()
            .HasFilter("\"TrackingNumber\" IS NOT NULL AND \"TrackingNumber\" <> ''");
    });
    b.Entity<StoredImage>(e =>
    {
        e.Property(i => i.FileName).HasMaxLength(128);
        e.Property(i => i.ContentType).HasMaxLength(50);
        e.HasKey(i => i.FileName);
    });
}
}
