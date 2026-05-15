using LuminaVault.Domain;

namespace LuminaVault.Data;

/// First-run seeds. Each block is idempotent (only inserts when the table is empty),
/// so calling this on every startup is safe.
public static class Seeder
{
    public static void Seed(AppDbContext db)
    {
        if (!db.AssetCategories.Any())
        {
            var defaults = new[]
            {
                "IT", "Hobby", "Furniture", "Tools", "Vehicle", "Office Supplies",
                "Clothing", "School", "Cleaning", "Homelab", "Other"
            };
            for (var i = 0; i < defaults.Length; i++)
                db.AssetCategories.Add(new AssetCategory { Name = defaults[i], SortOrder = i, Color = "#7c3aed" });
            db.SaveChanges();
        }

        if (!db.FinanceCategories.Any())
        {
            var defaults = new[]
            {
                "Salary", "Food", "Housing", "Transport", "Health", "Career", "Hobby",
                "Savings", "Investments", "Subscriptions", "Insurance", "Utilities",
                "Essentials", "Personal Care"
            };
            for (var i = 0; i < defaults.Length; i++)
                db.FinanceCategories.Add(new FinanceCategory { Name = defaults[i], SortOrder = i, Color = "#7c3aed" });
            db.SaveChanges();
        }
    }
}
