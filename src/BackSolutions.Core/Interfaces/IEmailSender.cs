namespace BackSolutions.Core.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        byte[]? attachmentBytes = null,
        string? attachmentName = null,
        CancellationToken cancellationToken = default);
}
