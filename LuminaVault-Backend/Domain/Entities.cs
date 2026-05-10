using System.ComponentModel.DataAnnotations;

namespace LuminaVault.Domain;

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(64)] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class House
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Room> Rooms { get; set; } = new();
}

public class Room
{
    public int Id { get; set; }
    public int HouseId { get; set; }
    public House? House { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string Color { get; set; } = "#7c3aed";

    // 3D placement (meters). Rooms are axis-aligned boxes on a floor plan.
    public double X { get; set; }
    public double Z { get; set; }
    public double Width { get; set; } = 4;
    public double Depth { get; set; } = 4;
    public double Height { get; set; } = 2.6;

    public List<Furniture> Furniture { get; set; } = new();
}

public enum FurnitureKind
{
    Cabinet, Drawer, Shelf, Wardrobe, Desk, Table, Sofa, Bed, Box, Other
}

public class Furniture
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public FurnitureKind Kind { get; set; } = FurnitureKind.Cabinet;

    // Local position inside the room (0..Width / 0..Depth)
    public double X { get; set; }
    public double Z { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 0.8;
    public double Depth { get; set; } = 0.5;
    public double Height { get; set; } = 1.2;
    public double RotationY { get; set; }

    public List<Container> Containers { get; set; } = new();
    public List<Item> Items { get; set; } = new();
}

public class Container
{
    public int Id { get; set; }
    public int FurnitureId { get; set; }
    public Furniture? Furniture { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<Item> Items { get; set; } = new();
}

public class Item
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [MaxLength(80)] public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? Value { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? WarrantyUntil { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public string TagsCsv { get; set; } = "";

    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    public int? FurnitureId { get; set; }
    public Furniture? Furniture { get; set; }
    public int? ContainerId { get; set; }
    public Container? Container { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? ModelFileName { get; set; }
    public string? ModelContentType { get; set; }

    public List<ItemPhoto> Photos { get; set; } = new();
    public List<DocumentAttachment> Attachments { get; set; } = new();
}

public class ItemPhoto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    [Required] public string FileName { get; set; } = "";
    [Required] public string ContentType { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class DocumentAttachment
{
    public int Id { get; set; }
    public int? ItemId { get; set; }
    public Item? Item { get; set; }
    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    [Required, MaxLength(220)] public string OriginalFileName { get; set; } = "";
    [Required] public string FileName { get; set; } = "";
    [Required, MaxLength(120)] public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class AssetCategory
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(24)] public string Color { get; set; } = "#7c3aed";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class FinanceCategory
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(24)] public string Color { get; set; } = "#7c3aed";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum FinanceAccountType
{
    Checking, Savings, Cash, CreditCard, Investment, Crypto, Loan, Other
}

public enum FinanceTransactionKind
{
    Income, Expense, Transfer
}

public enum FinanceTransactionStatus
{
    Pending, Cleared, Reconciled
}

public enum SubscriptionStatus
{
    Active, Paused, Cancelled
}

public class FinanceAccount
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(120)] public string? Institution { get; set; }
    public FinanceAccountType Type { get; set; } = FinanceAccountType.Checking;
    [Required, MaxLength(8)] public string Currency { get; set; } = "CHF";
    public decimal StartingBalance { get; set; }
    public decimal Balance { get; set; }
    [MaxLength(24)] public string Color { get; set; } = "#14b8a6";
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<FinanceTransaction> Transactions { get; set; } = new();
    public List<Subscription> Subscriptions { get; set; } = new();
    public List<AccountBalanceSnapshot> BalanceSnapshots { get; set; } = new();
}

public class FinanceTransaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    public int? TransferAccountId { get; set; }
    public FinanceAccount? TransferAccount { get; set; }

    public FinanceTransactionKind Kind { get; set; } = FinanceTransactionKind.Expense;
    public FinanceTransactionStatus Status { get; set; } = FinanceTransactionStatus.Cleared;
    public DateTime OccurredOn { get; set; } = DateTime.UtcNow.Date;
    [Required, MaxLength(140)] public string Payee { get; set; } = "";
    [Required, MaxLength(80)] public string Category { get; set; } = "General";
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string TagsCsv { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class MonthlyAccountSummary
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    public DateTime Month { get; set; } = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class FinanceBudget
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Category { get; set; } = "General";
    public DateTime Month { get; set; } = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    public decimal LimitAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AccountBalanceSnapshot
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow.Date;
    public decimal ActualBalance { get; set; }
    public decimal ExpectedBalance { get; set; }
    public decimal Difference { get; set; }
    public bool IsReconciled { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Subscription
{
    public int Id { get; set; }
    [Required, MaxLength(140)] public string Name { get; set; } = "";
    [Required, MaxLength(80)] public string Category { get; set; } = "Subscriptions";
    [MaxLength(120)] public string? Provider { get; set; }
    public int? AccountId { get; set; }
    public FinanceAccount? Account { get; set; }
    public decimal Amount { get; set; }
    [Required, MaxLength(8)] public string Currency { get; set; } = "CHF";
    public int BillingIntervalDays { get; set; } = 30;
    public DateTime StartedOn { get; set; } = DateTime.UtcNow.Date;
    public DateTime NextDueOn { get; set; } = DateTime.UtcNow.Date;
    public bool AutoRenew { get; set; } = true;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DocumentAttachment> Attachments { get; set; } = new();
}
