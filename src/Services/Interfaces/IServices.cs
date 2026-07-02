using OyaMicroCreditCLRRS.Domain.Entities;
using OyaMicroCreditCLRRS.Domain.Enums;
using OyaMicroCreditCLRRS.Models.Requests;
using OyaMicroCreditCLRRS.Models.Responses;

namespace OyaMicroCreditCLRRS.Services.Interfaces;

// ICustomerService

public interface ICustomerService
{
    Task<CustomerResponse> GetByIdAsync(Guid id);
    Task<CustomerResponse> GetByPhoneAsync(string phone);
    Task<PagedResult<CustomerResponse>> GetAllAsync(int page, int pageSize, string? search);
    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request);
    Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request);
    Task OptOutAsync(Guid id, NotificationChannel channel);
    Task OptInAsync(Guid id, NotificationChannel channel);
    Task DeleteAsync(Guid id);
}

// ILoanService

public interface ILoanService
{
    Task<LoanResponse> GetByIdAsync(Guid id);
    Task<LoanResponse> GetByReferenceAsync(string reference);
    Task<PagedResult<LoanResponse>> GetByCustomerAsync(Guid customerId, int page, int pageSize);
    Task<PagedResult<LoanResponse>> GetAllAsync(int page, int pageSize, LoanStatus? status, string? search);
    Task<LoanResponse> CreateAsync(CreateLoanRequest request);
    Task<LoanResponse> RecordPaymentAsync(Guid loanId, RecordPaymentRequest request);
    Task<LoanResponse> RestructureAsync(Guid loanId, RestructureLoanRequest request);
    Task<IEnumerable<RepaymentScheduleResponse>> GetScheduleAsync(Guid loanId);
    Task<string> GenerateLoanReferenceAsync();
}

// INotificationService

public interface INotificationService
{
    /// <summary>Send a reminder for a specific loan and notification type.</summary>
    Task<NotificationLog> SendReminderAsync(
        Guid loanId,
        NotificationType type,
        NotificationChannel channel,
        string triggeredBy,
        Guid? scheduleId = null);

    // Bulk send reminders to multiple loans.
    Task BulkSendAsync(IEnumerable<Guid> loanIds, NotificationType type, string triggeredBy);

    Task<PagedResult<NotificationLogResponse>> GetLogsAsync(
        int page, int pageSize,
        NotificationStatus? status,
        NotificationChannel? channel,
        NotificationType? type,
        DateTime? from, DateTime? to);

    Task<IEnumerable<NotificationLogResponse>> GetLoanHistoryAsync(Guid loanId);

    // Called by Termii DLR webhook — updates delivery status.
    Task ProcessDeliveryReceiptAsync(string termiiMessageId, string status);

    /// <summary>Process NDPR STOP reply from a customer.</summary>
    Task ProcessOptOutAsync(string phoneNumber);
}

// ITermiiGateway

public interface ITermiiGateway
{
    Task<TermiiSendResult> SendSmsAsync(string to, string message, string senderId);
    Task<TermiiSendResult> SendEmailAsync(string to, string subject, string message);
}

public record TermiiSendResult(
    bool Success,
    string? MessageId,
    string? ErrorMessage);

// ITemplateService

public interface ITemplateService
{
    Task<IEnumerable<TemplateResponse>> GetAllAsync();
    Task<TemplateResponse> GetByIdAsync(Guid id);
    Task<TemplateResponse> CreateAsync(CreateTemplateRequest request, string createdBy);
    Task<TemplateResponse> UpdateAsync(Guid id, UpdateTemplateRequest request, string updatedBy);
    Task ActivateAsync(Guid id, string activatedBy);
    Task<string> PreviewAsync(Guid id, Dictionary<string, string> sampleValues);
    Task<bool> TestSendAsync(Guid id, string toPhone, Dictionary<string, string> sampleValues);
}

// IReminderSchedulerService

public interface IReminderSchedulerService
{
    // Scans and sends pre-due reminders. Called by Hangfire at 7:30 AM WAT daily.
    Task RunPreDueReminderJobAsync();

    // Scans and sends due-today reminders. Called by Hangfire at 8:00 AM WAT daily.
    Task RunDueTodayReminderJobAsync();

    // Scans and sends overdue reminders. Called by Hangfire at 8:30 AM WAT daily.
    Task RunOverdueReminderJobAsync();

    // Retries failed notifications. Called by Hangfire every 30 minutes.
    Task RunRetryJobAsync();
}

// IAuthService

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task<StaffUserResponse> CreateStaffUserAsync(CreateStaffUserRequest request);
    Task<IEnumerable<StaffUserResponse>> GetAllStaffAsync();
    Task DeactivateStaffAsync(string userId);
}

// IReminderConfigService

public interface IReminderConfigService
{
    Task<ReminderConfigResponse> GetGlobalConfigAsync();
    Task<ReminderConfigResponse?> GetProductConfigAsync(Guid productId);
    Task<ReminderConfigResponse> GetEffectiveConfigAsync(Guid productId);
    Task<ReminderConfigResponse> UpdateGlobalConfigAsync(UpdateReminderConfigRequest request);
    Task<ReminderConfigResponse> UpsertProductConfigAsync(Guid productId, UpdateReminderConfigRequest request);
}

// IReportService

public interface IReportService
{
    Task<DashboardResponse> GetDashboardAsync(string? branchCode);
    Task<byte[]> ExportNotificationLogsCsvAsync(DateTime from, DateTime to);
    Task<IEnumerable<ParReportRow>> GetParReportAsync(DateOnly asOf);
}