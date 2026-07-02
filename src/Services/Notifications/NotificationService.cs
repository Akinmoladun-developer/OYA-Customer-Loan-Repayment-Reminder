using Microsoft.EntityFrameworkCore;
using OyaMicroCreditCLRRS.Data;
using OyaMicroCreditCLRRS.Domain.Entities;
using OyaMicroCreditCLRRS.Domain.Enums;
using OyaMicroCreditCLRRS.Domain.Exceptions;
using OyaMicroCreditCLRRS.Helpers;
using OyaMicroCreditCLRRS.Models.Responses;
using OyaMicroCreditCLRRS.Services.Interfaces;

namespace OyaMicroCreditCLRRS.Services.Notifications;

// Creates all notification dispatch for the Oya CLRRS.
// Applying SOLID Principle
// - SRP: handles only notification dispatch and audit logging
// - OCP: new channels added by implementing ITermiiGateway 
// - DIP: depends on ITermiiGateway, not TermiiGateway directly

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly ITermiiGateway _termii;
    private readonly ILogger<NotificationService> _logger;
    private readonly TermiiSettings _termiiSettings;

    public NotificationService(
        AppDbContext db,
        ITermiiGateway termii,
        ILogger<NotificationService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _termii = termii;
        _logger = logger;
        _termiiSettings = configuration
            .GetSection(TermiiSettings.SectionKey)
            .Get<TermiiSettings>() ?? new TermiiSettings();
    }

    // Send a single reminder

    public async Task<NotificationLog> SendReminderAsync(
        Guid loanId,
        NotificationType type,
        NotificationChannel channel,
        string triggeredBy,
        Guid? scheduleId = null)
    {
        // Load loan with all related data needed for rendering
        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanOfficer)
            .Include(l => l.RepaymentSchedules)
            .FirstOrDefaultAsync(l => l.Id == loanId)
            ?? throw new NotFoundException("Loan", loanId);

        // NDPR: check opt-out before sending anything
        if (channel == NotificationChannel.Sms && loan.Customer.SmsOptOut)
        {
            _logger.LogInformation(
                "Skipping SMS for loan {LoanRef} — customer has opted out.",
                loan.LoanReference);
            throw new DomainException("Customer has opted out of SMS notifications.");
        }

        if (channel == NotificationChannel.Email && loan.Customer.EmailOptOut)
            throw new DomainException("Customer has opted out of email notifications.");

        // Resolve the currently active template for this channel + type
        var template = await _db.MessageTemplates
            .FirstOrDefaultAsync(t =>
                t.Channel == channel &&
                t.NotificationType == type &&
                t.IsActive)
            ?? throw new NotFoundException(
                $"No active template found for channel '{channel}' and type '{type}'.");

        // Find the next unpaid installment to use for placeholder values
        var nextInstallment = loan.RepaymentSchedules
            .Where(s => s.Status is
                InstallmentStatus.Pending or
                InstallmentStatus.PartiallyPaid or
                InstallmentStatus.Overdue)
            .OrderBy(s => s.DueDate)
            .FirstOrDefault();

        // Build placeholder dictionary and render the message body
        var values = BuildPlaceholderValues(loan, nextInstallment);
        var rendered = RenderTemplate(template.Body, values);

        // Create audit log immediately in Queued state before sending
        var log = new NotificationLog
        {
            LoanId = loanId,
            CustomerId = loan.CustomerId,
            ScheduleId = scheduleId ?? nextInstallment?.Id,
            TemplateId = template.Id,
            Channel = channel,
            NotificationType = type,
            Status = NotificationStatus.Queued,
            RecipientPhone = PhoneHelper.Mask(loan.Customer.PhoneNumber),
            RenderedMessage = rendered,
            TriggeredBy = triggeredBy
        };

        _db.NotificationLogs.Add(log);
        await _db.SaveChangesAsync();

        // Dispatch via Termii 

        TermiiSendResult result;

        switch (channel)
        {
            case NotificationChannel.Sms:
                result = await _termii.SendSmsAsync(
                    loan.Customer.PhoneNumber,
                    rendered,
                    _termiiSettings.SenderId);
                break;

            case NotificationChannel.Email when loan.Customer.Email is not null:
                result = await _termii.SendEmailAsync(
                    loan.Customer.Email,
                    template.SubjectLine ?? "Oya Micro Finance Bank - Loan Reminder",
                    rendered);
                break;

            default:
                result = new TermiiSendResult(
                    false, null,
                    $"Channel '{channel}' is not supported or customer has no email address.");
                break;
        }

        // Update log with dispatch result

        if (result.Success)
        {
            log.Status = NotificationStatus.Sent;
            log.TermiiMessageId = result.MessageId;
            log.SentAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Notification sent for loan {LoanRef} via {Channel}. TermiiId: {MessageId}",
                loan.LoanReference, channel, result.MessageId);
        }
        else
        {
            log.RetryCount++;
            log.FailureReason = result.ErrorMessage;
            log.Status = log.RetryCount >= 3
                ? NotificationStatus.Failed
                : NotificationStatus.Retrying;

            _logger.LogWarning(
                "Notification failed for loan {LoanRef}. Attempt {Retry}/3. Reason: {Reason}",
                loan.LoanReference, log.RetryCount, result.ErrorMessage);
        }

        await _db.SaveChangesAsync();

        // Mark idempotency flag on schedule after successful send

        if (nextInstallment is not null && result.Success)
        {
            var today = WatClock.Today;
            var daysUntilDue = nextInstallment.DueDate.DayNumber - today.DayNumber;

            if (type == NotificationType.PreDueReminder)
                MarkReminderSentFlag(nextInstallment, daysUntilDue);
            else if (type == NotificationType.DueTodayReminder)
                nextInstallment.ReminderSentDueToday = true;

            await _db.SaveChangesAsync();
        }

        return log;
    }

    // Bulk send

    public async Task BulkSendAsync(
        IEnumerable<Guid> loanIds,
        NotificationType type,
        string triggeredBy)
    {
        var tasks = loanIds.Select(id =>
            SendReminderAsync(id, type, NotificationChannel.Sms, triggeredBy)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _logger.LogWarning(
                            "Bulk send failed for loan {LoanId}: {Error}",
                            id, t.Exception?.GetBaseException().Message);
                }));

        await Task.WhenAll(tasks);
    }

    // Termii DLR webhook

    public async Task ProcessDeliveryReceiptAsync(string termiiMessageId, string status)
    {
        var log = await _db.NotificationLogs
            .FirstOrDefaultAsync(n => n.TermiiMessageId == termiiMessageId);

        if (log is null)
        {
            _logger.LogWarning(
                "DLR received for unknown TermiiMessageId: {Id}", termiiMessageId);
            return;
        }

        if (status.ToLowerInvariant() is "delivered" or "success")
        {
            log.Status = NotificationStatus.Delivered;
            log.DeliveredAt = DateTime.UtcNow;
            _logger.LogInformation(
                "DLR confirmed delivery for TermiiMessageId: {Id}", termiiMessageId);
        }
        else
        {
            log.RetryCount++;
            log.FailureReason = $"DLR status: {status}";
            log.Status = log.RetryCount >= 3
                ? NotificationStatus.Failed
                : NotificationStatus.Retrying;

            _logger.LogWarning(
                "DLR reported failure for {MessageId}. Status: {Status}",
                termiiMessageId, status);
        }

        await _db.SaveChangesAsync();
    }

    // NDPR opt-out (customer replies STOP)

    public async Task ProcessOptOutAsync(string phoneNumber)
    {
        var normalised = PhoneHelper.Normalise(phoneNumber);

        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.PhoneNumber == normalised);

        if (customer is null)
        {
            _logger.LogWarning(
                "Opt-out received for unknown phone: {Phone}",
                PhoneHelper.Mask(normalised));
            return;
        }

        customer.SmsOptOut = true;
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Customer {Id} ({Phone}) opted out of SMS notifications.",
            customer.Id, PhoneHelper.Mask(normalised));
    }

    // Query: paged notification log

    public async Task<PagedResult<NotificationLogResponse>> GetLogsAsync(
        int page,
        int pageSize,
        NotificationStatus? status,
        NotificationChannel? channel,
        NotificationType? type,
        DateTime? from,
        DateTime? to)
    {
        var query = _db.NotificationLogs
            .Include(n => n.Loan)
                .ThenInclude(l => l.Customer)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(n => n.Status == status.Value);

        if (channel.HasValue)
            query = query.Where(n => n.Channel == channel.Value);

        if (type.HasValue)
            query = query.Where(n => n.NotificationType == type.Value);

        if (from.HasValue)
            query = query.Where(n => n.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(n => n.CreatedAt <= to.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => MapToLogResponse(n))
            .ToListAsync();

        return new PagedResult<NotificationLogResponse>(items, total, page, pageSize);
    }

    // Query: notification history for a single loan

    public async Task<IEnumerable<NotificationLogResponse>> GetLoanHistoryAsync(Guid loanId)
    {
        return await _db.NotificationLogs
            .Include(n => n.Loan)
                .ThenInclude(l => l.Customer)
            .Where(n => n.LoanId == loanId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => MapToLogResponse(n))
            .ToListAsync();
    }

    // Private helpers

    private static Dictionary<string, string> BuildPlaceholderValues(
        Loan loan,
        RepaymentSchedule? installment) => new()
    {
        ["{CustomerName}"]  = loan.Customer.FullName,
        ["{LoanRef}"]       = loan.LoanReference,
        ["{Amount}"]        = (installment?.AmountDue ?? loan.OutstandingBalance).ToString("N2"),
        ["{DueDate}"]       = installment?.DueDate.ToString("dd MMM yyyy") ?? string.Empty,
        ["{Balance}"]       = loan.OutstandingBalance.ToString("N2"),
        ["{PenaltyAmount}"] = (installment?.PenaltyAmount ?? 0).ToString("N2"),
        ["{TotalDue}"]      = (installment?.TotalAmountDue ?? loan.OutstandingBalance).ToString("N2"),
        ["{PaymentLink}"]   = $"https://pay.oyamfb.com/{loan.LoanReference}",
        ["{OfficerPhone}"]  = loan.LoanOfficer?.PhoneNumber ?? "08000000000",
        ["{AmountPaid}"]    = (installment?.AmountPaid ?? 0).ToString("N2"),
        ["{PaymentDate}"]   = installment?.PaymentDate?.ToString("dd MMM yyyy") ?? string.Empty,
        ["{BankName}"]      = "Oya Micro Finance Bank"
    };

    private static string RenderTemplate(
        string body,
        Dictionary<string, string> values)
    {
        var result = body;
        foreach (var (key, val) in values)
            result = result.Replace(key, val, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static void MarkReminderSentFlag(
        RepaymentSchedule schedule,
        int daysUntilDue)
    {
        switch (daysUntilDue)
        {
            case 7: schedule.ReminderSentD7 = true; break;
            case 3: schedule.ReminderSentD3 = true; break;
            case 1: schedule.ReminderSentD1 = true; break;
        }
    }

    private static NotificationLogResponse MapToLogResponse(NotificationLog n) =>
        new(
            n.Id,
            n.LoanId,
            n.Loan.LoanReference,
            n.Loan.Customer.FullName,
            n.RecipientPhone,
            n.Channel,
            n.NotificationType,
            n.Status,
            n.RenderedMessage,
            n.TriggeredBy,
            n.TermiiMessageId,
            n.RetryCount,
            n.FailureReason,
            n.CreatedAt,
            n.SentAt,
            n.DeliveredAt);
}