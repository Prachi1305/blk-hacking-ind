// ============================================================
//  TransactionService  –  all period-processing business logic
// ============================================================

using BlkHackingInd.Models;

namespace BlkHackingInd.Services;

public class TransactionService
{
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    // ── helpers ───────────────────────────────────────────────

    /// <summary>
    /// Parse a date string that may use either space or 'T' separator
    /// and may omit seconds.
    /// </summary>
    private static DateTime Parse(string s)
    {
        s = s.Trim();
        // normalise: replace 'T' with space
        s = s.Replace('T', ' ');
        // pad seconds if missing
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd HH:mm:ss.fff"
        };
        if (DateTime.TryParseExact(s, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        // fallback
        return DateTime.Parse(s,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Fmt(DateTime dt) =>
        dt.ToString(DateFormat);

    // ── ceiling & remanent ────────────────────────────────────

    /// <summary>Round amount UP to the next strict multiple of 100.
    /// If already a multiple, the ceiling is amount+100.</summary>
    public static double ComputeCeiling(double amount)
    {
        if (amount <= 0) return 100;
        // if already exact multiple
        if (amount % 100 == 0) return amount + 100;
        return Math.Ceiling(amount / 100.0) * 100;
    }

    public static double ComputeRemanent(double amount, double ceiling)
        => ceiling - amount;

    // ── Endpoint 1 : parse ────────────────────────────────────

    public ParseResponse Parse(ParseRequest req)
    {
        var transactions = req.Expenses
            .Select(e =>
            {
                var dt      = Parse(e.Timestamp);
                var ceiling = ComputeCeiling(e.Amount);
                var rem     = ComputeRemanent(e.Amount, ceiling);
                return new Transaction(Fmt(dt), e.Amount, ceiling, rem);
            })
            .ToList();

        return new ParseResponse(
            transactions,
            transactions.Sum(t => t.Amount),
            transactions.Sum(t => t.Ceiling),
            transactions.Sum(t => t.Remanent));
    }

    // ── Endpoint 2 : validate ─────────────────────────────────

    public ValidatorResponse Validate(ValidatorRequest req)
    {
        // maximum that can be invested:
        // NPS cap → min(10% of annual_income, 200_000)
        double annualIncome  = req.Wage * 12;
        double maxInvestment = Math.Min(annualIncome * 0.10, 200_000);

        var valid      = new List<Transaction>();
        var invalid    = new List<InvalidTransaction>();
        var duplicates = new List<InvalidTransaction>();

        // detect duplicate dates
        var seen    = new HashSet<string>();
        var dupDates = new HashSet<string>();

        foreach (var t in req.Transactions)
            if (!seen.Add(t.Date))
                dupDates.Add(t.Date);

        foreach (var t in req.Transactions)
        {
            // 1. duplicate check
            if (dupDates.Contains(t.Date))
            {
                duplicates.Add(new InvalidTransaction(
                    t.Date, t.Amount, t.Ceiling, t.Remanent,
                    "Duplicate transaction date"));
                continue;
            }

            var errors = new List<string>();

            // 2. amount must be positive
            if (t.Amount <= 0)
                errors.Add("Transaction amount must be positive");

            // 3. ceiling must equal computed ceiling
            var expectedCeiling = ComputeCeiling(t.Amount);
            if (Math.Abs(t.Ceiling - expectedCeiling) > 0.001)
                errors.Add($"Ceiling mismatch: expected {expectedCeiling}, got {t.Ceiling}");

            // 4. remanent must match
            var expectedRem = ComputeRemanent(t.Amount, expectedCeiling);
            if (Math.Abs(t.Remanent - expectedRem) > 0.001)
                errors.Add($"Remanent mismatch: expected {expectedRem}, got {t.Remanent}");

            // 5. remanent must not exceed investment cap
            if (t.Remanent > maxInvestment)
                errors.Add($"Remanent {t.Remanent} exceeds maximum allowed {maxInvestment}");

            if (errors.Count > 0)
                invalid.Add(new InvalidTransaction(
                    t.Date, t.Amount, t.Ceiling, t.Remanent,
                    string.Join("; ", errors)));
            else
                valid.Add(t);
        }

        return new ValidatorResponse(valid, invalid, duplicates);
    }

    // ── Endpoint 3 : filter (apply q / p / k) ────────────────

    /// <summary>
    /// Apply period rules to every transaction and group into k periods.
    ///
    /// Processing order per transaction:
    ///   1. ceiling / remanent already supplied
    ///   2. apply q rules  (replace remanent with fixed)
    ///   3. apply p rules  (add extra to remanent)
    ///
    /// Then for k grouping: a transaction is "valid" if its date falls
    /// inside at least one k period; otherwise "invalid" (not in any k range).
    /// </summary>
    public FilterResponse Filter(FilterRequest req)
    {
        // pre-parse all period boundaries once
        var qParsed = req.Q.Select(q => (
            q.Fixed, Start: Parse(q.Start), End: Parse(q.End)
        )).ToList();

        var pParsed = req.P.Select(p => (
            p.Extra, Start: Parse(p.Start), End: Parse(p.End)
        )).ToList();

        var kParsed = req.K.Select(k => (
            Start: Parse(k.Start), End: Parse(k.End),
            RawStart: k.Start, RawEnd: k.End
        )).ToList();

        var valid   = new List<FilteredTransaction>();
        var invalid = new List<FilteredTransaction>();

        foreach (var tx in req.Transactions)
        {
            var txDate = Parse(tx.Date);
            double remanent = tx.Remanent;

            // ── Step 2: apply q rules ──────────────────────────
            // Find all q periods that contain this date
            var matchingQ = qParsed
                .Where(q => txDate >= q.Start && txDate <= q.End)
                .ToList();

            if (matchingQ.Count > 0)
            {
                // use the one with the latest start date;
                // on tie use first in original list (stable sort)
                var bestQ = matchingQ
                    .OrderByDescending(q => q.Start)
                    .ThenBy(q => qParsed.IndexOf(q))   // stable tie-break
                    .First();
                remanent = bestQ.Fixed;
            }

            // ── Step 3: apply p rules ──────────────────────────
            // add ALL matching p extras
            foreach (var p in pParsed)
                if (txDate >= p.Start && txDate <= p.End)
                    remanent += p.Extra;

            // ── Step 4: k period membership ───────────────────
            bool inAnyK = kParsed.Count == 0 ||
                          kParsed.Any(k => txDate >= k.Start && txDate <= k.End);

            var ft = new FilteredTransaction(
                tx.Date, tx.Amount, tx.Ceiling, remanent);

            if (inAnyK)
                valid.Add(ft);
            else
                invalid.Add(ft with
                {
                    Message = "Transaction date is outside all k period ranges"
                });
        }

        return new FilterResponse(valid, invalid);
    }

    /// <summary>
    /// Apply the same q/p logic and return per-transaction remanents,
    /// then aggregate by each k period.  Used by the returns endpoints.
    /// </summary>
    public List<(KPeriod Period, double SumRemanent, List<Transaction> Txns)>
        ComputeKGroups(
            List<QPeriod>    q,
            List<PPeriod>    p,
            List<KPeriod>    k,
            List<Transaction> transactions)
    {
        var qParsed = q.Select(qp => (
            qp.Fixed, Start: Parse(qp.Start), End: Parse(qp.End)
        )).ToList();

        var pParsed = p.Select(pp => (
            pp.Extra, Start: Parse(pp.Start), End: Parse(pp.End)
        )).ToList();

        // compute effective remanent for each transaction
        var withRem = transactions.Select(tx =>
        {
            var txDate  = Parse(tx.Date);
            double rem  = tx.Remanent;

            var matchQ = qParsed
                .Where(qp => txDate >= qp.Start && txDate <= qp.End)
                .ToList();
            if (matchQ.Count > 0)
            {
                var best = matchQ
                    .OrderByDescending(qp => qp.Start)
                    .ThenBy(qp => qParsed.IndexOf(qp))
                    .First();
                rem = best.Fixed;
            }

            foreach (var pp in pParsed)
                if (txDate >= pp.Start && txDate <= pp.End)
                    rem += pp.Extra;

            return (tx, txDate, EffectiveRem: rem);
        }).ToList();

        // group per k period
        return k.Select(kp =>
        {
            var kStart = Parse(kp.Start);
            var kEnd   = Parse(kp.End);
            var inK    = withRem
                .Where(x => x.txDate >= kStart && x.txDate <= kEnd)
                .ToList();
            double sum = inK.Sum(x => x.EffectiveRem);
            return (kp, sum, inK.Select(x => x.tx).ToList());
        }).ToList();
    }
}
