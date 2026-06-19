using System.Text;

namespace LampLightLabs.JobSearch.Api.Middleware;

/// <summary>
/// Replaces literal newline and carriage return characters in JSON request bodies
/// with spaces before the body reaches the JSON deserializer. Unescaped newlines
/// inside JSON string values are illegal per RFC 8259 and cause the parser to reject
/// the request; clients that paste raw job description text can easily produce them.
/// </summary>
public class NewlineSanitizingMiddleware
{
    private readonly RequestDelegate _next;

    public NewlineSanitizingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            string body;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync();

            // Replace \r\n first so it becomes one space, not two
            var sanitized = body.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
            var bytes = Encoding.UTF8.GetBytes(sanitized);

            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }

        await _next(context);
    }
}
