using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BackSolutions.Externals.Interfaces;
using BackSolutions.Settings;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;
using MimeKit.Text;

namespace BackSolutions.Externals.ExternalServices
{
    public class EmailExternal : EmailInterface
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPassword;

        public EmailExternal(IOptions<EmailSettings> emailSettings)
        {
            var settings = emailSettings.Value;
            _smtpServer = settings.SmtpServer;
            _smtpPort = settings.SmtpPort;
            _smtpUser = settings.SmtpUser;
            _smtpPassword = settings.SmtpPassword;
        }
        public async Task<bool> SendEmail(string to, string subject, string body, byte[]? pdfBytes, string pdfName)
        {
            var message = new MimeMessage();

            // remitente y destinatario
            message.From.Add(new MailboxAddress("BackSolutions", _smtpUser));
            message.To.Add(new MailboxAddress("Destinatario", to));

            // asunto y cuerpo del mensaje
            var builder = new BodyBuilder{
                HtmlBody = body
            };

            if(pdfBytes != null && pdfBytes.Length > 0)
            {
                builder.Attachments.Add(pdfName, pdfBytes, new ContentType("application", "pdf"));
            }
            message.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                try
                {
                    await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_smtpUser, _smtpPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                    return true;
                }
                catch (Exception ex)
                {
                    // Manejar la excepción según sea necesario
                    Console.WriteLine($"Error al enviar el correo: {ex.Message}");
                    return false;
                }
                finally
                {
                    await client.DisconnectAsync(true);
                }
            }
        }
    }
}