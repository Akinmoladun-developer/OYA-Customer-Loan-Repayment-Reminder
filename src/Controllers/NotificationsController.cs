using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyaMicroCreditCLRRS.Domain.Enums;
using OyaMicroCreditCLRRS.Models.Requests;
using OyaMicroCreditCLRRS.Models.Responses;
using OyaMicroCreditCLRRS.Services.Interfaces;

namespace OyaMicroCreditCLRRS.Controllers;

[Authorize]
public class NotificationsController : BaseController
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    // GET api/v1/notifications/logs
    [HttpGet("logs")]
    [Authorize(Roles = "Admin,Operations,BranchManager")]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationLogResponse>>>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] NotificationStatus? status = null,
        [FromQuery] NotificationChannel? channel = null,
        [FromQuery] NotificationType? type = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var result = await _notificationService.GetLogsAsync(
            page, pageSize, status, channel, type, from, to);

        return Ok(ApiResponse<PagedResult<NotificationLogResponse>>.Ok(result));
    }

    // POST api/v1/notifications/bulk-send
    [HttpPost("bulk-send")]
    [Authorize(Roles = "Admin,Operations")]
    public async Task<ActionResult<ApiResponse<string>>> BulkSend(
        [FromBody] BulkSendRequest request)
    {
        await _notificationService.BulkSendAsync(
            request.LoanIds,
            request.NotificationType,
            CurrentUserId);

        return Ok(ApiResponse<string>.Ok(
            "Success", "Bulk reminders dispatched successfully."));
    }

    // POST api/v1/notifications/webhook/dlr
    // Called by Termii to confirm message delivery — no auth required
    [HttpPost("webhook/dlr")]
    [AllowAnonymous]
    public async Task<IActionResult> DeliveryReceiptWebhook(
        [FromBody] TermiiDlrWebhookRequest request)
    {
        await _notificationService.ProcessDeliveryReceiptAsync(
            request.MessageId, request.Status);

        // Always return 200 to Termii — even on internal errors
        // to prevent Termii from retrying the webhook repeatedly
        return Ok(new { received = true });
    }

    // POST api/v1/notifications/opt-out
    // Called when a customer replies STOP to an SMS — no auth required
    [HttpPost("opt-out")]
    [AllowAnonymous]
    public async Task<IActionResult> SmsOptOut([FromQuery] string phone)
    {
        await _notificationService.ProcessOptOutAsync(phone);
        return Ok(new { received = true });
    }
}