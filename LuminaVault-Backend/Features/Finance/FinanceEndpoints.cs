namespace LuminaVault.Endpoints;

/// Thin orchestrator — each resource lives in its own file under Endpoints/Finance/.
/// Wire shapes (DTOs/Inputs) are in FinanceDtos.cs; shared math in FinanceHelpers.cs.
public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinance(this IEndpointRouteBuilder app)
    {
        AccountEndpoints.Map(app);
        TransactionEndpoints.Map(app);
        HoldingEndpoints.Map(app);
        BudgetEndpoints.Map(app);
        GoalEndpoints.Map(app);
        SubscriptionEndpoints.Map(app);
        MonthlySummaryEndpoints.Map(app);
        BalanceSnapshotEndpoints.Map(app);
        FinanceSummaryEndpoints.Map(app);
        return app;
    }
}
