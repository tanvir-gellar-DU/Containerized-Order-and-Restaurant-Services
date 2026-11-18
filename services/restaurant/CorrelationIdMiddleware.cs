using System.Diagnostics;

namespace Restaurant;

/// <summary>
/// Runs for every request:
///  1. takes X-Correlation-ID from the request, or creates a new one,
///  2. sends it back in the response header,
///  3. adds it to every log line written during this request,
///  4. writes one log line with method, path, status and duration.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (_logger.BeginScope("correlation_id={CorrelationId}", correlationId))
        {
            Stopwatch timer = Stopwatch.StartNew();
            await _next(context);
            timer.Stop();
            _logger.LogInformation("{Method} {Path} -> {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, timer.ElapsedMilliseconds);
        }
    }

    public static string GetCorrelationId(HttpContext context)
    {
        object? value = context.Items[ItemKey];
        if (value == null)
        {
            return "unknown";
        }
        return value.ToString() ?? "unknown";
    }
}
