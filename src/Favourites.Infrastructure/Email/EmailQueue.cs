using System.Threading.Channels;

namespace Favourites.Infrastructure.Email;

public sealed record QueuedEmail(string To, string Subject, string HtmlBody);

/// <summary>
/// In-process queue between HTTP request handling and SMTP delivery, so a
/// request never waits on the mail server (and response time stays independent
/// of whether an email is actually sent).
/// </summary>
public sealed class EmailQueue
{
    private readonly Channel<QueuedEmail> _channel = Channel.CreateUnbounded<QueuedEmail>();

    public void Enqueue(QueuedEmail email) => _channel.Writer.TryWrite(email);

    public IAsyncEnumerable<QueuedEmail> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
