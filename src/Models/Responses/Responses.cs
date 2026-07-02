using OyaMicroCreditCLRRS.Domain.Enums;

namespace OyaMicroCreditCLRRS.Models.Responses;

// Shared

public record ApiResponse<T>(bool Success, string? Message, T? Data)
{
    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new(true, message, data);

    public static ApiResponse<T> Fail(string message) =>
        new(false, message, default);
}

public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

// Customer

public record CustomerResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,        // masked in all responses
    string? Email,
    NotificationChannel PreferredChannel,
    bool SmsOptOut,
    bool EmailOptOut,
    DateTime CreatedAt);

// Loan

public record LoanResponse(
    Guid Id,
    string LoanReference,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    decimal PrincipalAmount,
    decimal OutstandingBalance,
    decimal MonthlyInterestRate,
    int TenorMonths,
    DateOnly DisbursementDate,
    DateOnly MaturityDate,
    LoanStatus Status,
    string LoanOfficerName,
    DateTime CreatedAt);

public record RepaymentScheduleResponse(
    Guid Id,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal AmountDue,
    decimal AmountPaid,
    decimal PenaltyAmount,
    decimal AmountRemaining,
    InstallmentStatus Status,
    DateOnly? PaymentDate);

// Notification

public record NotificationLogResponse(
    Guid Id,
    Guid LoanId,
    string LoanReference,
    string CustomerName,
    string RecipientPhone,
    NotificationChannel Channel,
    NotificationType NotificationType,
    NotificationStatus Status,
    string RenderedMessage,
    string TriggeredBy,
    string? TermiiMessageId,
    int RetryCount,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeliveredAt);

// Template 

public record TemplateResponse(
    Guid Id,
    string Name,
    NotificationChannel Channel,
    NotificationType NotificationType,
    string Body,
    string? SubjectLine,
    bool IsActive,
    int Version,
    int SmsCharCount,
    bool IsMultiPartSms,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// Auth
public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    StaffUserResponse User);

public record StaffUserResponse(
    string Id,
    string FullName,
    string Email,
    UserRole Role,
    string? BranchCode,
    bool IsActive);

// Config

public record ReminderConfigResponse(
    Guid Id,
    Guid? LoanProductId,
    int[] PreDueDays,
    int[] OverdueDays,
    int BlackoutStartHour,
    int BlackoutEndHour,
    bool EnableSms,
    bool EnableEmail,
    bool EnableWhatsApp,
    DateTime UpdatedAt);

// Reports

public record DashboardResponse(
    int TotalActiveLoans,
    int TotalOverdueLoans,
    decimal TotalOutstandingBalance,
    int RemindersSentToday,
    int RemindersSentThisMonth,
    int DeliveredCount,
    int FailedCount,
    int Par1Count,
    int Par7Count,
    int Par30Count,
    int Par90Count);

public record ParReportRow(
    string LoanReference,
    string CustomerName,
    string CustomerPhone,
    decimal OutstandingBalance,
    int DaysAtRisk,
    DateOnly OldestDueDate);