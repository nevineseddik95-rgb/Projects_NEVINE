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
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

protected override void OnModelCreating(ModelBuilder b)
{
    base.OnModelCreating(b);
    b.Entity<AdminUser>(e =>
    {
        e.Property(u => u.Username).HasMaxLength(100);
        e.HasIndex(u => u.Username).IsUnique();
    });
}
}
