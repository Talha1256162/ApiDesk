namespace ApiForge.Application.Abstractions.Services;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(
    string ToEmail,
    string Subject,
    string HtmlBody,
    string TextBody);

public sealed record EmailSendResult(bool Succeeded, string Provider, string? ErrorMessage = null)
{
    public static EmailSendResult Sent(string provider) => new(true, provider);
    public static EmailSendResult Failed(string provider, string errorMessage) => new(false, provider, errorMessage);
}
