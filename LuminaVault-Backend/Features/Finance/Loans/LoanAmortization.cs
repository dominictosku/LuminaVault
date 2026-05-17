using LuminaVault.Domain;

namespace LuminaVault.Endpoints;

/// Pure, dependency-free amortisation: takes a Loan, returns its payment schedule
/// and the summary totals. Lives outside the endpoint so tests can call it directly
/// and the schedule UI shows the same numbers the persisted loan reports.
internal static class LoanAmortization
{
    public record ScheduleRow(
        int PaymentNumber,
        DateTime PaymentDate,
        decimal Payment,
        decimal Principal,
        decimal Interest,
        decimal ExtraPrincipal,
        decimal Balance);

    public record Schedule(
        decimal MonthlyPayment,
        decimal TotalPayments,
        decimal TotalInterest,
        decimal TotalPrincipal,
        int PaymentsMade,
        DateTime PayoffDate,
        decimal CurrentBalance,
        decimal NextPaymentDue,
        DateTime? NextPaymentDate,
        ScheduleRow[] Rows);

    public static Schedule Build(Loan loan, DateTime asOf)
    {
        var principal = loan.Principal;
        var termMonths = Math.Max(1, loan.TermMonths);
        var monthlyRate = loan.AnnualInterestRate <= 0
            ? 0m
            : loan.AnnualInterestRate / 100m / 12m;
        var basePayment = MonthlyPayment(principal, monthlyRate, termMonths);
        var extra = Math.Max(0m, loan.ExtraMonthlyPayment);

        var rows = new List<ScheduleRow>(termMonths);
        var balance = principal;
        var totalInterest = 0m;
        var totalPrincipal = 0m;
        var totalPayments = 0m;
        var date = loan.StartDate.Date;
        // Hard cap so a 0% / extra-payment combo can't loop forever (it won't, but be safe).
        for (var i = 1; i <= termMonths * 2 && balance > 0.005m; i++)
        {
            date = i == 1 ? FirstPaymentDate(loan.StartDate) : date.AddMonths(1);
            var interest = Math.Round(balance * monthlyRate, 2, MidpointRounding.AwayFromZero);
            var principalPortion = Math.Round(basePayment - interest, 2, MidpointRounding.AwayFromZero);
            var extraPortion = extra;
            // Final payment: don't overshoot.
            if (principalPortion + extraPortion > balance)
            {
                principalPortion = Math.Max(0m, balance - extraPortion);
                if (principalPortion + extraPortion > balance)
                {
                    extraPortion = balance - principalPortion;
                }
            }
            if (principalPortion < 0m) { extraPortion += principalPortion; principalPortion = 0m; }
            var payment = principalPortion + interest + extraPortion;
            balance = Math.Round(balance - principalPortion - extraPortion, 2, MidpointRounding.AwayFromZero);
            if (balance < 0.005m) balance = 0m;
            rows.Add(new ScheduleRow(i, date, payment, principalPortion, interest, extraPortion, balance));
            totalInterest += interest;
            totalPrincipal += principalPortion + extraPortion;
            totalPayments += payment;
        }

        var paymentsMade = rows.Count(r => r.PaymentDate.Date <= asOf.Date);
        var currentBalance = paymentsMade > 0 ? rows[paymentsMade - 1].Balance : principal;
        var nextRow = rows.Skip(paymentsMade).FirstOrDefault();
        var payoffDate = rows.Count > 0 ? rows[^1].PaymentDate : loan.StartDate;

        return new Schedule(
            MonthlyPayment: basePayment + extra,
            TotalPayments: totalPayments,
            TotalInterest: totalInterest,
            TotalPrincipal: totalPrincipal,
            PaymentsMade: paymentsMade,
            PayoffDate: payoffDate,
            CurrentBalance: currentBalance,
            NextPaymentDue: nextRow?.Payment ?? 0m,
            NextPaymentDate: nextRow?.PaymentDate,
            Rows: rows.ToArray());
    }

    /// Standard amortisation formula: P · r(1+r)^n / ((1+r)^n - 1).
    /// At 0% interest this collapses to P/n, which the branch handles directly.
    public static decimal MonthlyPayment(decimal principal, decimal monthlyRate, int termMonths)
    {
        if (termMonths <= 0) return principal;
        if (monthlyRate <= 0m) return Math.Round(principal / termMonths, 2, MidpointRounding.AwayFromZero);
        var pow = Pow(1m + monthlyRate, termMonths);
        var payment = principal * (monthlyRate * pow) / (pow - 1m);
        return Math.Round(payment, 2, MidpointRounding.AwayFromZero);
    }

    /// Decimal-domain integer exponent; avoids the double conversion in Math.Pow
    /// which would lose precision for the small monthly rates we deal with.
    static decimal Pow(decimal value, int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++) result *= value;
        return result;
    }

    /// First payment is conventionally one period after origination. Stay on the same
    /// day-of-month; if the next month is shorter, .AddMonths clamps to the last day.
    static DateTime FirstPaymentDate(DateTime startDate) => startDate.Date.AddMonths(1);
}
