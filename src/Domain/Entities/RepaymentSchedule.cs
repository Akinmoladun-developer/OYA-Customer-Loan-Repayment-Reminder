using OyaMicroCreditCLRRS.Domain.Enums;

namespace OyaMicroCreditCLRRS.Domain.Entities;


// This is for a single installment in a loan's repayment schedule.
// To Track reminder-sent flags to prevent duplicate notifications.

public class RepaymentSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LoanId { get; set; }

    // 1-based sequence number within the loan.
    public int InstallmentNumber { get; set; }

    public DateOnly DueDate { get; set; }

    // EMI amount: principal slice + interest for this period.
    public decimal AmountDue { get; set; }

    public decimal AmountPaid { get; set; } = 0m;

    /// <summary>Late penalty applied once installment becomes overdue.</summary>
    public decimal PenaltyAmount { get; set; } = 0m;

    public DateOnly? PaymentDate { get; set; }
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending;

    //  Reminder sent flags This to prevent idempotency
    public bool ReminderSentD7 { get; set; } = false;
    public bool ReminderSentD3 { get; set; } = false;
    public bool ReminderSentD1 { get; set; } = false;
    public bool ReminderSentDueToday { get; set; } = false;

    // Navigation
    public Loan Loan { get; set; } = default!;

    // Helpers

    public decimal TotalAmountDue => AmountDue + PenaltyAmount;
    public decimal AmountRemaining => Math.Max(0, TotalAmountDue - AmountPaid);
    public bool IsFullyPaid => AmountRemaining == 0;
}