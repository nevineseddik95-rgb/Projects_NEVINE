using System.Net;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ProductStore.Api.Services;

public interface IEmailSender
{
    Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody,
        CancellationToken cancellationToken = default);
}

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> log)
    {
        _config = config;
        _log = log;
    }

    public static bool IsConfigured(IConfiguration config)
    {
        var host = config["Email:Host"]?.Trim();
        var user = config["Email:Username"]?.Trim();
        var pass = config["Email:Password"];
        var from = config["Email:FromAddress"] ?? user;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from)) return false;
        if (IsGmailHost(host)) return !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass);
        return string.IsNullOrWhiteSpace(user) || !string.IsNullOrWhiteSpace(pass);
    }

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var host = _config["Email:Host"]?.Trim();
        var user = _config["Email:Username"]?.Trim();
        var pass = _config["Email:Password"]?.Replace(" ", "");
        var configuredFrom = _config["Email:FromAddress"] ?? user ?? "";

        if (!IsConfigured(_config))
        {
            _log.LogError("Email is not configured. Set Email__Host, Email__FromAddress, Email__Username and Email__Password.");
            return false;
        }
        if (!EmailAddress.TryNormalize(toEmail, out var recipient) || recipient.Length == 0)
        {
            _log.LogError("The customer email address is missing or invalid.");
            return false;
        }
        if (!EmailAddress.TryNormalize(configuredFrom, out var from))
        {
            _log.LogError("Email__FromAddress is invalid.");
            return false;
        }

        if (IsGmailHost(host!) && !string.IsNullOrWhiteSpace(user))
        {
            var gmailAddress = EmailAddress.Normalize(user);
            if (!string.Equals(from, gmailAddress, StringComparison.OrdinalIgnoreCase))
            {
                _log.LogWarning("Gmail requires the From address to match Email__Username; using the authenticated Gmail address.");
                from = gmailAddress;
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_config["Email:FromName"] ?? "Beauty & Shine", from));
            message.To.Add(new MailboxAddress(toName, recipient));
            message.Subject = subject;
            var textBody = WebUtility.HtmlDecode(Regex.Replace(htmlBody, "<[^>]+>", " "));
            textBody = Regex.Replace(textBody, "\\s+", " ").Trim();
            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = textBody.Length > 0 ? textBody : subject
            }.ToMessageBody();

            using var client = new SmtpClient { Timeout = 15000 };
            await client.ConnectAsync(host!, _config.GetValue<int>("Email:Port", 587),
                SecureSocketOptions.Auto, timeout.Token);
            if (!string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, pass ?? "", timeout.Token);
            await client.SendAsync(new SendRequest(message), timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _log.LogError("Email delivery timed out after 20 seconds.");
            return false;
        }
        catch (AuthenticationException ex)
        {
            _log.LogError("Email login was rejected for {User}. Check the username and app password. Server said: {Message}", user, ex.Message);
            return false;
        }
        catch (SslHandshakeException ex)
        {
            _log.LogError("The secure email connection failed. Check the SMTP host, port and TLS settings. {Message}", ex.Message);
            return false;
        }
        catch (SmtpCommandException ex)
        {
            _log.LogError("The email server rejected the message. Code {Code}: {Message}", ex.StatusCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _log.LogError("Email delivery failed with {Type}: {Message}", ex.GetType().Name, ex.Message);
            return false;
        }
    }

    private static bool IsGmailHost(string host) =>
        host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase);
}
