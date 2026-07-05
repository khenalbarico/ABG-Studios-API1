using Abg.Domain.Contracts;
using FunctionApp1.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FunctionApp1.Functions;

internal static class FunctionHelpers
{
    public static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ClientPrincipal? GetPrincipal(this HttpRequest req)
        => SwaPrincipalParser.Parse(req.Headers[SwaPrincipalParser.HeaderName]);

    public static string GetCallerKey(this HttpRequest req, ClientPrincipal? principal)
        => principal?.UserKey
           ?? req.HttpContext.Connection.RemoteIpAddress?.ToString()
           ?? "unknown";

    public static async Task<T?> ReadBodyAsync<T>(this HttpRequest req, CancellationToken ct)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(req.Body, RequestJsonOptions, ct);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static IActionResult Unauthenticated()
        => new ObjectResult(new ApiError { Message = "Sign in to book a service." }) { StatusCode = StatusCodes.Status401Unauthorized };

    public static IActionResult TooManyRequests()
        => new ObjectResult(new ApiError { Message = "Too many requests. Please wait a moment and try again." }) { StatusCode = StatusCodes.Status429TooManyRequests };

    public static IActionResult BadRequest(string message)
        => new BadRequestObjectResult(new ApiError { Message = message });

    public static IActionResult Conflict(string message)
        => new ConflictObjectResult(new ApiError { Message = message });
}
