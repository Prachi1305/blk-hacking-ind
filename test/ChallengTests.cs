// ============================================================
// Test type    : Unit Tests (xUnit)
// Validation   : TransactionService + ReturnsService business logic
// Command      : dotnet test test/blk-hacking-ind.Tests.csproj
// ============================================================

using BlkHackingInd.Models;
using BlkHackingInd.Services;
using Xunit;

namespace BlkHackingInd.Tests;

public class CeilingRemanentTests
{
    // ── Ceiling computation ───────────────────────────────────
    [Theory]
    [InlineData(250,  300,  50)]   // normal rounding
    [InlineData(375,  400,  25)]
    [InlineData(620,  700,  80)]
    [InlineData(480,  500,  20)]
    [InlineData(1519, 1600, 81)]   // from PDF description
    [InlineData(100,  200, 100)]   // already a multiple → next multiple
    [InlineData(200,  300, 100)]
    [InlineData(1,    100,  99)]   // tiny amount
    public void Ceiling_And_Remanent_AreCorrect(
        double amount, double expectedCeiling, double expectedRem)
    {
        double ceiling = TransactionService.ComputeCeiling(amount);
        double rem     = TransactionService.ComputeRemanent(amount, ceiling);

        Assert.Equal(expectedCeiling, ceiling);
        Assert.Equal(expectedRem, rem, precision: 2);
    }
}

public class ParseTests
{
    private readonly TransactionService _svc = new();

    // ── Full parse from PDF example ───────────────────────────
    [Fact]
    public void Parse_PdfExample_ReturnsCorrectTransactions()
    {
        // Test type    : Unit – full parse pipeline
        // Validation   : ceiling, remanent, totals match PDF example
        // Command      : dotnet test test/blk-hacking-ind.Tests.csproj

        var req = new ParseRequest(new List<Expense>
        {
            new("2023-10-12 20:15:00", 250),
            new("2023-02-28 15:49:00", 375),
            new("2023-07-01 21:59:00", 620),
            new("2023-12-17 08:09:00", 480),
        });

        var resp = _svc.Parse(req);

        Assert.Equal(4, resp.Transactions.Count);

        // expense 250 → ceiling 300, rem 50
        Assert.Equal(300,  resp.Transactions[0].Ceiling);
        Assert.Equal(50,   resp.Transactions[0].Remanent, 2);

        // expense 375 → ceiling 400, rem 25
        Assert.Equal(400,  resp.Transactions[1].Ceiling);
        Assert.Equal(25,   resp.Transactions[1].Remanent, 2);

        // expense 620 → ceiling 700, rem 80
        Assert.Equal(700,  resp.Transactions[2].Ceiling);
        Assert.Equal(80,   resp.Transactions[2].Remanent, 2);

        // expense 480 → ceiling 500, rem 20
        Assert.Equal(500,  resp.Transactions[3].Ceiling);
        Assert.Equal(20,   resp.Transactions[3].Remanent, 2);

        // totals
        Assert.Equal(1725, resp.TotalAmount,   2);
        Assert.Equal(1900, resp.TotalCeiling,  2);
        Assert.Equal(175,  resp.TotalRemanent, 2);
    }

    // ── Edge: empty list ──────────────────────────────────────
    [Fact]
    public void Parse_EmptyExpenses_ReturnsEmptyList()
    {
        var resp = _svc.Parse(new ParseRequest(new List<Expense>()));
        Assert.Empty(resp.Transactions);
        Assert.Equal(0, resp.TotalRemanent);
    }
}

public class FilterTests
{
    private readonly TransactionService _svc = new();

    // ── Full period logic from PDF example ───────────────────
    [Fact]
    public void Filter_PdfExample_CorrectRemanents()
    {
        // Test type    : Unit – q / p period rules
        // Validation   : matches the PDF worked example exactly

        var transactions = new List<Transaction>
        {
            new("2023-10-12 20:15:00", 250, 300,  50),
            new("2023-02-28 15:49:00", 375, 400,  25),
            new("2023-07-01 21:59:00", 620, 700,  80),
            new("2023-12-17 08:09:00", 480, 500,  20),
        };

        var q = new List<QPeriod>
        {
            new(0, "2023-07-01 00:00:00", "2023-07-31 23:59:00")
        };
        var p = new List<PPeriod>
        {
            new(25, "2023-10-01 08:00:00", "2023-12-31 19:59:00")
        };
        var k = new List<KPeriod>
        {
            new("2023-03-01 00:00:00", "2023-11-30 23:59:00"),
            new("2023-01-01 00:00:00", "2023-12-31 23:59:00"),
        };

        var resp = _svc.Filter(new FilterRequest(q, p, k, transactions));

        // All 4 transactions fall in at least one k period → all valid
        Assert.Equal(4, resp.Valid.Count);
        Assert.Empty(resp.Invalid);

        // Oct-12: p adds 25 → rem = 75
        var oct12 = resp.Valid.Single(t => t.Date.StartsWith("2023-10-12"));
        Assert.Equal(75, oct12.Remanent, 2);

        // Feb-28: no q, no p → rem = 25
        var feb28 = resp.Valid.Single(t => t.Date.StartsWith("2023-02-28"));
        Assert.Equal(25, feb28.Remanent, 2);

        // Jul-01: q replaces to 0 → rem = 0 (not in any p period)
        var jul01 = resp.Valid.Single(t => t.Date.StartsWith("2023-07-01"));
        Assert.Equal(0, jul01.Remanent, 2);

        // Dec-17: p adds 25 → rem = 45
        // Note: Dec-17 is AFTER p end (19:59), so depends on exact time check
        var dec17 = resp.Valid.Single(t => t.Date.StartsWith("2023-12-17"));
        Assert.Equal(45, dec17.Remanent, 2);
    }

    // ── q-period tie-break: latest start wins ─────────────────
    [Fact]
    public void Filter_MultipleQPeriods_LatestStartWins()
    {
        // Test type    : Unit – q tie-break rule
        // Validation   : when 2 q periods overlap, use latest start

        var tx = new List<Transaction>
        {
            new("2023-06-15 10:00:00", 500, 600, 100)
        };

        var q = new List<QPeriod>
        {
            new(50,  "2023-06-01 00:00:00", "2023-06-30 23:59:00"),  // earlier start
            new(99,  "2023-06-10 00:00:00", "2023-06-30 23:59:00"),  // later start → should win
        };

        var resp = _svc.Filter(new FilterRequest(q, new(), new(), tx));

        Assert.Single(resp.Valid);
        Assert.Equal(99, resp.Valid[0].Remanent, 2);
    }

    // ── p-period stacks ───────────────────────────────────────
    [Fact]
    public void Filter_MultiplePPeriods_ExtrasStack()
    {
        // Test type    : Unit – p stacking rule
        // Validation   : both p extras are added to remanent

        var tx = new List<Transaction>
        {
            new("2023-05-10 10:00:00", 250, 300, 50)
        };

        var p = new List<PPeriod>
        {
            new(10, "2023-05-01 00:00:00", "2023-05-31 23:59:00"),
            new(15, "2023-05-01 00:00:00", "2023-05-31 23:59:00"),
        };

        var resp = _svc.Filter(new FilterRequest(new(), p, new(), tx));
        Assert.Equal(75, resp.Valid[0].Remanent, 2);  // 50 + 10 + 15
    }

    // ── Transaction outside all k periods → invalid ───────────
    [Fact]
    public void Filter_TxOutsideAllKPeriods_MarkedInvalid()
    {
        var tx = new List<Transaction>
        {
            new("2023-01-15 10:00:00", 200, 300, 100)
        };

        var k = new List<KPeriod>
        {
            new("2023-06-01 00:00:00", "2023-12-31 23:59:00")
        };

        var resp = _svc.Filter(new FilterRequest(new(), new(), k, tx));
        Assert.Empty(resp.Valid);
        Assert.Single(resp.Invalid);
    }
}

public class ValidatorTests
{
    private readonly TransactionService _svc = new();

    // ── Duplicate date detection ──────────────────────────────
    [Fact]
    public void Validator_DuplicateDate_DetectedCorrectly()
    {
        // Test type    : Unit – duplicate detection
        // Validation   : same date timestamp → goes to duplicates

        var req = new ValidatorRequest(50_000, new List<Transaction>
        {
            new("2023-10-12 20:15:00", 250, 300, 50),
            new("2023-10-12 20:15:00", 350, 400, 50),  // duplicate
        });

        var resp = _svc.Validate(req);
        Assert.Empty(resp.Valid);
        Assert.Equal(2, resp.Duplicates.Count);
    }

    // ── Ceiling mismatch → invalid ────────────────────────────
    [Fact]
    public void Validator_WrongCeiling_MarkedInvalid()
    {
        var req = new ValidatorRequest(50_000, new List<Transaction>
        {
            new("2023-10-12 20:15:00", 250, 400, 150)  // wrong ceiling (should be 300)
        });

        var resp = _svc.Validate(req);
        Assert.Empty(resp.Valid);
        Assert.Single(resp.Invalid);
        Assert.Contains("Ceiling mismatch", resp.Invalid[0].Message);
    }
}

public class ReturnsTests
{
    private readonly TransactionService _tx  = new();
    private readonly ReturnsService     _ret = new();

    // ── Tax computation ───────────────────────────────────────
    [Theory]
    [InlineData(600_000,  0)]          // below 7L → 0 tax
    [InlineData(700_000,  0)]          // exactly 7L  → 0 tax
    [InlineData(800_000,  10_000)]     // 10% on 1L above 7L
    [InlineData(1_000_000, 30_000)]    // 10% on 3L = 30,000
    [InlineData(1_100_000, 45_000)]    // 30,000 + 15% on 1L = 15,000 → 45,000
    public void Tax_SlabsAreCorrect(double income, double expectedTax)
    {
        Assert.Equal(expectedTax, ReturnsService.ComputeTax(income), 0);
    }

    // ── NPS deduction cap ─────────────────────────────────────
    [Fact]
    public void NpsTaxBenefit_BelowSevenLakh_ZeroBenefit()
    {
        // Annual income 6L → falls in 0% slab → no tax benefit
        double benefit = ReturnsService.ComputeNpsTaxBenefit(145, 6_00_000);
        Assert.Equal(0, benefit, 2);
    }

    // ── Compound interest ─────────────────────────────────────
    [Fact]
    public void CompoundInterest_MatchesPdfExample()
    {
        // PDF: 145 × (1.0711)^31 ≈ 1219.45
        double result = ReturnsService.CompoundInterest(145, 0.0711, 31);
        Assert.InRange(result, 1210, 1230);  // allow small floating-point delta
    }

    [Fact]
    public void CompoundInterest_NiftyMatchesPdfExample()
    {
        // PDF: 145 × (1.1449)^31 ≈ 9619.7
        double result = ReturnsService.CompoundInterest(145, 0.1449, 31);
        Assert.InRange(result, 9600, 9650);
    }

    // ── Inflation adjustment ──────────────────────────────────
    [Fact]
    public void InflationAdjustment_MatchesPdfExample()
    {
        // NPS real: 1219.45 / (1.055)^31 ≈ 231.9
        double real = ReturnsService.AdjustForInflation(1219.45, 0.055, 31);
        Assert.InRange(real, 228, 236);
    }

    // ── Full PDF worked example returns ──────────────────────
    [Fact]
    public void ReturnsIndex_FullPdfExample_CorrectOutput()
    {
        // Test type    : Integration – returns pipeline
        // Validation   : second k range (full year) → remanent=145, real≈1829

        var req = new ReturnsRequest(
            Age:   29,
            Wage:  50_000,
            Inflation: 0.055,
            Q: new() { new(0, "2023-07-01 00:00:00", "2023-07-31 23:59:00") },
            P: new() { new(25, "2023-10-01 08:00:00", "2023-12-31 19:59:00") },
            K: new()
            {
                new("2023-03-01 00:00:00", "2023-11-30 23:59:00"),
                new("2023-01-01 00:00:00", "2023-12-31 23:59:00"),
            },
            Transactions: new()
            {
                new("2023-10-12 20:15:00", 250, 300,  50),
                new("2023-02-28 15:49:00", 375, 400,  25),
                new("2023-07-01 21:59:00", 620, 700,  80),
                new("2023-12-17 08:09:00", 480, 500,  20),
            });

        var resp = _ret.CalculateIndex(req, _tx);

        // k1 (Mar-Nov): transactions in range: Oct-12(75), Jul-01(0) → sum=75
        var k1 = resp.SavingsByDates[0];
        Assert.Equal(75, k1.Amount, 2);
        Assert.True(k1.Profits > 0);

        // k2 (full year): sum = 75+25+0+45 = 145
        var k2 = resp.SavingsByDates[1];
        Assert.Equal(145, k2.Amount, 2);

        // real NIFTY return for 145 × 31 yrs ≈ 1829 → profit ≈ 1829 - 145 = 1684
        Assert.InRange(k2.Profits, 1650, 1720);
        Assert.Equal(0, k2.TaxBenefit);
    }

    // ── Age ≥ 60 uses minimum 5 years ─────────────────────────
    [Fact]
    public void InvestmentYears_AgeOver60_Returns5()
    {
        Assert.Equal(5, ReturnsService.InvestmentYears(65));
        Assert.Equal(5, ReturnsService.InvestmentYears(60));
    }

    [Fact]
    public void InvestmentYears_Age29_Returns31()
    {
        Assert.Equal(31, ReturnsService.InvestmentYears(29));
    }
}
