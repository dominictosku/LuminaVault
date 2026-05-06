using LuminaVault.Domain;
using Microsoft.EntityFrameworkCore;

namespace LuminaVault.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<House> Houses => Set<House>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Furniture> Furniture => Set<Furniture>();
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemPhoto> ItemPhotos => Set<ItemPhoto>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();

        b.Entity<Room>()
            .HasOne(r => r.House)
            .WithMany(h => h.Rooms)
            .HasForeignKey(r => r.HouseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Furniture>()
            .HasOne(f => f.Room)
            .WithMany(r => r.Furniture)
            .HasForeignKey(f => f.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Container>()
            .HasOne(c => c.Furniture)
            .WithMany(f => f.Containers)
            .HasForeignKey(c => c.FurnitureId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Item>()
            .HasOne(i => i.Furniture)
            .WithMany(f => f.Items)
            .HasForeignKey(i => i.FurnitureId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<Item>()
            .HasOne(i => i.Container)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.ContainerId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<ItemPhoto>()
            .HasOne(p => p.Item)
            .WithMany(i => i.Photos)
            .HasForeignKey(p => p.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Item>().Property(i => i.Value).HasColumnType("decimal(18,2)");
    }
}
