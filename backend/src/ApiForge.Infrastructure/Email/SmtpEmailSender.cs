using System.Net;
using System.Net.Mail;
using ApiForge.Application.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiForge.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private const string ProviderName = "smtp";

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value.Smtp;
        if (!smtp.IsConfigured)
        {
            return EmailSendResult.Failed(ProviderName, "SMTP email is not configured.");
        }

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(smtp.FromEmail, smtp.FromName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(message.ToEmail);
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, null, "text/plain"));

            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.EncryptionId == 2,
                Credentials = new NetworkCredential(smtp.Username, smtp.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000
            };

            await client.SendMailAsync(mail, cancellationToken);
            return EmailSendResult.Sent(ProviderName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invitation email delivery failed for recipient {Recipient}.", message.ToEmail);
            return EmailSendResult.Failed(ProviderName, "Invitation email could not be delivered by the configured SMTP provider.");
        }
    }
}
