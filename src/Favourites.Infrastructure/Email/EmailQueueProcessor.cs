using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Favourites.Infrastructure.Email;

/// <summary>
/// Background service that drains <see cref="EmailQueue"/> and delivers each
/// message via <see cref="SmtpEmailSender"/>. Failures are logged, never
/// surfaced to the request that queued the email.
/// </summary>
public sealed class EmailQueueProcessor(
    EmailQueue queue,
    SmtpEmailSender smtpEmailSender,
    ILogger<EmailQueueProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await smtpEmailSender.SendAsync(email.To, email.Subject, email.HtmlBody, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send email with subject {Subject}.", email.Subject);
            }
        }
    }
}
