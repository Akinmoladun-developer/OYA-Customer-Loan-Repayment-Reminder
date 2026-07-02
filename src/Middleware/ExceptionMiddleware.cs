using System.Net;
using System.Text.Json;
using OyaMicroCreditCLRRS.Domain.Exceptions;
using OyaMicroCreditCLRRS.Models.Responses;

namespace OyaMicroCreditCLRRS.Middleware;

// This catches all unhandled exceptions and returns a consistent
// ApiResponse JSON envelope instead of raw stack traces.
// DomainException   - 400 Bad Request
// NotFoundException - 404 Not Found
// ForbiddenException - 403 Forbidden
// Other request response - 500 Internal Server Error

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            DomainException d     => (HttpStatusCode.BadRequest, d.Message),
            NotFoundException n   => (HttpStatusCode.NotFound, n.Message),
            ForbiddenException f  => (HttpStatusCode.Forbidden, f.Message),
            _                     => (HttpStatusCode.InternalServerError,
                                      "An unexpected error occurred. Please try again.")
        };

        // Log 500s as errors, everything else as warnings
        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
        else
            _logger.LogWarning("Handled exception [{Status}]: {Message}",
                (int)statusCode, ex.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse<string>.Fail(message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}