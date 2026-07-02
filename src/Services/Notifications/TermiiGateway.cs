using System.Text;
using System.Text.Json;
using OyaMicroCreditCLRRS.Services.Interfaces;

namespace OyaMicroCreditCLRRS.Services.Notifications;

// Concrete implementation of ITermiiGateway.
// Sends SMS and email via Termii's REST API.
// Applying SOLID Principle — Single Responsibility: this class only handles Termii HTTP calls.
// All retry logic and logging lives in NotificationService.

public class TermiiGateway : ITermiiGateway
{
    private readonly HttpClient _http;
    private readonly ILogger<TermiiGateway> _logger;
    private readonly TermiiSettings _settings;

    public TermiiGateway(
        HttpClient http,
        ILogger<TermiiGateway> logger,
        IConfiguration configuration)
    {
        _http = http;
        _logger = logger;
        _settings = configuration
            .GetSection(TermiiSettings.SectionKey)
            .Get<TermiiSettings>()
            ?? throw new InvalidOperationException("Termii settings not configured.");
    }

    public async Task<TermiiSendResult> SendSmsAsync(string to, string message, string senderId)
    {
        var payload = new
        {
            to,
            from = senderId,
            sms = message,
            type = "plain",
            channel = "generic",
            api_key = _settings.ApiKey
        };

        return await PostAsync("/api/sms/send", payload);
    }

    public async Task<TermiiSendResult> SendEmailAsync(string to, string subject, string message)
    {
        var payload = new
        {
            api_key = _settings.ApiKey,
            email_address = to,
            code = message,
            email_configuration_id = _settings.EmailConfigId
        };

        return await PostAsync("/api/email/otp/send", payload);
    }

    private async Task<TermiiSendResult> PostAsync(string endpoint, object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Termii API error {StatusCode}: {Body}", response.StatusCode, body);
                return new TermiiSendResult(false, null, $"HTTP {(int)response.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Termii returns message_id on success
            var messageId = root.TryGetProperty("message_id", out var mid)
                ? mid.GetString()
                : root.TryGetProperty("pinId", out var pin)
                    ? pin.GetString()
                    : null;

            _logger.LogInformation("Termii message sent. MessageId: {MessageId}", messageId);
            return new TermiiSendResult(true, messageId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Termii gateway exception on {Endpoint}", endpoint);
            return new TermiiSendResult(false, null, ex.Message);
        }
    }
}

// Termii API configuration bound from appsettings.json
public class TermiiSettings
{
    public const string SectionKey = "Termii";

    public string ApiKey { get; set; } = default!;
    public string BaseUrl { get; set; } = "https://v3.api.termii.com";
    public string SenderId { get; set; } = "OYAMFB";
    public string? EmailConfigId { get; set; }
}