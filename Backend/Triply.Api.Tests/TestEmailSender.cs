using System.Collections.Concurrent;
using Triply.Api.Modules.Auth;

namespace Triply.Api.Tests;

/// <summary>
/// Test double for <see cref="IEmailSender"/>. Keeps every "sent" email in memory
/// so integration tests can inspect confirmation/reset emails.
/// </summary>
public class TestEmailSender : IEmailSender
{
    public record SentEmail(
        string ToEmail,
        string Subject,
        string Body,
        long Sequence);

    private static readonly ConcurrentBag<SentEmail> _sent = new();
    private static long _sequence;

    public Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var sequence = Interlocked.Increment(ref _sequence);

        _sent.Add(new SentEmail(
            toEmail,
            subject,
            body,
            sequence));

        return Task.CompletedTask;
    }

    // Returns the most recently sent email for this recipient.
    // ConcurrentBag does not guarantee enumeration order, so use the explicit sequence.
    public static SentEmail? LastFor(string toEmail) =>
        _sent
            .Where(e => e.ToEmail == toEmail)
            .OrderByDescending(e => e.Sequence)
            .FirstOrDefault();

    /// <summary>
    /// Extracts the token/userId that AuthController embeds in the plain-text email body.
    /// </summary>
    public static (string token, Guid userId) ParseTokenAndUserId(SentEmail email)
    {
        var lines = email.Body.Split('\n');

        var token = lines[0]
            .Split(": ", 2)[1]
            .Trim();

        var userId = Guid.Parse(
            lines[1]
                .Split(": ", 2)[1]
                .Trim());

        return (token, userId);
    }
}