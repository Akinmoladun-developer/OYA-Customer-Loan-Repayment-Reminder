using System.ComponentModel.DataAnnotations;
using OyaMicroCreditCLRRS.Domain.Enums;

namespace OyaMicroCreditCLRRS.Models.Requests;

// Customer

public record CreateCustomerRequest(
    [Required][MaxLength(200)] string FullName,
    [Required][MaxLength(15)] string PhoneNumber,
    [EmailAddress][MaxLength(255)] string? Email,
    [Required][MaxLength(64)] string BvnHash,
    NotificationChannel PreferredChannel = NotificationChannel.Sms);

public record UpdateCustomerRequest(
    [MaxLength(200)] string? FullName,
    [EmailAddress][MaxLength(255)] string? Email,
    NotificationChannel? PreferredChannel);

// Loan

public record CreateLoanRequest(
    [Required] Guid CustomerId,
    [Required] Guid LoanProductId,
    [Required] string LoanOfficerId,
    [Required][Range(1000, 50_000_000)] decimal PrincipalAmount,
    [Required][Range(0.01, 30.0)] decimal MonthlyInterestRate,
    [Required][Range(1, 360)] int TenorMonths,
    [Required] DateOnly DisbursementDate);

public record RecordPaymentRequest(
    [Required][Range(1, double.MaxValue)] decimal AmountPaid,
    [Required] DateOnly PaymentDate,
    string? PaymentReference);

public record RestructureLoanRequest(
    [Required] DateOnly NewMaturityDate,
    string? Reason);

// Notifications

public record SendReminderRequest(
    [Required] NotificationType NotificationType,
    [Required] NotificationChannel Channel,
    Guid? ScheduleId);

public record BulkSendRequest(
    [Required] IEnumerable<Guid> LoanIds,
    [Required] NotificationType NotificationType,
    NotificationChannel Channel = NotificationChannel.Sms);

public record TermiiDlrWebhookRequest(
    string MessageId,
    string Status,
    string? PhoneNumber);

// Templates

public record CreateTemplateRequest(
    [Required][MaxLength(100)] string Name,
    [Required] NotificationChannel Channel,
    [Required] NotificationType NotificationType,
    [Required] string Body,
    string? SubjectLine);

public record UpdateTemplateRequest(
    [Required] string Body,
    string? SubjectLine);

// Auth

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password);

public record CreateStaffUserRequest(
    [Required][MaxLength(200)] string FullName,
    [Required][EmailAddress] string Email,
    [Required] string Password,
    [Required] UserRole Role,
    string? BranchCode);

// Config

public record UpdateReminderConfigRequest(
    [Required] int[] PreDueDays,
    [Required] int[] OverdueDays,
    [Range(0, 23)] int BlackoutStartHour = 21,
    [Range(0, 23)] int BlackoutEndHour = 7,
    bool EnableSms = true,
    bool EnableEmail = true,
    bool EnableWhatsApp = false);