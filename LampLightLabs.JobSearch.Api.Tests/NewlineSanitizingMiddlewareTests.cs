using System.Text;
using LampLightLabs.JobSearch.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Unit tests for <see cref="NewlineSanitizingMiddleware"/>.
///
/// Each test wires a capturing RequestDelegate as the next middleware so the
/// body seen downstream can be asserted without involving ASP.NET Core hosting.
/// </summary>
public class NewlineSanitizingMiddlewareTests
{
    private static DefaultHttpContext BuildJsonContext(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json; charset=utf-8";
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        return context;
    }

    private static async Task<string> RunAndCaptureBody(DefaultHttpContext context)
    {
        var ct = TestContext.Current.CancellationToken;
        string captured = string.Empty;

        var middleware = new NewlineSanitizingMiddleware(async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            captured = await reader.ReadToEndAsync(ct);
        });

        await middleware.InvokeAsync(context);
        return captured;
    }

    // --- Newline replacement ---

    [Fact]
    public async Task InvokeAsync_JsonBodyWithLiteralLineFeed_ReplacesWithSpace()
    {
        var context = BuildJsonContext("{\"jobDescription\":\"Senior .NET Engineer\nwith Azure\"}");

        var result = await RunAndCaptureBody(context);

        Assert.Equal("{\"jobDescription\":\"Senior .NET Engineer with Azure\"}", result);
    }

    [Fact]
    public async Task InvokeAsync_JsonBodyWithCarriageReturnLineFeed_ReplacesWithSingleSpace()
    {
        // \r\n must become one space, not two
        var context = BuildJsonContext("{\"jobDescription\":\"Requirements\r\nResponsibilities\"}");

        var result = await RunAndCaptureBody(context);

        Assert.Equal("{\"jobDescription\":\"Requirements Responsibilities\"}", result);
    }

    [Fact]
    public async Task InvokeAsync_JsonBodyWithCarriageReturn_ReplacesWithSpace()
    {
        var context = BuildJsonContext("{\"jobDescription\":\"Line one\rLine two\"}");

        var result = await RunAndCaptureBody(context);

        Assert.Equal("{\"jobDescription\":\"Line one Line two\"}", result);
    }

    // --- Non-JSON requests are not modified ---

    [Fact]
    public async Task InvokeAsync_NonJsonContentType_BodyIsNotModified()
    {
        var original = "field=value%0Awith+newline";
        var bytes = Encoding.UTF8.GetBytes(original);
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;

        var result = await RunAndCaptureBody(context);

        Assert.Equal(original, result);
    }
}
