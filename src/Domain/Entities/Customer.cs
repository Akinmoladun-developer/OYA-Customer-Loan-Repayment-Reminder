using OyaMicroCreditCLRRS.Domain.Enums;

namespace OyaMicroCreditCLRRS.Domain.Entities;


// This represents a customer/borrower registered with Oya Micro Finance Bank.
// NDPR: BVN stored as SHA-256 hash. Phone masked in all logs/exports.

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = default!;

    // Normalised Nigerian phone: 2348012345678
    public string PhoneNumber { get; set; } = default!;

    public string? Email { get; set; }

    // SHA-256 hash of BVN — never store raw BVN.
    public string BvnHash { get; set; } = default!;

    // Setting Customer's preferred contact channel for reminders.
    public NotificationChannel PreferredChannel { get; set; } = NotificationChannel.Sms;

    // True if customer sent STOP reply. In this case, customer stops receiving SMS (NDPR).
    public bool SmsOptOut { get; set; } = false;

    public bool EmailOptOut { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Soft delete to preserve audit history per CBN 7-year retention rule.
    public bool IsDeleted { get; set; } = false;

    // Navigation
    public ICollection<Loan> Loans { get; set; } = [];
}