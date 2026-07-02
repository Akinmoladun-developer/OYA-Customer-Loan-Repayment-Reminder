using OyaMicroCreditCLRRS.Domain.Enums;

namespace OyaMicroCreditCLRRS.Domain.Entities;

// This is for audit record of every notification dispatched by the system.
// Following CBN requirement to logs retained for 7 years. Only soft delete is allowed on the system.
// Phone numbers stored masked. Status updated only via Termii DLR webhook.

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LoanId { get; set; }
    public Guid CustomerId { get; set; }

    // Nullable — restructure/escalation notice
    public Guid? ScheduleId { get; set; }

    public Guid TemplateId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationType NotificationType { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Queued;

    // NDPR: stored masked e.g. 234801****78</summary>
    public string RecipientPhone { get; set; } = default!;

    // For fully rendered message body. Placeholder will be substituted</summary>
    public string RenderedMessage { get; set; } = default!;

    // System for CRON jobs, UserId for manual sends.
    public string TriggeredBy { get; set; } = default!;

    // Termii's message ID to match inbound DLR webhooks.
    public string? TermiiMessageId { get; set; }

    public int RetryCount { get; set; } = 0;
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    // Navigation
    public Loan Loan { get; set; } = default!;
    public MessageTemplate Template { get; set; } = default!;
}