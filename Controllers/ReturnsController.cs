// ============================================================
//  ReturnsController
//  Handles:
//    POST /blackrock/challenge/v1/returns:nps
//    POST /blackrock/challenge/v1/returns:index
// ============================================================

using BlkHackingInd.Models;
using BlkHackingInd.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlkHackingInd.Controllers;

[ApiController]
[Route("blackrock/challenge/v1/returns")]
public class ReturnsController : ControllerBase
{
    private readonly ReturnsService     _ret;
    private readonly TransactionService _txn;

    public ReturnsController(ReturnsService ret, TransactionService txn)
    {
        _ret = ret;
        _txn = txn;
    }

    // ── NPS ───────────────────────────────────────────────────
    [HttpPost(":nps")]
    [ProducesResponseType(typeof(ReturnsResponse), 200)]
    [ProducesResponseType(400)]
    public IActionResult Nps([FromBody] ReturnsRequest req)
    {
        var err = ValidateReturnsRequest(req);
        if (err is not null) return BadRequest(new { error = err });

        try { return Ok(_ret.CalculateNps(req, _txn)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Index Fund ────────────────────────────────────────────
    [HttpPost(":index")]
    [ProducesResponseType(typeof(ReturnsResponse), 200)]
    [ProducesResponseType(400)]
    public IActionResult Index([FromBody] ReturnsRequest req)
    {
        var err = ValidateReturnsRequest(req);
        if (err is not null) return BadRequest(new { error = err });

        try { return Ok(_ret.CalculateIndex(req, _txn)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    private static string? ValidateReturnsRequest(ReturnsRequest req)
    {
        if (req.Age <= 0)              return "Age must be a positive integer.";
        if (req.Wage <= 0)             return "Wage must be a positive number.";
        if (req.K is null || req.K.Count == 0)
            return "At least one k period must be supplied.";
        if (req.Transactions is null || req.Transactions.Count == 0)
            return "Transactions list is empty.";
        return null;
    }
}
