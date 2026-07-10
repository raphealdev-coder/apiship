using System.Net;
using System.Net.Mail;

namespace apiship.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}

/// <summary>
/// Sends email via SMTP when configured (Email:Smtp:Host in settings).
/// Otherwise falls back to writing the message to the SentEmails folder so the
/// flow works locally without any credentials.
/// </summary>
public class EmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration config, IWebHostEnvironment env, ILogger<EmailSender> logger)
    {
        _config = config;
        _env = env;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var host = _config["Email:Smtp:Host"];
        var from = _config["Email:From"] ?? "no-reply@apiship.app";

        if (!string.IsNullOrWhiteSpace(host))
        {
            var port = int.TryParse(_config["Email:Smtp:Port"], out var p) ? p : 587;
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(
                    _config["Email:Smtp:User"], _config["Email:Smtp:Password"])
            };
            using var message = new MailMessage(from, toEmail, subject, htmlBody) { IsBodyHtml = true };
            await client.SendMailAsync(message);
            _logger.LogInformation("Confirmation email sent to {Email} via SMTP.", toEmail);
            return;
        }

        // Fallback: write the email to disk for local development.
        var dir = Path.Combine(_env.ContentRootPath, "SentEmails");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.html");
        var content = $"<!-- To: {toEmail} | Subject: {subject} -->\n{htmlBody}";
        await File.WriteAllTextAsync(file, content);
        _logger.LogInformation("SMTP not configured. Confirmation email for {Email} written to {File}.", toEmail, file);
    }
}
