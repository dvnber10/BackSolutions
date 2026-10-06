using BackSolutions.Core.Interfaces;
using BackSolutions.Core.Options;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BackSolutions.Api.Services;

public sealed class EmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public EmailSender(IOptions<SmtpOptions> options) => _options = options.Value;

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        byte[]? attachmentBytes = null,
        string? attachmentName = null,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.User));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };

        if (attachmentBytes != null && !string.IsNullOrEmpty(attachmentName))
        {
            builder.Attachments.Add(attachmentName, attachmentBytes, MimeKit.ContentType.Parse("application/pdf"));
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, _options.UseStartTls ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.Auto, cancellationToken);
            await client.AuthenticateAsync(_options.User, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
