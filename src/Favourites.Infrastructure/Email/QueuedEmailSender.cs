using Favourites.Application.Abstractions.Email;

namespace Favourites.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> adapter that enqueues instead of sending inline;
/// <see cref="EmailQueueProcessor"/> performs the actual SMTP delivery.
/// </summary>
public sealed class QueuedEmailSender(EmailQueue queue) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        queue.Enqueue(new QueuedEmail(to, subject, htmlBody));
        return Task.CompletedTask;
    }
}
