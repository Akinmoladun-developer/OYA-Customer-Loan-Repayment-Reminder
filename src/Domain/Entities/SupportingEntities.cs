using OyaMicroCreditCLRRS.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace OyaMicroCreditCLRRS.Domain.Entities;

// Per-product (or global default) configuration for the reminder engine.
// Product-specific config overrides the global default.

public class ReminderConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Null = global default. Non-null = product-specific override.
    public Guid? LoanProductId { get; set; }

    // JSON-serialised list e.g. [7,3,1] — days before due to send reminders.
    public string PreDueDaysJson { get; set; } = "[7,3,1]";

    // JSON-serialised list e.g. [1,3,7,14] — days after due to escalate.
    public string OverdueDaysJson { get; set; } = "[1,3,7,14]";

    /// <summary>WAT hour (24h) after which no SMS is sent. Default: 21 (9 PM).</summary>
    public int BlackoutStartHour { get; set; } = 21;

    /// <summary>WAT hour (24h) from which sending resumes. Default: 7 (7 AM).</summary>
    public int BlackoutEndHour { get; set; } = 7;

    public bool EnableSms { get; set; } = true;
    public bool EnableEmail { get; set; } = true;
    public bool EnableWhatsApp { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public LoanProduct? LoanProduct { get; set; }
}

//  Loan product definition — e.g. "SME Loan 6M", "Agricultural Loan 12M".
//  Controls EMI calculation method and maps to reminder configuration.

public class LoanProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Description { get; set; } = string.Empty;
    public decimal DefaultMonthlyInterestRate { get; set; }
    public int MinTenorMonths { get; set; }
    public int MaxTenorMonths { get; set; }
    public decimal MinLoanAmount { get; set; }
    public decimal MaxLoanAmount { get; set; }

    // <summary>Penalty rate applied per day overdue as % of outstanding balance.</summary>
    public decimal DailyPenaltyRate { get; set; } = 0.1m;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public ICollection<Loan> Loans { get; set; } = [];
    public ReminderConfiguration? ReminderConfiguration { get; set; }
}

// Extends ASP.NET Core Identity user with Oya-specific staff fields.
// Authenticated users: { loan officers, branch managers, admins, and operations staff}

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = default!;
    public UserRole Role { get; set; }
    public string? BranchCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public ICollection<Loan> AssignedLoans { get; set; } = [];
}