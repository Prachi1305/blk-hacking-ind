// ============================================================
//  ReturnsService  –  compound interest, tax benefit, inflation
// ============================================================

using BlkHackingInd.Models;

namespace BlkHackingInd.Services;

public class ReturnsService
{
    private const double NpsRate    = 0.0711;   // 7.11 %
    private const double NiftyRate  = 0.1449;   // 14.49 %
    private const double DefaultInflation = 0.055; // 5.5 %

    // ── Tax slabs (simplified, pre-tax / gross salary) ────────
    /// <summary>
    /// Simplified Indian income-tax on <paramref name="income"/> (annual).
    /// Slabs (no deductions / standard-deduction applied):
    ///   0  – 7,00,000    : 0 %
    ///   7,00,001–10,00,000: 10 % on excess above 7 L
    ///   10,00,001–12,00,000: 15 % on excess above 10 L
    ///   12,00,001–15,00,000: 20 % on excess above 12 L
    ///   > 15,00,000       : 30 % on excess above 15 L
    /// </summary>
    public static double ComputeTax(double income)
    {
        if (income <= 0) return 0;

        double tax = 0;
        if (income > 1_500_000)
        {
            tax += (income - 1_500_000) * 0.30;
            income = 1_500_000;
        }
        if (income > 1_200_000)
        {
            tax += (income - 1_200_000) * 0.20;
            income = 1_200_000;
        }
        if (income > 1_000_000)
        {
            tax += (income - 1_000_000) * 0.15;
            income = 1_000_000;
        }
        if (income > 700_000)
        {
            tax += (income - 700_000) * 0.10;
        }
        return tax;
    }

    // ── NPS tax benefit ───────────────────────────────────────
    /// <summary>
    /// NPS_Deduction = min(invested, 10 % of annual_income, 2,00,000)
    /// Tax_Benefit   = Tax(income) – Tax(income – NPS_Deduction)
    /// </summary>
    public static double ComputeNpsTaxBenefit(double invested, double annualIncome)
    {
        double npsDeduction = Math.Min(invested,
                              Math.Min(annualIncome * 0.10, 200_000));
        return ComputeTax(annualIncome) - ComputeTax(annualIncome - npsDeduction);
    }

    // ── Compound interest ─────────────────────────────────────
    /// <summary>A = P × (1 + r)^t   (compounded annually, n=1)</summary>
    public static double CompoundInterest(double principal, double rate, int years)
        => principal * Math.Pow(1 + rate, years);

    // ── Inflation adjustment ──────────────────────────────────
    /// <summary>Areal = A / (1 + inflation)^t</summary>
    public static double AdjustForInflation(double amount, double inflation, int years)
        => amount / Math.Pow(1 + inflation, years);

    // ── Years to invest ───────────────────────────────────────
    /// <summary>
    /// t = 60 – age.   If age >= 60, use 5 as minimum.
    /// </summary>
    public static int InvestmentYears(int age)
        => age >= 60 ? 5 : 60 - age;

    // ── Main return calculation (NPS) ─────────────────────────
    public ReturnsResponse CalculateNps(
        ReturnsRequest req,
        TransactionService txService)
    {
        var groups = txService.ComputeKGroups(
            req.Q, req.P, req.K, req.Transactions);

        int    years       = InvestmentYears(req.Age);
        double annualIncome = req.Wage * 12;
        double inflation   = req.Inflation > 0 ? req.Inflation : DefaultInflation;

        var savingsByDates = groups.Select(g =>
        {
            double p         = g.SumRemanent;
            double maturity  = CompoundInterest(p, NpsRate, years);
            double realValue = AdjustForInflation(maturity, inflation, years);
            double profit    = realValue - p;
            double taxBen    = ComputeNpsTaxBenefit(p, annualIncome);

            return new SavingByDate(
                g.Period.Start,
                g.Period.End,
                Math.Round(p, 2),
                Math.Round(profit, 2),
                Math.Round(taxBen, 2));
        }).ToList();

        return new ReturnsResponse(
            Math.Round(req.Transactions.Sum(t => t.Amount), 2),
            Math.Round(req.Transactions.Sum(t => t.Ceiling), 2),
            savingsByDates);
    }

    // ── Main return calculation (Index / NIFTY 50) ────────────
    public ReturnsResponse CalculateIndex(
        ReturnsRequest req,
        TransactionService txService)
    {
        var groups = txService.ComputeKGroups(
            req.Q, req.P, req.K, req.Transactions);

        int    years     = InvestmentYears(req.Age);
        double inflation = req.Inflation > 0 ? req.Inflation : DefaultInflation;

        var savingsByDates = groups.Select(g =>
        {
            double p         = g.SumRemanent;
            double maturity  = CompoundInterest(p, NiftyRate, years);
            double realValue = AdjustForInflation(maturity, inflation, years);
            double profit    = realValue - p;

            return new SavingByDate(
                g.Period.Start,
                g.Period.End,
                Math.Round(p, 2),
                Math.Round(profit, 2),
                0.0);   // no tax benefit for index fund
        }).ToList();

        return new ReturnsResponse(
            Math.Round(req.Transactions.Sum(t => t.Amount), 2),
            Math.Round(req.Transactions.Sum(t => t.Ceiling), 2),
            savingsByDates);
    }
}
