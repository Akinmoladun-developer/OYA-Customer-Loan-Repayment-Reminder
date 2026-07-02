using OyaMicroCreditCLRRS.Domain.Enums;

namespace OyaMicroCreditCLRRS.Domain.Entities;


// Versioned, admin-managed message template used by the notification engine.
// Placeholders Supported: {CustomerName, LoanRef, Amount, DueDate, Balance, PenaltyAmount, TotalDue, PaymentLink...
// OfficerPhone, AmountPaid, PaymentDate, BankName}

// Only one template may be active for each Channel plus the NotificationType.
public class MessageTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Friendly name e.g. SMS_PreDue_D7
    public string Name { get; set; } = default!;

    public NotificationChannel Channel { get; set; }
    public NotificationType NotificationType { get; set; }

    /// <summary>Template body with {Placeholder} tokens.</summary>
    public string Body { get; set; } = default!;

    // Email subject line — required for email channel, but null for SMS.
    public string? SubjectLine { get; set; }

    // Only one template active per Channel + NotificationType combination.
    public bool IsActive { get; set; } = false;

    public int Version { get; set; } = 1;
    public string CreatedBy { get; set; } = default!;
    public string? LastUpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    public ICollection<NotificationLog> NotificationLogs { get; set; } = [];
}