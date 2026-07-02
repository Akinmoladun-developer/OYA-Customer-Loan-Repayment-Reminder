using OyaMicroCreditCLRRS.Domain.Enums;

namespace OyaMicroCreditCLRRS.Domain.Entities;

// This details records of microloan issued by Oya Micro Finance Bank. 
// And customer/borrower repayment schedule collection.
public class Loan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid LoanProductId { get; set; }
    public string LoanOfficerId { get; set; } = default!; // FK → AspNetUsers.Id

    // refference number: OYA-2026-00123
    public string LoanReference { get; set; } = default!;

    // Original amount disbursed to customer/borrower. Currency in Nigerian NGN
    public decimal PrincipalAmount { get; set; }

    // Current remaining balance which will be updated on each payment/repayment
    public decimal OutstandingBalance { get; set; }

    // Monthly interest rate as a percentage { 3.5% }
    public decimal MonthlyInterestRate { get; set; }

    public int TenorMonths { get; set; }
    public DateOnly DisbursementDate { get; set; }
    public DateOnly MaturityDate { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    public Customer Customer { get; set; } = default!;
    public LoanProduct LoanProduct { get; set; } = default!;
    public AppUser LoanOfficer { get; set; } = default!;
    public ICollection<RepaymentSchedule> RepaymentSchedules { get; set; } = [];
    public ICollection<NotificationLog> NotificationLogs { get; set; } = [];
}