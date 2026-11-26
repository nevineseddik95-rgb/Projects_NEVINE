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

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var host = _config["Email:Host"];
        var user = _config["Email:Username"];
        var pass = _config["Email:Password"]?.Replace(" ", "");
        var from = _config["Email:FromAddress"] ?? user ?? "";

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            _log.LogError("Email is not configured. Set Email__Host and Email__FromAddress (or Email__Username).");
            return false;
        }
        if (!string.IsNullOrEmpty(user) && string.IsNullOrEmpty(pass))
        {
            _log.LogError("Email username is set but Email__Password is empty.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(toEmail))
            return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_config["Email:FromName"] ?? "Beauty & Shine", from));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient { Timeout = 15000 };
            await client.ConnectAsync(host, _config.GetValue<int>("Email:Port", 587),
                SecureSocketOptions.Auto, timeout.Token);
            if (!string.IsNullOrEmpty(user))
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
}
