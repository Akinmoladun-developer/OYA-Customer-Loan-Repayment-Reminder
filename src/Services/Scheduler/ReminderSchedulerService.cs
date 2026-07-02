using Microsoft.EntityFrameworkCore;
using OyaMicroCreditCLRRS.Data;
using OyaMicroCreditCLRRS.Domain.Enums;
using OyaMicroCreditCLRRS.Helpers;
using OyaMicroCreditCLRRS.Services.Interfaces;
using System.Text.Json;

namespace OyaMicroCreditCLRRS.Services.Scheduler;


// Setting up the backgroung job with Hangfire that runs the four daily reminder scan jobs.
// SRP: this class only decides WHEN and WHICH loans to remind.
// HOW to send is fully delegated to INotificationService.

public class ReminderSchedulerService : IReminderSchedulerService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ReminderSchedulerService> _logger;

    public ReminderSchedulerService(
        AppDbContext db,
        INotificationService notificationService,
        ILogger<ReminderSchedulerService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _logger = logger;
    }

    // Job 1: Pre-due reminders 
    // Hangfire : "30 7 * * *" → 7:30 AM WAT daily

    public async Task RunPreDueReminderJobAsync()
    {
        _logger.LogInformation(
            "[PreDueJob] Starting at {Time} WAT", WatClock.Now);

        var config = await GetEffectiveConfigAsync();

        if (!IsWithinSendWindow(config))
        {
            _logger.LogInformation(
                "[PreDueJob] Current hour {Hour} WAT is outside send window. Skipping.",
                WatClock.CurrentHour);
            return;
        }

        var today = WatClock.Today;

        foreach (var daysAhead in config.PreDueDays)
        {
            var targetDate = today.AddDays(daysAhead);

            // Find all pending installments due on the target date
            // whose reminder flag for this day interval hasn't been set yet
            var schedules = await _db.RepaymentSchedules
                .Include(s => s.Loan)
                    .ThenInclude(l => l.Customer)
                .Where(s =>
                    s.DueDate == targetDate &&
                    s.Status == InstallmentStatus.Pending &&
                    !s.Loan.Customer.SmsOptOut &&
                    !s.Loan.Customer.IsDeleted &&
                    s.Loan.Status == LoanStatus.Active)
                .ToListAsync();

            // Filter in memory using the reminder flags
            var due = schedules.Where(s => daysAhead switch
            {
                7 => !s.ReminderSentD7,
                3 => !s.ReminderSentD3,
                1 => !s.ReminderSentD1,
                _ => false
            }).ToList();

            _logger.LogInformation(
                "[PreDueJob] D-{Days}: {Count} installments to remind.",
                daysAhead, due.Count);

            foreach (var schedule in due)
            {
                try
                {
                    // Respect customer's preferred channel
                    var channel = schedule.Loan.Customer.PreferredChannel
                        == NotificationChannel.Email
                            ? NotificationChannel.Email
                            : NotificationChannel.Sms;

                    await _notificationService.SendReminderAsync(
                        schedule.LoanId,
                        NotificationType.PreDueReminder,
                        channel,
                        "System:PreDueJob",
                        schedule.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[PreDueJob] Failed for schedule {ScheduleId} on loan {LoanId}.",
                        schedule.Id, schedule.LoanId);
                }
            }
        }

        _logger.LogInformation("[PreDueJob] Completed at {Time} WAT.", WatClock.Now);
    }

    // Background Job 2: Due-today reminders
    // Hangfire : "0 8 * * *" → 8:00 AM WAT daily

    public async Task RunDueTodayReminderJobAsync()
    {
        _logger.LogInformation(
            "[DueTodayJob] Starting at {Time} WAT", WatClock.Now);

        var today = WatClock.Today;

        // Find all installments due today that haven't received
        // a due-today reminder yet and customer has not opted out
        var schedules = await _db.RepaymentSchedules
            .Include(s => s.Loan)
                .ThenInclude(l => l.Customer)
            //.Where(s =>
                //s.DueDate == today &&
                //s.Status is InstallmentStatus.Pending or InstallmentStatus.PartiallyPaid &&
                //!s.ReminderSentDueToday &&
                //!s.Loan.Customer.SmsOptOut &&
                //!s.Loan.Customer.IsDeleted)
            //.ToListAsync();

            // Note Behind(N.B) 
            // Due to EF Limitation, the "is-Pattern" can be translated to SQL


            .Where(s =>
                s.DueDate == today &&
                (s.Status == InstallmentStatus.Pending ||
                s.Status == InstallmentStatus.PartiallyPaid) &&
                !s.ReminderSentDueToday &&
                !s.Loan.Customer.SmsOptOut &&
                !s.Loan.Customer.IsDeleted)
            .ToListAsync();

        _logger.LogInformation(
            "[DueTodayJob] {Count} installments due today.", schedules.Count);

        foreach (var schedule in schedules)
        {
            try
            {
                // Due-today always goes via SMS regardless of preference
                // This is the highest-priority reminder — must reach the customer
                await _notificationService.SendReminderAsync(
                    schedule.LoanId,
                    NotificationType.DueTodayReminder,
                    NotificationChannel.Sms,
                    "System:DueTodayJob",
                    schedule.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[DueTodayJob] Failed for schedule {ScheduleId} on loan {LoanId}.",
                    schedule.Id, schedule.LoanId);
            }
        }

        _logger.LogInformation("[DueTodayJob] Completed at {Time} WAT.", WatClock.Now);
    }

    // Background Job 3: Overdue reminders 
    // Hangfire : "30 8 * * *" → 8:30 AM WAT daily

    public async Task RunOverdueReminderJobAsync()
    {
        _logger.LogInformation(
            "[OverdueJob] Starting at {Time} WAT", WatClock.Now);

        var config = await GetEffectiveConfigAsync();

        if (!IsWithinSendWindow(config))
        {
            _logger.LogInformation(
                "[OverdueJob] Current hour {Hour} WAT is outside send window. Skipping.",
                WatClock.CurrentHour);
            return;
        }

        var today = WatClock.Today;

        foreach (var daysOverdue in config.OverdueDays)
        {
            // The target date is the due date that was missed
            var targetDate = today.AddDays(-daysOverdue);

            var schedules = await _db.RepaymentSchedules
                .Include(s => s.Loan)
                    .ThenInclude(l => l.Customer)
                .Include(s => s.Loan)
                    .ThenInclude(l => l.LoanProduct)
                //.Where(s =>
                   // s.DueDate == targetDate &&
                    //s.Status is
                        //InstallmentStatus.Pending or
                        //InstallmentStatus.PartiallyPaid or
                        //InstallmentStatus.Overdue &&
                    //!s.Loan.Customer.SmsOptOut &&
                   // !s.Loan.Customer.IsDeleted)
                //.ToListAsync();

                // EF Limitation cannot translate this expression with the "is Pattern" to SQL


                .Where(s =>
                    s.DueDate == targetDate &&
                    (s.Status == InstallmentStatus.Pending ||
                    s.Status == InstallmentStatus.PartiallyPaid ||
                    s.Status == InstallmentStatus.Overdue) &&
                    !s.Loan.Customer.SmsOptOut &&
                    !s.Loan.Customer.IsDeleted)
                .ToListAsync();

            _logger.LogInformation(
                "[OverdueJob] D+{Days}: {Count} overdue installments found.",
                daysOverdue, schedules.Count);

            foreach (var schedule in schedules)
            {
                // Mark installment and loan as overdue if not already
                if (schedule.Status != InstallmentStatus.Overdue)
                {
                    schedule.Status = InstallmentStatus.Overdue;
                    schedule.Loan.Status = LoanStatus.Overdue;
                }

                // Apply daily penalty if product has one configured
                var dailyRate = schedule.Loan.LoanProduct?.DailyPenaltyRate ?? 0.001m;
                var penalty = schedule.AmountDue * dailyRate * daysOverdue;
                schedule.PenaltyAmount = Math.Round(penalty, 2, MidpointRounding.AwayFromZero);

                // D+14 and beyond → escalation notice to loan officer
                var notificationType = daysOverdue >= 14
                    ? NotificationType.EscalationNotice
                    : NotificationType.OverdueReminder;

                try
                {
                    await _notificationService.SendReminderAsync(
                        schedule.LoanId,
                        notificationType,
                        NotificationChannel.Sms,
                        "System:OverdueJob",
                        schedule.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[OverdueJob] Failed for schedule {ScheduleId} on loan {LoanId}.",
                        schedule.Id, schedule.LoanId);
                }
            }

            // Persist status and penalty updates for this day-bucket
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("[OverdueJob] Completed at {Time} WAT.", WatClock.Now);
    }

    // Background Job 4: Retry failed notifications
    // Hangfire : "0/30 * * * *" → every 30 minutes

    public async Task RunRetryJobAsync()
    {
        _logger.LogInformation(
            "[RetryJob] Scanning for retryable notifications at {Time} WAT.", WatClock.Now);

        // Pick up to 100 notifications stuck in Retrying state
        // that haven't exhausted their 3-attempt limit
        var retryable = await _db.NotificationLogs
            .Include(n => n.Loan)
                .ThenInclude(l => l.Customer)
            .Where(n =>
                n.Status == NotificationStatus.Retrying &&
                n.RetryCount < 3)
            .OrderBy(n => n.CreatedAt)
            .Take(100)
            .ToListAsync();

        _logger.LogInformation(
            "[RetryJob] {Count} notifications queued for retry.", retryable.Count);

        foreach (var log in retryable)
        {
            try
            {
                await _notificationService.SendReminderAsync(
                    log.LoanId,
                    log.NotificationType,
                    log.Channel,
                    "System:RetryJob",
                    log.ScheduleId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[RetryJob] Retry failed for NotificationLog {Id}.", log.Id);
            }
        }

        _logger.LogInformation("[RetryJob] Completed at {Time} WAT.", WatClock.Now);
    }

    // Private helpers

    // Reads the global reminder configuration from the database.
    // Falls back to hardcoded defaults if none exists.
   
    private async Task<ReminderConfigDto> GetEffectiveConfigAsync()
    {
        var config = await _db.ReminderConfigurations
            .FirstOrDefaultAsync(r => r.LoanProductId == null);

        if (config is null)
        {
            _logger.LogWarning(
                "No global reminder configuration found. Using hardcoded defaults.");

            return new ReminderConfigDto(
                PreDueDays: [7, 3, 1],
                OverdueDays: [1, 3, 7, 14],
                BlackoutStartHour: 21,
                BlackoutEndHour: 7);
        }

        return new ReminderConfigDto(
            PreDueDays: JsonSerializer.Deserialize<int[]>(config.PreDueDaysJson) ?? [7, 3, 1],
            OverdueDays: JsonSerializer.Deserialize<int[]>(config.OverdueDaysJson) ?? [1, 3, 7, 14],
            BlackoutStartHour: config.BlackoutStartHour,
            BlackoutEndHour: config.BlackoutEndHour);
    }

    // Returns true if the current WAT hour is within the allowed send window.
    // Blackout wraps midnight: e.g. start=21, end=7 means blocked from 9 PM to 7 AM.
   
    private static bool IsWithinSendWindow(ReminderConfigDto config)
    {
        var hour = WatClock.CurrentHour;

        // Normal case: blackout wraps midnight (start > end)
        // Allowed window: end <= hour < start
        // e.g. start=21, end=7 → allowed between 07:00 and 20:59
        return hour >= config.BlackoutEndHour && hour < config.BlackoutStartHour;
    }

    // Internal DTO — used only within this service
    private record ReminderConfigDto(
        int[] PreDueDays,
        int[] OverdueDays,
        int BlackoutStartHour,
        int BlackoutEndHour);
}