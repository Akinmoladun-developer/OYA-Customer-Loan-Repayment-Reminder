using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OyaMicroCreditCLRRS.Domain.Entities;
using OyaMicroCreditCLRRS.Data.Configurations;

namespace OyaMicroCreditCLRRS.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<RepaymentSchedule> RepaymentSchedules => Set<RepaymentSchedule>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<LoanProduct> LoanProducts => Set<LoanProduct>();
    public DbSet<ReminderConfiguration> ReminderConfigurations => Set<ReminderConfiguration>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Must call — applies Identity table configs

        // Apply all IEntityTypeConfiguration classes from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Rename Identity tables to match Oya naming convention
        builder.Entity<AppUser>().ToTable("StaffUsers");
    }

    // Automatically sets UpdatedAt on every SaveChanges call.
  
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        SetAuditTimestamps();
        return base.SaveChanges();
    }

    private void SetAuditTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is Customer c) c.UpdatedAt = now;
                if (entry.Entity is Loan l) l.UpdatedAt = now;
                if (entry.Entity is MessageTemplate t) t.UpdatedAt = now;
                if (entry.Entity is ReminderConfiguration r) r.UpdatedAt = now;
            }
        }
    }
}