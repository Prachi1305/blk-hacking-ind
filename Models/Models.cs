// ============================================================
//  All request / response DTOs for the BlackRock challenge
// ============================================================

namespace BlkHackingInd.Models;

// ── Shared primitives ──────────────────────────────────────

/// <summary>A raw expense supplied by the caller.</summary>
public record Expense(
    string Timestamp,
    double Amount);

/// <summary>A transaction after parsing (ceiling + remanent attached).</summary>
public record Transaction(
    string Date,
    double Amount,
    double Ceiling,
    double Remanent);

/// <summary>A transaction that failed validation.</summary>
public record InvalidTransaction(
    string Date,
    double Amount,
    double Ceiling,
    double Remanent,
    string Message);

// ── Period definitions ─────────────────────────────────────

public record QPeriod(double Fixed, string Start, string End);
public record PPeriod(double Extra,  string Start, string End);
public record KPeriod(string Start,  string End);

// ── Endpoint 1 : transactions:parse ───────────────────────

public record ParseRequest(List<Expense> Expenses);

public record ParseResponse(
    List<Transaction> Transactions,
    double TotalAmount,
    double TotalCeiling,
    double TotalRemanent);

// ── Endpoint 2 : transactions:validator ───────────────────

public record ValidatorRequest(
    double Wage,
    List<Transaction> Transactions);

public record ValidatorResponse(
    List<Transaction>        Valid,
    List<InvalidTransaction> Invalid,
    List<InvalidTransaction> Duplicates);

// ── Endpoint 3 : transactions:filter ──────────────────────

/// <summary>Transaction with all period rules already applied.</summary>
public record FilteredTransaction(
    string Date,
    double Amount,
    double Ceiling,
    double Remanent,
    string? Message = null);   // null ⇒ valid

public record FilterRequest(
    List<QPeriod>      Q,
    List<PPeriod>      P,
    List<KPeriod>      K,
    List<Transaction>  Transactions);

public record FilterResponse(
    List<FilteredTransaction> Valid,
    List<FilteredTransaction> Invalid);

// ── Endpoint 4 : returns:nps / returns:index ──────────────

public record ReturnsRequest(
    int              Age,
    double           Wage,
    double           Inflation,
    List<QPeriod>    Q,
    List<PPeriod>    P,
    List<KPeriod>    K,
    List<Transaction> Transactions);

public record SavingByDate(
    string Start,
    string End,
    double Amount,
    double Profits,
    double TaxBenefit);

public record ReturnsResponse(
    double           TransactionsTotalAmount,
    double           TransactionsTotalCeiling,
    List<SavingByDate> SavingsByDates);

// ── Endpoint 5 : performance ──────────────────────────────

public record PerformanceResponse(
    string Time,
    string Memory,
    int    Threads);
