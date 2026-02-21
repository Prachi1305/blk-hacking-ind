// ============================================================
//  PerformanceController
//  Handles:  GET /blackrock/challenge/v1/performance
// ============================================================

using BlkHackingInd.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BlkHackingInd.Controllers;

[ApiController]
[Route("blackrock/challenge/v1")]
public class PerformanceController : ControllerBase
{
    // Capture application start time so we can measure uptime / response time
    private static readonly Stopwatch _appWatch = Stopwatch.StartNew();

    [HttpGet("performance")]
    [ProducesResponseType(typeof(PerformanceResponse), 200)]
    public IActionResult GetPerformance()
    {
        // ── Response time: wall-clock since app start ─────────
        var elapsed   = _appWatch.Elapsed;
        string timeStr = elapsed.ToString(@"hh\:mm\:ss\.fff");

        // ── Memory: GC + private bytes (process) ─────────────
        long   privateBytes = Process.GetCurrentProcess().PrivateMemorySize64;
        double memMb        = privateBytes / (1024.0 * 1024.0);
        string memStr       = $"{memMb:F2} MB";

        // ── Thread count ──────────────────────────────────────
        int threads = Process.GetCurrentProcess().Threads.Count;

        return Ok(new PerformanceResponse(timeStr, memStr, threads));
    }
}
