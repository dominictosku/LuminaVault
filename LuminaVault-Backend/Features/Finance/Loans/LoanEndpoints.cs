using LuminaVault.Data;
using LuminaVault.Domain;
using LuminaVault.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LuminaVault.Endpoints.FinanceHelpers;

namespace LuminaVault.Endpoints;

public record LoanDto(
    int Id, string Name, string? Lender, int? AccountId, string? AccountName,
    string Currency, decimal Principal, decimal AnnualInterestRate, int TermMonths,
    DateTime StartDate, decimal ExtraMonthlyPayment, decimal MonthlyPayment,
    decimal CurrentBalance, decimal TotalInterest, decimal TotalPayments,
    int PaymentsMade, DateTime PayoffDate, decimal NextPaymentDue,
    DateTime? NextPaymentDate, LoanStatus Status, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt);

public record LoanInput(
    string Name, string? Lender, int? AccountId, string Currency,
    decimal Principal, decimal AnnualInterestRate, int TermMonths,
    DateTime StartDate, decimal ExtraMonthlyPayment, LoanStatus Status, string? Notes);

public record LoanScheduleRowDto(
    int PaymentNumber, DateTime PaymentDate, decimal Payment,
    decimal Principal, decimal Interest, decimal ExtraPrincipal, decimal Balance);

public record LoanScheduleDto(
    int LoanId, string Name, decimal MonthlyPayment, decimal TotalPayments,
    decimal TotalInterest, decimal TotalPrincipal, int PaymentsMade,
    DateTime PayoffDate, decimal CurrentBalance, decimal NextPaymentDue,
    DateTime? NextPaymentDate, LoanScheduleRowDto[] Rows);

internal static class LoanEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var loans = app.MapGroup("/api/finance/loans").RequireAuthorization().WithTags("Finance");

        loans.MapGet("/", async (AppDbContext db, bool includeClosed = false) =>
        {
            var query = db.Loans.Include(l => l.Account).AsQueryable();
            if (!includeClosed)
                query = query.Where(l => l.Status == LoanStatus.Active);
            var result = await query
                .OrderBy(l => l.Status == LoanStatus.Active ? 0 : 1)
                .ThenBy(l => l.Name)
                .ToListAsync();
            var asOf = DateTime.UtcNow.Date;
            return Results.Ok(result.Select(l => MapLoan(l, asOf)));
        });

        loans.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var loan = await db.Loans.Include(l => l.Account).FirstOrDefaultAsync(l => l.Id == id);
            if (loan is null) return Results.NotFound();
            return Results.Ok(MapLoan(loan, DateTime.UtcNow.Date));
        });

        loans.MapGet("/{id:int}/schedule", async (int id, AppDbContext db) =>
        {
            var loan = await db.Loans.FirstOrDefaultAsync(l => l.Id == id);
            if (loan is null) return Results.NotFound();
            var schedule = LoanAmortization.Build(loan, DateTime.UtcNow.Date);
            return Results.Ok(new LoanScheduleDto(
                loan.Id, loan.Name, schedule.MonthlyPayment, schedule.TotalPayments,
                schedule.TotalInterest, schedule.TotalPrincipal, schedule.PaymentsMade,
                schedule.PayoffDate, schedule.CurrentBalance, schedule.NextPaymentDue,
                schedule.NextPaymentDate,
                schedule.Rows.Select(r => new LoanScheduleRowDto(
                    r.PaymentNumber, r.PaymentDate, r.Payment, r.Principal, r.Interest,
                    r.ExtraPrincipal, r.Balance)).ToArray()));
        });

        loans.MapPost("/", async ([FromBody] LoanInput input, AppDbContext db) =>
        {
            var validation = await ValidateLoan(input, db);
            if (validation is not null) return validation;
            var loan = new Loan();
            ApplyLoan(loan, input);
            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            await db.Entry(loan).Reference(l => l.Account).LoadAsync();
            return Results.Created($"/api/finance/loans/{loan.Id}", MapLoan(loan, DateTime.UtcNow.Date));
        });

        loans.MapPut("/{id:int}", async (int id, [FromBody] LoanInput input, AppDbContext db) =>
        {
            var loan = await db.Loans.Include(l => l.Account).FirstOrDefaultAsync(l => l.Id == id);
            if (loan is null) return Results.NotFound();
            var validation = await ValidateLoan(input, db);
            if (validation is not null) return validation;
            ApplyLoan(loan, input);
            loan.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await db.Entry(loan).Reference(l => l.Account).LoadAsync();
            return Results.Ok(MapLoan(loan, DateTime.UtcNow.Date));
        });

        loans.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var loan = await db.Loans.FindAsync(id);
            if (loan is null) return Results.NotFound();
            db.Loans.Remove(loan);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    static void ApplyLoan(Loan loan, LoanInput input)
    {
        loan.Name = input.Name.Trim();
        loan.Lender = Clean(input.Lender);
        loan.AccountId = input.AccountId;
        loan.Currency = (Clean(input.Currency) ?? "CHF").ToUpperInvariant();
        loan.Principal = Math.Abs(input.Principal);
        loan.AnnualInterestRate = Math.Max(0m, input.AnnualInterestRate);
        loan.TermMonths = Math.Max(1, input.TermMonths);
        loan.StartDate = input.StartDate.Date;
        loan.ExtraMonthlyPayment = Math.Max(0m, input.ExtraMonthlyPayment);
        loan.Status = input.Status;
        loan.Notes = Clean(input.Notes);
    }

    static async Task<IResult?> ValidateLoan(LoanInput input, AppDbContext db)
    {
        var basic = Validate.FirstFailure(
            Validate.Required(input.Name, "Loan name"),
            Validate.Positive(input.Principal, "Principal"),
            Validate.NonNegative(input.AnnualInterestRate, "Annual interest rate"),
            input.TermMonths <= 0 ? Problem.BadRequest("Term must be at least 1 month.") : null);
        if (basic is not null) return basic;
        if (input.AccountId is { } accountId)
            return await Validate.FinanceAccountExists(db, accountId);
        return null;
    }

    public static LoanDto MapLoan(Loan loan, DateTime asOf)
    {
        var schedule = LoanAmortization.Build(loan, asOf);
        return new LoanDto(
            loan.Id, loan.Name, loan.Lender, loan.AccountId, loan.Account?.Name,
            loan.Currency, loan.Principal, loan.AnnualInterestRate, loan.TermMonths,
            loan.StartDate, loan.ExtraMonthlyPayment, schedule.MonthlyPayment,
            schedule.CurrentBalance, schedule.TotalInterest, schedule.TotalPayments,
            schedule.PaymentsMade, schedule.PayoffDate, schedule.NextPaymentDue,
            schedule.NextPaymentDate, loan.Status, loan.Notes,
            loan.CreatedAt, loan.UpdatedAt);
    }
}
