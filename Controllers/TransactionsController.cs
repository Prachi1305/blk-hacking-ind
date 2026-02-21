// ============================================================
//  TransactionsController
//  Handles:
//    POST /blackrock/challenge/v1/transactions:parse
//    POST /blackrock/challenge/v1/transactions:validator
//    POST /blackrock/challenge/v1/transactions:filter
// ============================================================

using BlkHackingInd.Models;
using BlkHackingInd.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlkHackingInd.Controllers;

[ApiController]
[Route("blackrock/challenge/v1")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _svc;

    public TransactionsController(TransactionService svc) => _svc = svc;

    // ── 1. Parse ──────────────────────────────────────────────
    [HttpPost("transactions:parse")]
    [ProducesResponseType(typeof(ParseResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public IActionResult Parse([FromBody] ParseRequest req)
    {
        if (req.Expenses is null || req.Expenses.Count == 0)
            return BadRequest(new { error = "Expenses list is empty or missing." });

        try
        {
            return Ok(_svc.Parse(req));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── 2. Validate ───────────────────────────────────────────
    [HttpPost("transactions:validator")]
    [ProducesResponseType(typeof(ValidatorResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public IActionResult Validate([FromBody] ValidatorRequest req)
    {
        if (req.Wage <= 0)
            return BadRequest(new { error = "Wage must be a positive number." });
        if (req.Transactions is null || req.Transactions.Count == 0)
            return BadRequest(new { error = "Transactions list is empty." });

        try
        {
            return Ok(_svc.Validate(req));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── 3. Filter (q + p + k) ─────────────────────────────────
    [HttpPost("transactions:filter")]
    [ProducesResponseType(typeof(FilterResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public IActionResult Filter([FromBody] FilterRequest req)
    {
        if (req.Transactions is null || req.Transactions.Count == 0)
            return BadRequest(new { error = "Transactions list is empty." });

        try
        {
            return Ok(_svc.Filter(req));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
