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
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<FinanceCategory> FinanceCategories => Set<FinanceCategory>();
    public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();
    public DbSet<FinanceTransaction> FinanceTransactions => Set<FinanceTransaction>();
    public DbSet<MonthlyAccountSummary> MonthlyAccountSummaries => Set<MonthlyAccountSummary>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

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
            .HasOne(i => i.Room)
            .WithMany()
            .HasForeignKey(i => i.RoomId)
            .OnDelete(DeleteBehavior.SetNull);

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
        b.Entity<AssetCategory>().HasIndex(c => c.Name).IsUnique();
        b.Entity<FinanceCategory>().HasIndex(c => c.Name).IsUnique();

        b.Entity<FinanceAccount>().Property(a => a.StartingBalance).HasColumnType("decimal(18,2)");
        b.Entity<FinanceAccount>().Property(a => a.Balance).HasColumnType("decimal(18,2)");

        b.Entity<FinanceTransaction>()
            .HasOne(t => t.Account)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<FinanceTransaction>()
            .HasOne(t => t.TransferAccount)
            .WithMany()
            .HasForeignKey(t => t.TransferAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<FinanceTransaction>().Property(t => t.Amount).HasColumnType("decimal(18,2)");
        b.Entity<FinanceTransaction>().HasIndex(t => t.OccurredOn);
        b.Entity<FinanceTransaction>().HasIndex(t => t.Category);

        b.Entity<MonthlyAccountSummary>()
            .HasOne(s => s.Account)
            .WithMany()
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<MonthlyAccountSummary>().Property(s => s.Income).HasColumnType("decimal(18,2)");
        b.Entity<MonthlyAccountSummary>().Property(s => s.Expenses).HasColumnType("decimal(18,2)");
        b.Entity<MonthlyAccountSummary>().Property(s => s.OpeningBalance).HasColumnType("decimal(18,2)");
        b.Entity<MonthlyAccountSummary>().Property(s => s.ClosingBalance).HasColumnType("decimal(18,2)");
        b.Entity<MonthlyAccountSummary>().HasIndex(s => new { s.AccountId, s.Month }).IsUnique();

        b.Entity<Subscription>()
            .HasOne(s => s.Account)
            .WithMany(a => a.Subscriptions)
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<Subscription>().Property(s => s.Amount).HasColumnType("decimal(18,2)");
        b.Entity<Subscription>().HasIndex(s => s.NextDueOn);
    }
}
