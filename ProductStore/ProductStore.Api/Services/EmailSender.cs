using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ProductStore.Api.Services;

public interface IEmailSender
{
    /// returns true when the email was sent
    Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody);
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

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var host = _config["Email:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _log.LogError("EMAIL: Email:Host is not set in appsettings.json, so no email was sent.");
            return false;
        }

        var user = _config["Email:Username"];
        var pass = _config["Email:Password"]?.Replace(" ", "");   // Google shows app passwords with spaces

        if (!string.IsNullOrEmpty(user) && string.IsNullOrEmpty(pass))
        {
            _log.LogError("EMAIL: Email:Password is empty. Set it with: dotnet user-secrets set \"Email:Password\" \"your-app-password\" (user-secrets only load when ASPNETCORE_ENVIRONMENT is Development).");
            return false;
        }

        try
        {
            var from = _config["Email:FromAddress"] ?? user ?? "";
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_config["Email:FromName"] ?? "Beauty & Shine", from));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, _config.GetValue<int>("Email:Port", 587), SecureSocketOptions.Auto);

            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, pass);

            await client.SendAsync(new SendRequest(message));
            await client.DisconnectAsync(true);
            return true;
        }
        catch (AuthenticationException ex)
        {
            _log.LogError("EMAIL: the email server refused the login for {User}. Use the full Gmail address as Username and a Gmail App Password (not your normal password). Server said: {Message}", user, ex.Message);
            return false;
        }
        catch (SslHandshakeException ex)
        {
            _log.LogError("EMAIL: secure connection failed (antivirus or firewall may be scanning the connection). {Message}", ex.Message);
            return false;
        }
        catch (SmtpCommandException ex)
        {
            _log.LogError("EMAIL: the server rejected the message. Code {Code}: {Message}", ex.StatusCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _log.LogError("EMAIL: failed with {Type}: {Message}", ex.GetType().Name, ex.Message);
            return false;
        }
    }
}