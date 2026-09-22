namespace Triply.Api.Modules.Auth;

/// <summary>
/// Minimal email-sending abstraction for the account-verification and
/// password-reset flows (Security Task 1). Swap <see cref="LoggingEmailSender"/>
/// for a real provider (SendGrid, SES, SMTP, ...) once one is wired up — nothing
/// else in the auth flow needs to change.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dev/placeholder implementation: no outbound email infrastructure exists yet,
/// so this logs the message (including the confirmation/reset link) instead of
/// silently dropping it. Safe for local development and CI; replace before
/// shipping to real users so tokens aren't only visible in server logs.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[EMAIL] To: {ToEmail} | Subject: {Subject}\n{Body}",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}
