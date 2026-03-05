using System.Net;
using System.Net.Mail;
using CarManaagementApi.Contracts;
using Microsoft.Extensions.Options;

namespace CarManaagementApi.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;

    public SmtpEmailSender(IOptions<SmtpSettings> smtpOptions)
    {
        _settings = smtpOptions.Value;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (!_settings.Enabled)
        {
            throw new InvalidOperationException("SMTP is disabled. Enable Smtp:Enabled to send emails.");
        }

        if (string.IsNullOrWhiteSpace(_settings.Host)
            || string.IsNullOrWhiteSpace(_settings.Username)
            || string.IsNullOrWhiteSpace(_settings.Password)
            || string.IsNullOrWhiteSpace(_settings.SenderEmail))
        {
            throw new InvalidOperationException("SMTP settings are incomplete.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(toEmail);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.UseSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };

        await client.SendMailAsync(message);
    }
}
