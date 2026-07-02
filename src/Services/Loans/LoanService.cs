using Microsoft.EntityFrameworkCore;
using OyaMicroCreditCLRRS.Data;
using OyaMicroCreditCLRRS.Domain.Entities;
using OyaMicroCreditCLRRS.Domain.Enums;
using OyaMicroCreditCLRRS.Domain.Exceptions;
using OyaMicroCreditCLRRS.Helpers;
using OyaMicroCreditCLRRS.Models.Requests;
using OyaMicroCreditCLRRS.Models.Responses;
using OyaMicroCreditCLRRS.Services.Interfaces;

namespace OyaMicroCreditCLRRS.Services.Loans;

// This Handles all the Loan Lifecycle Operations
// creation, payment recording, restructuring, and schedule queries.
// Applying SOLID Principles:
// - SRP: no notification logic here — that belongs to INotificationService
// - DIP: depends on AppDbContext with constructor injection

public class LoanService : ILoanService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LoanService> _logger;

    public LoanService(AppDbContext db, ILogger<LoanService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Queries

    public async Task<LoanResponse> GetByIdAsync(Guid id)
    {
        var loan = await LoadLoanAsync(id);
        return MapToResponse(loan);
    }

    public async Task<LoanResponse> GetByReferenceAsync(string reference)
    {
        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanOfficer)
            .FirstOrDefaultAsync(l =>
                l.LoanReference == reference.ToUpperInvariant())
            ?? throw new NotFoundException(
                $"Loan with reference '{reference}' was not found.");

        return MapToResponse(loan);
    }

    public async Task<PagedResult<LoanResponse>> GetByCustomerAsync(
        Guid customerId, int page, int pageSize)
    {
        // Confirm customer exists first
        var customerExists = await _db.Customers
            .AnyAsync(c => c.Id == customerId);

        if (!customerExists)
            throw new NotFoundException("Customer", customerId);

        var query = _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanOfficer)
            .Where(l => l.CustomerId == customerId)
            .OrderByDescending(l => l.CreatedAt);

        return await ToPagedResultAsync(query, page, pageSize);
    }

    public async Task<PagedResult<LoanResponse>> GetAllAsync(
        int page, int pageSize, LoanStatus? status, string? search)
    {
        var query = _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanOfficer)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower().Trim();
            query = query.Where(l =>
                l.LoanReference.ToLower().Contains(term) ||
                l.Customer.FullName.ToLower().Contains(term) ||
                l.Customer.PhoneNumber.Contains(term));
        }

        query = query.OrderByDescending(l => l.CreatedAt);
        return await ToPagedResultAsync(query, page, pageSize);
    }

    public async Task<IEnumerable<RepaymentScheduleResponse>> GetScheduleAsync(Guid loanId)
    {
        // Confirm loan exists
        var loanExists = await _db.Loans.AnyAsync(l => l.Id == loanId);
        if (!loanExists)
            throw new NotFoundException("Loan", loanId);

        var schedules = await _db.RepaymentSchedules
            .Where(s => s.LoanId == loanId)
            .OrderBy(s => s.InstallmentNumber)
            .ToListAsync();

        return schedules.Select(MapToScheduleResponse);
    }

    // Create

    public async Task<LoanResponse> CreateAsync(CreateLoanRequest request)
    {
        // Validate all referenced entities exist

        var customer = await _db.Customers.FindAsync(request.CustomerId)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var officer = await _db.Users.FindAsync(request.LoanOfficerId)
            ?? throw new NotFoundException(
                $"Loan officer with ID '{request.LoanOfficerId}' was not found.");

        var product = await _db.LoanProducts.FindAsync(request.LoanProductId)
            ?? throw new NotFoundException("LoanProduct", request.LoanProductId);

        // Business rule validations

        if (!product.IsActive)
            throw new DomainException(
                $"Loan product '{product.Name}' is inactive and cannot be used.");

        if (request.PrincipalAmount < product.MinLoanAmount ||
            request.PrincipalAmount > product.MaxLoanAmount)
            throw new DomainException(
                $"Principal amount must be between " +
                $"NGN {product.MinLoanAmount:N2} and NGN {product.MaxLoanAmount:N2} " +
                $"for the '{product.Name}' product.");

        if (request.TenorMonths < product.MinTenorMonths ||
            request.TenorMonths > product.MaxTenorMonths)
            throw new DomainException(
                $"Tenor must be between {product.MinTenorMonths} and " +
                $"{product.MaxTenorMonths} months for the '{product.Name}' product.");

        if (request.DisbursementDate < DateOnly.FromDateTime(DateTime.Today.AddDays(-7)))
            throw new DomainException(
                "Disbursement date cannot be more than 7 days in the past.");

        // Build the loan

        var reference = await GenerateLoanReferenceAsync();
        var rateAsDecimal = request.MonthlyInterestRate / 100m;

        var loan = new Loan
        {
            CustomerId = request.CustomerId,
            LoanProductId = request.LoanProductId,
            LoanOfficerId = request.LoanOfficerId,
            LoanReference = reference,
            PrincipalAmount = request.PrincipalAmount,
            OutstandingBalance = request.PrincipalAmount,
            MonthlyInterestRate = rateAsDecimal,
            TenorMonths = request.TenorMonths,
            DisbursementDate = request.DisbursementDate,
            MaturityDate = request.DisbursementDate.AddMonths(request.TenorMonths),
            Status = LoanStatus.Active
        };

        _db.Loans.Add(loan);

        // Generate repayment schedule 

        var emi = EmiCalculator.Calculate(
            request.PrincipalAmount,
            rateAsDecimal,
            request.TenorMonths);

        for (int i = 1; i <= request.TenorMonths; i++)
        {
            _db.RepaymentSchedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNumber = i,
                DueDate = request.DisbursementDate.AddMonths(i),
                AmountDue = emi,
                Status = InstallmentStatus.Pending
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Loan created: {Reference} | Customer: {CustomerId} | " +
            "Principal: NGN {Amount} | Tenor: {Tenor} months | EMI: NGN {Emi}",
            reference, request.CustomerId,
            request.PrincipalAmount, request.TenorMonths, emi);

        return await GetByIdAsync(loan.Id);
    }

    // Record Payment

    public async Task<LoanResponse> RecordPaymentAsync(
        Guid loanId, RecordPaymentRequest request)
    {
        var loan = await _db.Loans
            .Include(l => l.RepaymentSchedules)
            .FirstOrDefaultAsync(l => l.Id == loanId)
            ?? throw new NotFoundException("Loan", loanId);

        // Guard against invalid loan states

        if (loan.Status == LoanStatus.Settled)
            throw new DomainException(
                "Cannot record payment — this loan is already fully settled.");

        if (loan.Status == LoanStatus.WrittenOff)
            throw new DomainException(
                "Cannot record payment — this loan has been written off.");

        if (request.AmountPaid <= 0)
            throw new DomainException(
                "Payment amount must be greater than zero.");

        if (request.PaymentDate > DateOnly.FromDateTime(DateTime.Today))
            throw new DomainException(
                "Payment date cannot be in the future.");

        // Find the oldest outstanding installment

        var target = loan.RepaymentSchedules
            .Where(s => s.Status is
                InstallmentStatus.Pending or
                InstallmentStatus.PartiallyPaid or
                InstallmentStatus.Overdue)
            .OrderBy(s => s.DueDate)
            .FirstOrDefault()
            ?? throw new DomainException(
                "No outstanding installments found on this loan.");

        // Apply payment

        var totalReceived = target.AmountPaid + request.AmountPaid;
        var totalDue = target.TotalAmountDue;

        if (totalReceived >= totalDue)
        {
            // Full payment — cap to exact amount due
            target.AmountPaid = totalDue;
            target.Status = InstallmentStatus.Paid;
            target.PaymentDate = request.PaymentDate;

            _logger.LogInformation(
                "Installment {Number} on loan {Ref} fully paid. Amount: NGN {Amount}",
                target.InstallmentNumber, loan.LoanReference, totalDue);
        }
        else
        {
            // Partial payment
            target.AmountPaid = totalReceived;
            target.Status = InstallmentStatus.PartiallyPaid;

            _logger.LogInformation(
                "Partial payment of NGN {Paid} applied to installment {Number} " +
                "on loan {Ref}. Remaining: NGN {Remaining}",
                request.AmountPaid, target.InstallmentNumber,
                loan.LoanReference, totalDue - totalReceived);
        }

        // Recalculate outstanding balance

        var totalScheduled = loan.RepaymentSchedules
            .Sum(s => s.TotalAmountDue);

        var totalPaidAcrossAll = loan.RepaymentSchedules
            .Sum(s => s.AmountPaid);

        loan.OutstandingBalance =
            Math.Max(0, totalScheduled - totalPaidAcrossAll);

        // Update loan status 

        var allSettled = loan.RepaymentSchedules.All(s =>
            s.Status == InstallmentStatus.Paid ||
            s.Status == InstallmentStatus.Waived);

        if (allSettled)
        {
            loan.Status = LoanStatus.Settled;
            _logger.LogInformation(
                "Loan {Ref} is now fully settled.", loan.LoanReference);
        }
        else if (loan.Status == LoanStatus.Overdue)
        {
            // Payment received on overdue loan — move back to Active
            loan.Status = LoanStatus.Active;
        }

        await _db.SaveChangesAsync();

        return await GetByIdAsync(loanId);
    }

    // Restructure

    public async Task<LoanResponse> RestructureAsync(
        Guid loanId, RestructureLoanRequest request)
    {
        var loan = await _db.Loans.FindAsync(loanId)
            ?? throw new NotFoundException("Loan", loanId);

        if (loan.Status is LoanStatus.Settled or LoanStatus.WrittenOff)
            throw new DomainException(
                $"Cannot restructure a loan with status '{loan.Status}'.");

        if (request.NewMaturityDate <= DateOnly.FromDateTime(DateTime.Today))
            throw new DomainException(
                "New maturity date must be a future date.");

        if (request.NewMaturityDate <= loan.MaturityDate)
            throw new DomainException(
                "New maturity date must be later than the current maturity date " +
                $"({loan.MaturityDate:dd MMM yyyy}).");

        loan.MaturityDate = request.NewMaturityDate;
        loan.Status = LoanStatus.Restructured;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Loan {Ref} restructured. New maturity: {Date}. Reason: {Reason}",
            loan.LoanReference,
            request.NewMaturityDate.ToString("dd MMM yyyy"),
            request.Reason ?? "Not specified");

        return await GetByIdAsync(loanId);
    }

    // Generate loan reference

    public async Task<string> GenerateLoanReferenceAsync()
    {
        var year = WatClock.Today.Year;

        // Count loans created this calendar year to get next sequence number
        var countThisYear = await _db.Loans
            .CountAsync(l => l.CreatedAt.Year == year);

        var reference = LoanReferenceGenerator.Generate(year, countThisYear + 1);

        // Ensure uniqueness — loop if the reference already exists
        while (await _db.Loans.AnyAsync(l => l.LoanReference == reference))
        {
            countThisYear++;
            reference = LoanReferenceGenerator.Generate(year, countThisYear + 1);
        }

        return reference;
    }

    // Private helpers

    private async Task<Loan> LoadLoanAsync(Guid id) =>
        await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanOfficer)
            .FirstOrDefaultAsync(l => l.Id == id)
            ?? throw new NotFoundException("Loan", id);

    private static LoanResponse MapToResponse(Loan l) =>
        new(
            l.Id,
            l.LoanReference,
            l.CustomerId,
            l.Customer.FullName,
            PhoneHelper.Mask(l.Customer.PhoneNumber),
            l.PrincipalAmount,
            l.OutstandingBalance,
            l.MonthlyInterestRate * 100m,   // return as % to caller
            l.TenorMonths,
            l.DisbursementDate,
            l.MaturityDate,
            l.Status,
            l.LoanOfficer?.FullName ?? "N/A",
            l.CreatedAt);

    private static RepaymentScheduleResponse MapToScheduleResponse(
        RepaymentSchedule s) =>
        new(
            s.Id,
            s.InstallmentNumber,
            s.DueDate,
            s.AmountDue,
            s.AmountPaid,
            s.PenaltyAmount,
            s.AmountRemaining,
            s.Status,
            s.PaymentDate);

    private static async Task<PagedResult<LoanResponse>> ToPagedResultAsync(
        IQueryable<Loan> query, int page, int pageSize)
    {
        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<LoanResponse>(
            items.Select(MapToResponse),
            total, page, pageSize);
    }
}