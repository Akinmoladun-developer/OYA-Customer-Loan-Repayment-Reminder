using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyaMicroCreditCLRRS.Domain.Enums;
using OyaMicroCreditCLRRS.Models.Requests;
using OyaMicroCreditCLRRS.Models.Responses;
using OyaMicroCreditCLRRS.Services.Interfaces;

namespace OyaMicroCreditCLRRS.Controllers;

[Authorize]
public class LoansController : BaseController
{
    private readonly ILoanService _loanService;
    private readonly INotificationService _notificationService;

    public LoansController(
        ILoanService loanService,
        INotificationService notificationService)
    {
        _loanService = loanService;
        _notificationService = notificationService;
    }

    // GET api/v1/loans?page=1&pageSize=20&status=Active&search=OYA
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<LoanResponse>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] LoanStatus? status = null,
        [FromQuery] string? search = null)
    {
        var result = await _loanService.GetAllAsync(page, pageSize, status, search);
        return Ok(ApiResponse<PagedResult<LoanResponse>>.Ok(result));
    }

    // GET api/v1/loans/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<LoanResponse>>> GetById(Guid id)
    {
        var result = await _loanService.GetByIdAsync(id);
        return Ok(ApiResponse<LoanResponse>.Ok(result));
    }

    // GET api/v1/loans/by-reference/{reference}
    [HttpGet("by-reference/{reference}")]
    public async Task<ActionResult<ApiResponse<LoanResponse>>> GetByReference(string reference)
    {
        var result = await _loanService.GetByReferenceAsync(reference);
        return Ok(ApiResponse<LoanResponse>.Ok(result));
    }

    // GET api/v1/loans/by-customer/{customerId}
    [HttpGet("by-customer/{customerId:guid}")]
    public async Task<ActionResult<ApiResponse<PagedResult<LoanResponse>>>> GetByCustomer(
        Guid customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _loanService.GetByCustomerAsync(customerId, page, pageSize);
        return Ok(ApiResponse<PagedResult<LoanResponse>>.Ok(result));
    }

    // GET api/v1/loans/{id}/schedule
    [HttpGet("{id:guid}/schedule")]
    public async Task<ActionResult<ApiResponse<IEnumerable<RepaymentScheduleResponse>>>> GetSchedule(
        Guid id)
    {
        var result = await _loanService.GetScheduleAsync(id);
        return Ok(ApiResponse<IEnumerable<RepaymentScheduleResponse>>.Ok(result));
    }

    // GET api/v1/loans/{id}/notifications
    [HttpGet("{id:guid}/notifications")]
    public async Task<ActionResult<ApiResponse<IEnumerable<NotificationLogResponse>>>> GetNotificationHistory(
        Guid id)
    {
        var result = await _notificationService.GetLoanHistoryAsync(id);
        return Ok(ApiResponse<IEnumerable<NotificationLogResponse>>.Ok(result));
    }

    // POST api/v1/loans
    [HttpPost]
    [Authorize(Roles = "Admin,LoanOfficer")]
    public async Task<ActionResult<ApiResponse<LoanResponse>>> Create(
        [FromBody] CreateLoanRequest request)
    {
        var result = await _loanService.CreateAsync(request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<LoanResponse>.Ok(result, "Loan created successfully."));
    }

    // POST api/v1/loans/{id}/payments
    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = "Admin,LoanOfficer,Operations")]
    public async Task<ActionResult<ApiResponse<LoanResponse>>> RecordPayment(
        Guid id, [FromBody] RecordPaymentRequest request)
    {
        var result = await _loanService.RecordPaymentAsync(id, request);
        return Ok(ApiResponse<LoanResponse>.Ok(result, "Payment recorded successfully."));
    }

    // POST api/v1/loans/{id}/restructure
    [HttpPost("{id:guid}/restructure")]
    [Authorize(Roles = "Admin,BranchManager")]
    public async Task<ActionResult<ApiResponse<LoanResponse>>> Restructure(
        Guid id, [FromBody] RestructureLoanRequest request)
    {
        var result = await _loanService.RestructureAsync(id, request);
        return Ok(ApiResponse<LoanResponse>.Ok(result, "Loan restructured successfully."));
    }

    // POST api/v1/loans/{id}/send-reminder
    [HttpPost("{id:guid}/send-reminder")]
    [Authorize(Roles = "Admin,LoanOfficer,Operations")]
    public async Task<ActionResult<ApiResponse<string>>> SendReminder(
        Guid id, [FromBody] SendReminderRequest request)
    {
        var log = await _notificationService.SendReminderAsync(
            id,
            request.NotificationType,
            request.Channel,
            CurrentUserId,
            request.ScheduleId);

        return Ok(ApiResponse<string>.Ok(
            log.Status.ToString(),
            "Reminder dispatched successfully."));
    }
}