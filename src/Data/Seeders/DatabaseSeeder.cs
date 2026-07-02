using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OyaMicroCreditCLRRS.Domain.Entities;
using OyaMicroCreditCLRRS.Domain.Enums;

namespace OyaMicroCreditCLRRS.Data.Seeders;


// Seeding essential startup data:
// - Default admin user
// - Default loan product
// - Default reminder configuration
// - Default message templates for all notification types

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            await db.Database.MigrateAsync();
            await SeedAdminUserAsync(userManager, logger);
            await SeedLoanProductAsync(db, logger);
            await SeedReminderConfigAsync(db, logger);
            await SeedMessageTemplatesAsync(db, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during database seeding.");
            throw;
        }
    }

    private static async Task SeedAdminUserAsync(UserManager<AppUser> userManager, ILogger logger)
    {
        const string adminEmail = "admin@oyamfb.com";
        if (await userManager.FindByEmailAsync(adminEmail) is not null) return;

        var admin = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "OyaMicroCredit System Admin",
            Role = UserRole.Admin,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(admin, "OyaMFB@Admin2026!");
        if (result.Succeeded)
            logger.LogInformation("Default admin user created: {Email}", adminEmail);
        else
            logger.LogWarning("Failed to create admin: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private static async Task SeedLoanProductAsync(AppDbContext db, ILogger logger)
    {
        if (await db.LoanProducts.AnyAsync()) return;

        db.LoanProducts.Add(new LoanProduct
        {
            Name = "General Microloan",
            Code = "GML-001",
            Description = "Standard Oya Micro Finance general-purpose loan",
            DefaultMonthlyInterestRate = 0.035m, // 3.5% p.m.
            MinTenorMonths = 1,
            MaxTenorMonths = 24,
            MinLoanAmount = 10_000m,
            MaxLoanAmount = 5_000_000m,
            DailyPenaltyRate = 0.001m, // 0.1% per day
            IsActive = true
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Default loan product seeded.");
    }

    private static async Task SeedReminderConfigAsync(AppDbContext db, ILogger logger)
    {
        if (await db.ReminderConfigurations.AnyAsync(r => r.LoanProductId == null)) return;

        db.ReminderConfigurations.Add(new ReminderConfiguration
        {
            LoanProductId = null, // Global default
            PreDueDaysJson = "[7,3,1]",
            OverdueDaysJson = "[1,3,7,14]",
            BlackoutStartHour = 21, // 9 PM WAT
            BlackoutEndHour = 7,    // 7 AM WAT
            EnableSms = true,
            EnableEmail = true,
            EnableWhatsApp = false
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Default reminder configuration seeded.");
    }

    private static async Task SeedMessageTemplatesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.MessageTemplates.AnyAsync()) return;

        var templates = new List<MessageTemplate>
        {
            new()
            {
                Name = "SMS_PreDue_D7",
                Channel = NotificationChannel.Sms,
                NotificationType = NotificationType.PreDueReminder,
                Body = "Dear {CustomerName}, friendly reminder from OyaMFB. Your loan repayment of NGN {Amount} (Ref: {LoanRef}) is due in 7 days on {DueDate}. Pay: {PaymentLink}. Help: {OfficerPhone}.",
                IsActive = true,
                CreatedBy = "System"
            },
            new()
            {
                Name = "SMS_PreDue_D3",
                Channel = NotificationChannel.Sms,
                NotificationType = NotificationType.PreDueReminder,
                Body = "Dear {CustomerName}, your OyaMFB loan of NGN {Amount} (Ref: {LoanRef}) is due in 3 days on {DueDate}. Please pay now: {PaymentLink}. Call: {OfficerPhone}.",
                IsActive = false,
                CreatedBy = "System"
            },
            new()
            {
                Name = "SMS_PreDue_D1",
                Channel = NotificationChannel.Sms,
                NotificationType = NotificationType.PreDueReminder,
                Body = "REMINDER: Dear {CustomerName}, your OyaMFB loan of NGN {Amount} (Ref: {LoanRef}) is due TOMORROW {DueDate}. Pay now: {PaymentLink}. Call: {OfficerPhone}.",
                IsActive = false,
                CreatedBy = "System"
            },
            new()
            {
                Name = "SMS_DueToday",
                Channel = NotificationChannel.Sms,
                NotificationType = NotificationType.DueTodayReminder,
                Body = "Dear {CustomerName}, your OyaMFB loan payment of NGN {Amount} (Ref: {LoanRef}) is DUE TODAY {DueDate}. Avoid penalty. Pay: {PaymentLink}. Call: {OfficerPhone}.",
                IsActive = true,
                CreatedBy = "System"
            },
            new()
            {
                Name = "SMS_Overdue_Gentle",
                Channel = NotificationChannel.Sms,
                NotificationType = NotificationType.OverdueReminder,
                Body = "Dear {CustomerName}, your OyaMFB loan payment of NGN {Amount} (Ref: {LoanRef}) was due {DueDate} and is unpaid. Please pay immediately: {PaymentLink}. Call: {OfficerPhone}.",
                IsActive = true,
                CreatedBy = "System"
            },
            new()
            {
                Name = "SMS_Overdue_Urgent",
                Channel = NotificationChannel.Sms,
                NotificationType = NotificationType.EscalationNotice,
                Body = "URGENT - OyaMFB: Dear {CustomerName}, your loan (Ref: {LoanRef}) is 14 days overdue. Total due: NGN {TotalDue} including penalty NGN {PenaltyAmount}. Your account has been escalated. Pay: {PaymentLink} or call {OfficerPhone} immediately.",
                IsActive = true,
                CreatedBy = "System"
            },
            new()
            {
                Name = "SMS_PaymentConfirm",
                Channel = NotificationChannel.Sms,
                NotificationType = NotificationType.PaymentConfirmation,
                Body = "Dear {CustomerName}, OyaMFB has received NGN {AmountPaid} for Loan Ref: {LoanRef} on {PaymentDate}. Outstanding balance: NGN {Balance}. Thank you.",
                IsActive = true,
                CreatedBy = "System"
            },
            new()
            {
                Name = "SMS_Restructure",
                Channel = NotificationChannel.Sms,
                NotificationType = NotificationType.RestructureNotification,
                Body = "Dear {CustomerName}, your OyaMFB loan (Ref: {LoanRef}) has been restructured. Please check your new repayment schedule. Call {OfficerPhone} for details.",
                IsActive = true,
                CreatedBy = "System"
            }
        };

        db.MessageTemplates.AddRange(templates);
        await db.SaveChangesAsync();
        logger.LogInformation("Default message templates seeded ({Count}).", templates.Count);
    }
}