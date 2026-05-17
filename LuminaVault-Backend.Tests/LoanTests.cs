using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LuminaVault.Tests;

/// Loan amortisation + endpoint coverage. We don't unit-test the math directly here
/// — the schedule shape is exercised end-to-end via the schedule endpoint, which is
/// the same path the UI hits and gives us the highest-confidence regression check.
public class LoanTests : IClassFixture<LuminaVaultFactory>
{
    private readonly ApiClient _api;

    public LoanTests(LuminaVaultFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    private record LoanDto(
        int Id, string Name, string? Lender, decimal Principal,
        [property: JsonPropertyName("annualInterestRate")] decimal Rate,
        [property: JsonPropertyName("termMonths")] int Term,
        decimal MonthlyPayment, decimal CurrentBalance, decimal TotalInterest,
        [property: JsonPropertyName("paymentsMade")] int PaymentsMade,
        [property: JsonPropertyName("payoffDate")] DateTime PayoffDate);

    private record ScheduleRowDto(
        [property: JsonPropertyName("paymentNumber")] int Number,
        [property: JsonPropertyName("paymentDate")] DateTime Date,
        decimal Payment, decimal Principal, decimal Interest,
        decimal ExtraPrincipal, decimal Balance);

    private record ScheduleDto(
        int LoanId, string Name, decimal MonthlyPayment,
        decimal TotalPayments, decimal TotalInterest, decimal TotalPrincipal,
        int PaymentsMade, DateTime PayoffDate, decimal CurrentBalance,
        ScheduleRowDto[] Rows);

    [Fact]
    public async Task Standard_mortgage_amortisation_matches_textbook_formula()
    {
        var loan = await CreateLoan(new
        {
            name = "House",
            principal = 300000m,
            annualInterestRate = 4.5m,
            termMonths = 360,
            startDate = "2026-01-15",
            extraMonthlyPayment = 0m,
        });
        // Textbook value for P=300000, r=0.045/12, n=360 is ~1520.06.
        Assert.InRange(loan.MonthlyPayment, 1520m, 1521m);

        var schedule = await GetSchedule(loan.Id);
        Assert.Equal(360, schedule.Rows.Length);
        // Total interest paid over 30 years should be well above the principal —
        // roughly $247k for these inputs. Looser bounds keep the test robust to rounding.
        Assert.InRange(schedule.TotalInterest, 245000m, 250000m);
        Assert.Equal(0m, schedule.Rows[^1].Balance);
        // The first payment is mostly interest, the last is mostly principal.
        Assert.True(schedule.Rows[0].Interest > schedule.Rows[0].Principal);
        Assert.True(schedule.Rows[^1].Principal > schedule.Rows[^1].Interest);
    }

    [Fact]
    public async Task Zero_interest_loan_amortises_to_flat_payments()
    {
        var loan = await CreateLoan(new
        {
            name = "Interest-free",
            principal = 12000m,
            annualInterestRate = 0m,
            termMonths = 24,
            startDate = "2026-01-01",
            extraMonthlyPayment = 0m,
        });
        Assert.Equal(500m, loan.MonthlyPayment);
        var schedule = await GetSchedule(loan.Id);
        Assert.Equal(24, schedule.Rows.Length);
        Assert.All(schedule.Rows, r => Assert.Equal(0m, r.Interest));
        Assert.Equal(0m, schedule.TotalInterest);
        Assert.Equal(0m, schedule.Rows[^1].Balance);
    }

    [Fact]
    public async Task Extra_monthly_payment_shortens_term_and_saves_interest()
    {
        var baseline = await CreateLoan(new
        {
            name = "Baseline",
            principal = 200000m,
            annualInterestRate = 5m,
            termMonths = 360,
            startDate = "2026-01-01",
            extraMonthlyPayment = 0m,
        });
        var aggressive = await CreateLoan(new
        {
            name = "Aggressive",
            principal = 200000m,
            annualInterestRate = 5m,
            termMonths = 360,
            startDate = "2026-01-01",
            extraMonthlyPayment = 500m,
        });
        var baselineSchedule = await GetSchedule(baseline.Id);
        var aggressiveSchedule = await GetSchedule(aggressive.Id);

        Assert.True(aggressiveSchedule.Rows.Length < baselineSchedule.Rows.Length,
            "extra payments should shorten the term");
        Assert.True(aggressiveSchedule.TotalInterest < baselineSchedule.TotalInterest,
            "extra payments should save interest");
    }

    [Fact]
    public async Task Rejects_invalid_inputs()
    {
        await _api.EnsureAuthedAsync();
        var negative = await _api.PostAsync("/api/finance/loans/", new
        {
            name = "Bad",
            principal = -10m,
            annualInterestRate = 5m,
            termMonths = 12,
            startDate = "2026-01-01",
            extraMonthlyPayment = 0m,
            currency = "CHF",
            status = "Active",
        });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);

        var noTerm = await _api.PostAsync("/api/finance/loans/", new
        {
            name = "Bad term",
            principal = 1000m,
            annualInterestRate = 5m,
            termMonths = 0,
            startDate = "2026-01-01",
            extraMonthlyPayment = 0m,
            currency = "CHF",
            status = "Active",
        });
        Assert.Equal(HttpStatusCode.BadRequest, noTerm.StatusCode);
    }

    async Task<LoanDto> CreateLoan(object input)
    {
        var full = MergeDefaults(input);
        var resp = await _api.PostAsync("/api/finance/loans/", full);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<LoanDto>())!;
    }

    async Task<ScheduleDto> GetSchedule(int id)
    {
        await _api.EnsureAuthedAsync();
        var resp = await _api.Raw.GetAsync($"/api/finance/loans/{id}/schedule");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ScheduleDto>())!;
    }

    static Dictionary<string, object?> MergeDefaults(object input)
    {
        var dict = new Dictionary<string, object?>
        {
            ["lender"] = null,
            ["accountId"] = null,
            ["currency"] = "CHF",
            ["status"] = "Active",
            ["notes"] = null,
        };
        foreach (var prop in input.GetType().GetProperties())
            dict[prop.Name] = prop.GetValue(input);
        return dict;
    }
}
