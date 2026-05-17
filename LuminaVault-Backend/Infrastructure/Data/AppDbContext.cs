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
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<FinanceCategory> FinanceCategories => Set<FinanceCategory>();
    public DbSet<FinanceCategoryRule> FinanceCategoryRules => Set<FinanceCategoryRule>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();
    public DbSet<FinanceTransaction> FinanceTransactions => Set<FinanceTransaction>();
    public DbSet<MonthlyAccountSummary> MonthlyAccountSummaries => Set<MonthlyAccountSummary>();
    public DbSet<FinanceBudget> FinanceBudgets => Set<FinanceBudget>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();
    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<NetWorthSnapshot> NetWorthSnapshots => Set<NetWorthSnapshot>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TransactionSplit> TransactionSplits => Set<TransactionSplit>();
    public DbSet<Loan> Loans => Set<Loan>();

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

        b.Entity<DocumentAttachment>()
            .HasOne(a => a.Item)
            .WithMany(i => i.Attachments)
            .HasForeignKey(a => a.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<DocumentAttachment>()
            .HasOne(a => a.Subscription)
            .WithMany(s => s.Attachments)
            .HasForeignKey(a => a.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Item>().Property(i => i.Value).HasColumnType("decimal(18,2)");
        b.Entity<AssetCategory>().HasIndex(c => c.Name).IsUnique();
        b.Entity<FinanceCategory>().HasIndex(c => c.Name).IsUnique();
        b.Entity<FinanceCategoryRule>().HasIndex(r => r.Pattern);
        b.Entity<FinanceCategoryRule>().HasIndex(r => r.Priority);
        b.Entity<ExchangeRate>().HasIndex(r => new { r.Currency, r.EffectiveDate }).IsUnique();
        b.Entity<ExchangeRate>().Property(r => r.RateToBase).HasColumnType("decimal(18,8)");

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
        b.Entity<FinanceTransaction>().Property(t => t.Quantity).HasColumnType("decimal(28,8)");
        b.Entity<FinanceTransaction>().Property(t => t.PricePerUnit).HasColumnType("decimal(18,8)");
        b.Entity<FinanceTransaction>().HasIndex(t => t.OccurredOn);
        b.Entity<FinanceTransaction>().HasIndex(t => t.Category);
        b.Entity<FinanceTransaction>().HasIndex(t => t.Symbol);

        b.Entity<TransactionSplit>()
            .HasOne(s => s.Transaction)
            .WithMany(t => t.Splits)
            .HasForeignKey(s => s.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<TransactionSplit>().Property(s => s.Amount).HasColumnType("decimal(18,2)");
        b.Entity<TransactionSplit>().HasIndex(s => s.TransactionId);
        b.Entity<TransactionSplit>().HasIndex(s => s.Category);

        b.Entity<Holding>()
            .HasOne(h => h.Account)
            .WithMany(a => a.Holdings)
            .HasForeignKey(h => h.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Holding>().Property(h => h.Quantity).HasColumnType("decimal(28,8)");
        b.Entity<Holding>().Property(h => h.AverageCost).HasColumnType("decimal(18,8)");
        b.Entity<Holding>().Property(h => h.LastPrice).HasColumnType("decimal(18,8)");
        b.Entity<Holding>().HasIndex(h => new { h.AccountId, h.Symbol }).IsUnique();

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

        b.Entity<FinanceBudget>().Property(x => x.LimitAmount).HasColumnType("decimal(18,2)");
        b.Entity<FinanceBudget>().HasIndex(x => new { x.Category, x.Month }).IsUnique();

        b.Entity<SavingsGoal>()
            .HasOne(g => g.Account)
            .WithMany()
            .HasForeignKey(g => g.AccountId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<SavingsGoal>().Property(g => g.TargetAmount).HasColumnType("decimal(18,2)");
        b.Entity<SavingsGoal>().Property(g => g.CurrentAmount).HasColumnType("decimal(18,2)");
        b.Entity<SavingsGoal>().HasIndex(g => g.Status);

        b.Entity<AccountBalanceSnapshot>()
            .HasOne(s => s.Account)
            .WithMany(a => a.BalanceSnapshots)
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<AccountBalanceSnapshot>().Property(s => s.ActualBalance).HasColumnType("decimal(18,2)");
        b.Entity<AccountBalanceSnapshot>().Property(s => s.ExpectedBalance).HasColumnType("decimal(18,2)");
        b.Entity<AccountBalanceSnapshot>().Property(s => s.Difference).HasColumnType("decimal(18,2)");
        b.Entity<AccountBalanceSnapshot>().HasIndex(s => new { s.AccountId, s.SnapshotDate }).IsUnique();

        b.Entity<Subscription>()
            .HasOne(s => s.Account)
            .WithMany(a => a.Subscriptions)
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<Subscription>().Property(s => s.Amount).HasColumnType("decimal(18,2)");
        b.Entity<Subscription>().HasIndex(s => s.NextDueOn);

        b.Entity<NetWorthSnapshot>().Property(s => s.AccountNetWorth).HasColumnType("decimal(18,2)");
        b.Entity<NetWorthSnapshot>().Property(s => s.HoldingsMarketValue).HasColumnType("decimal(18,2)");
        b.Entity<NetWorthSnapshot>().Property(s => s.HoldingsCostBasis).HasColumnType("decimal(18,2)");
        b.Entity<NetWorthSnapshot>().Property(s => s.InventoryValue).HasColumnType("decimal(18,2)");
        b.Entity<NetWorthSnapshot>().Property(s => s.NetWorth).HasColumnType("decimal(18,2)");
        b.Entity<NetWorthSnapshot>().HasIndex(s => s.SnapshotDate).IsUnique();

        b.Entity<Notification>().HasIndex(n => n.Source).IsUnique();
        b.Entity<Notification>().HasIndex(n => n.Status);
        b.Entity<Notification>().HasIndex(n => n.CreatedAt);

        b.Entity<Loan>()
            .HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<Loan>().Property(l => l.Principal).HasColumnType("decimal(18,2)");
        b.Entity<Loan>().Property(l => l.AnnualInterestRate).HasColumnType("decimal(8,4)");
        b.Entity<Loan>().Property(l => l.ExtraMonthlyPayment).HasColumnType("decimal(18,2)");
        b.Entity<Loan>().HasIndex(l => l.Status);
    }
}
