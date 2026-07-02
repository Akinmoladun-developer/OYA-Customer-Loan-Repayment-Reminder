using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyaMicroCreditCLRRS.Models.Responses;
using OyaMicroCreditCLRRS.Services.Interfaces;

namespace OyaMicroCreditCLRRS.Controllers;

[Authorize(Roles = "Admin,BranchManager,Operations")]
public class ReportsController : BaseController
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    // GET api/v1/reports/dashboard
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<DashboardResponse>>> GetDashboard(
        [FromQuery] string? branchCode = null)
    {
        var result = await _reportService.GetDashboardAsync(branchCode);
        return Ok(ApiResponse<DashboardResponse>.Ok(result));
    }

    // GET api/v1/reports/par?asOf=2026-06-01
    [HttpGet("par")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ParReportRow>>>> GetParReport(
        [FromQuery] DateOnly? asOf = null)
    {
        var date = asOf ?? DateOnly.FromDateTime(DateTime.Today);
        var result = await _reportService.GetParReportAsync(date);
        return Ok(ApiResponse<IEnumerable<ParReportRow>>.Ok(result));
    }

    // GET api/v1/reports/export?from=2026-01-01&to=2026-06-30
    [HttpGet("export")]
    public async Task<IActionResult> ExportNotificationLogs(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (from > to)
            return BadRequest(ApiResponse<string>.Fail(
                "The 'from' date must be earlier than the 'to' date."));

        var csvBytes = await _reportService.ExportNotificationLogsCsvAsync(from, to);

        var fileName = $"OyaMFB_NotificationLogs_{from:yyyyMMdd}_{to:yyyyMMdd}.csv";

        return File(csvBytes, "text/csv", fileName);
    }
}