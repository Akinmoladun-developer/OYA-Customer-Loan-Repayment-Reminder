namespace OyaMicroCreditCLRRS.Domain.Enums;

public enum LoanStatus
{
    Active = 1,
    Overdue = 2,
    Restructured = 3,
    Settled = 4,
    WrittenOff = 5
}

public enum InstallmentStatus
{
    Pending = 1,
    Paid = 2,
    PartiallyPaid = 3,
    Overdue = 4,
    Waived = 5
}

public enum NotificationChannel
{
    Sms = 1,
    Email = 2,
    WhatsApp = 3,
    InApp = 4
}

public enum NotificationType
{
    PreDueReminder = 1,
    DueTodayReminder = 2,
    OverdueReminder = 3,
    PaymentConfirmation = 4,
    PartialPaymentAcknowledgement = 5,
    RestructureNotification = 6,
    EscalationNotice = 7
}

public enum NotificationStatus
{
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    Retrying = 5
}

public enum UserRole
{
    Admin = 1,
    BranchManager = 2,
    LoanOfficer = 3,
    Operations = 4
}