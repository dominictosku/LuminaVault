# 📦 Data Management, Import/Export & Backups

LuminaVault ensures you retain complete ownership of your data with transparent spreadsheets, statement importers, and full-fidelity backup snapshots.

---

## 1. ODS Spreadsheet Import & Export

Round-trip your inventory and financial records with LibreOffice Calc or Microsoft Excel using the OpenDocument Spreadsheet (`.ods`) format.

### Supported Sheets
- **Accounts**: Account metadata, balances, institution, and currencies.
- **Transactions**: Full history including inline multi-category splits (`Category=Amount[|Notes];...`).
- **Holdings**: Symbol positions, average cost basis, and price metadata.
- **Monthly Summaries**: Opening/closing balances and reconciliation notes.
- **Budgets**: Category budget limits per month.
- **Balance Snapshots**: Recorded account reconciliation snapshots.
- **Subscriptions**: Billing intervals, amounts, providers, and auto-renew flags.
- **Assets (Inventory)**: Catalog of items, categories, brands, serial numbers, valuation, tags, and lithium battery tracking parameters.

### Import Workflow
1. Navigate to **Data → Import ODS**.
2. Upload an `.ods` spreadsheet file.
3. Review the parsed entities and preview warnings before committing changes to the database.

---

## 2. Bank CSV Statement Import

Import bank statement exports with deduplication:

- **Amount Formats**: Supports single signed `Amount` columns (negative for expenses, positive for income) or separate `Debit` and `Credit` columns.
- **Number Formats**: Automatically handles common European and international decimal styles (`42.50`, `42,50`, `1'234.50`).
- **Date Formats**: Parses `yyyy-MM-dd`, `dd.MM.yyyy`, `dd/MM/yyyy`, and variations.
- **Deduplication Engine**: Compares date, kind, payee, and rounded amount against existing account history to automatically skip duplicate imports.
- **Category Auto-Assignment**: Applies configured Category Rules to unassigned transactions upon import.

---

## 3. Backups & Disaster Recovery

Because LuminaVault is self-hosted, all storage resides locally under `data/`:
- `data/luminavault.db` (SQLite database)
- `data/uploads/` (Photos, 3D glTF models, document attachments)
- `data/logs/` (Application logs)
- `data/backups/` (Generated ZIP snapshots)

### Manual Snapshot Download
Trigger an authenticated snapshot via API:
```bash
curl -sH "Authorization: Bearer $LUMINA_TOKEN" \
  http://localhost:5256/api/data/backup \
  -o "luminavault-backup-$(date +%Y%m%d).zip"
```
The downloaded ZIP contains a consistent copy of the database and all files from `uploads/`.

### Scheduled Automatic Backups
Configure periodic snapshots directly in backend configuration:

```jsonc
// LuminaVault-Backend/appsettings.json
{
  "Backup": {
    "Enabled": true,
    "Directory": "backups",
    "IntervalHours": 24,
    "RetainedFiles": 14
  }
}
```

### Restoration Procedure
1. Stop the backend process / container.
2. Clear the target `luminavault.db` and `uploads/` directory.
3. Unzip the backup file into the data directory (`LUMINA_DATA_DIR` in Docker).
4. Start the backend. Database migrations will automatically run and verify schema consistency.
