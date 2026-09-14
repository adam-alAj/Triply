using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Triply.Api.Common.Middleware;
using Xunit;

namespace Triply.Api.Tests;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_UnhandledException_ReturnsProblemDetails_NeverStackTraceOrInternalMessage()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = _ => throw new InvalidOperationException("Connection string: Server=secret-db;Password=hunter2");

        var middleware = new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        Assert.Contains("An unexpected error occurred", body);

        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain("Connection string", body);
        Assert.DoesNotContain(" at ", body);
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThroughUnchanged()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }
}