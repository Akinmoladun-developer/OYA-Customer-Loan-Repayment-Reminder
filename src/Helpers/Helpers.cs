using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OyaMicroCreditCLRRS.Helpers;

public static class PhoneHelper
{
    private static readonly Regex NigerianPattern =
        new(@"^(\+?234|0)(7[0-9]|8[0-1]|9[0-1])\d{8}$", RegexOptions.Compiled);

    // Normalising Nigerian phone to be in this format {2348012345678 } format
    public static string Normalise(string raw)
    {
        var cleaned = raw.Trim().Replace(" ", "").Replace("-", "");
        if (!NigerianPattern.IsMatch(cleaned))
            throw new ArgumentException($"'{raw}' is not a valid Nigerian phone number.");

        return cleaned.StartsWith('+') ? cleaned[1..]
            : cleaned.StartsWith("234") ? cleaned
            : "234" + cleaned[1..];
    }

    // Masks phone for display and audit logs: 234801****78 
    public static string Mask(string normalised) =>
        normalised.Length >= 6
            ? normalised[..6] + "****" + normalised[^2..]
            : normalised;

    public static bool IsValidNigerian(string raw)
    {
        var cleaned = raw.Trim().Replace(" ", "");
        return NigerianPattern.IsMatch(cleaned);
    }
}

public static class HashHelper
{
    // SHA-256 hash for BVN storage (NDPR compliance).
    public static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public static class WatClock
{
    private static readonly TimeZoneInfo Wat =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "W. Central Africa Standard Time" : "Africa/Lagos");

    // Current time in West Africa Time (UTC+1).
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Wat);

    // Current hour in WAT for blackout checks.
    public static int CurrentHour => Now.Hour;

    // Today's date in WAT.
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    public static DateTime ToWat(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, Wat);
}

public static class LoanReferenceGenerator
{
    // Generates OYA-YYYY-NNNNN format from year + sequence.
    public static string Generate(int year, int sequence) =>
        $"OYA-{year}-{sequence:D5}";
}

public static class EmiCalculator
{
    // Calculates monthly EMI using reducing-balance method.
    // Formula: P × r × (1+r)^n / ((1+r)^n - 1)
    public static decimal Calculate(decimal principal, decimal monthlyRateDecimal, int tenorMonths)
    {
        if (monthlyRateDecimal == 0)
            return Math.Round(principal / tenorMonths, 2, MidpointRounding.AwayFromZero);

        var pow = (decimal)Math.Pow(1 + (double)monthlyRateDecimal, tenorMonths);
        return Math.Round(principal * monthlyRateDecimal * pow / (pow - 1), 2, MidpointRounding.AwayFromZero);
    }
}