using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OyaMicroCreditCLRRS.Domain.Entities;

namespace OyaMicroCreditCLRRS.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FullName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(15);
        builder.Property(c => c.Email).HasMaxLength(255);
        builder.Property(c => c.BvnHash).IsRequired().HasMaxLength(64);
        builder.Property(c => c.PreferredChannel).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => c.PhoneNumber).IsUnique();
        builder.HasIndex(c => c.BvnHash).IsUnique();

        // Global query filter — soft-deleted customers never appear in queries
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loans");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LoanReference).IsRequired().HasMaxLength(50);
        builder.Property(l => l.PrincipalAmount).HasColumnType("decimal(18,2)");
        builder.Property(l => l.OutstandingBalance).HasColumnType("decimal(18,2)");
        builder.Property(l => l.MonthlyInterestRate).HasColumnType("decimal(5,4)");
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(l => l.LoanReference).IsUnique();
        builder.HasIndex(l => l.CustomerId);
        builder.HasIndex(l => l.Status);

        builder.HasOne(l => l.Customer)
            .WithMany(c => c.Loans)
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.LoanProduct)
            .WithMany(p => p.Loans)
            .HasForeignKey(l => l.LoanProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.LoanOfficer)
            .WithMany(u => u.AssignedLoans)
            .HasForeignKey(l => l.LoanOfficerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RepaymentScheduleConfiguration : IEntityTypeConfiguration<RepaymentSchedule>
{
    public void Configure(EntityTypeBuilder<RepaymentSchedule> builder)
    {
        builder.ToTable("RepaymentSchedules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.AmountDue).HasColumnType("decimal(18,2)");
        builder.Property(r => r.AmountPaid).HasColumnType("decimal(18,2)");
        builder.Property(r => r.PenaltyAmount).HasColumnType("decimal(18,2)");
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(r => r.LoanId);
        builder.HasIndex(r => r.DueDate);
        builder.HasIndex(r => r.Status);

        // Compound index for the daily reminder scan query
        builder.HasIndex(r => new { r.DueDate, r.Status });

        builder.HasOne(r => r.Loan)
            .WithMany(l => l.RepaymentSchedules)
            .HasForeignKey(r => r.LoanId)
            .OnDelete(DeleteBehavior.Cascade);

        // Computed columns (ignored by EF — calculated in app layer)
        builder.Ignore(r => r.TotalAmountDue);
        builder.Ignore(r => r.AmountRemaining);
        builder.Ignore(r => r.IsFullyPaid);
    }
}

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.RecipientPhone).IsRequired().HasMaxLength(20);
        builder.Property(n => n.RenderedMessage).IsRequired();
        builder.Property(n => n.TriggeredBy).IsRequired().HasMaxLength(100);
        builder.Property(n => n.TermiiMessageId).HasMaxLength(100);
        builder.Property(n => n.FailureReason).HasMaxLength(500);
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.NotificationType).HasConversion<string>().HasMaxLength(40);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(n => n.LoanId);
        builder.HasIndex(n => n.CustomerId);
        builder.HasIndex(n => n.Status);
        builder.HasIndex(n => n.TermiiMessageId);
        builder.HasIndex(n => n.CreatedAt);

        builder.HasOne(n => n.Loan)
            .WithMany(l => l.NotificationLogs)
            .HasForeignKey(n => n.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Template)
            .WithMany(t => t.NotificationLogs)
            .HasForeignKey(n => n.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.ToTable("MessageTemplates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Body).IsRequired();
        builder.Property(t => t.SubjectLine).HasMaxLength(200);
        builder.Property(t => t.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(t => t.LastUpdatedBy).HasMaxLength(100);
        builder.Property(t => t.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.NotificationType).HasConversion<string>().HasMaxLength(40);

        // Enforce unique active template per Channel + NotificationType
        builder.HasIndex(t => new { t.Channel, t.NotificationType, t.IsActive })
            .HasFilter("[IsActive] = 1")
            .IsUnique();
    }
}

public class LoanProductConfiguration : IEntityTypeConfiguration<LoanProduct>
{
    public void Configure(EntityTypeBuilder<LoanProduct> builder)
    {
        builder.ToTable("LoanProducts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Code).IsRequired().HasMaxLength(20);
        builder.Property(p => p.DefaultMonthlyInterestRate).HasColumnType("decimal(5,4)");
        builder.Property(p => p.MinLoanAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.MaxLoanAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.DailyPenaltyRate).HasColumnType("decimal(5,4)");

        builder.HasIndex(p => p.Code).IsUnique();

        builder.HasOne(p => p.ReminderConfiguration)
            .WithOne(r => r.LoanProduct)
            .HasForeignKey<ReminderConfiguration>(r => r.LoanProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ReminderConfigurationEntityConfiguration : IEntityTypeConfiguration<ReminderConfiguration>
{
    public void Configure(EntityTypeBuilder<ReminderConfiguration> builder)
    {
        builder.ToTable("ReminderConfigurations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.PreDueDaysJson).IsRequired().HasMaxLength(100);
        builder.Property(r => r.OverdueDaysJson).IsRequired().HasMaxLength(100);
    }
}