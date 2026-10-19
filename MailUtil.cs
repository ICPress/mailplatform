using MailKit.Net.Smtp;
using MailKit;
using MimeKit;
using System.Text;
using System.Net;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

public static class MailUtil
{
    public static async Task SendMessage(MimeMessage message, ServerSettings serverSettings)
    {
        using (var client = new SmtpClient())
        {
            await client.ConnectAsync(serverSettings.SMTPServer, serverSettings.SMTPPort, true);

            // Note: only needed if the SMTP server requires authentication
            if (serverSettings.SMTPUsername != null && serverSettings.SMTPPassword != null)
            {
                await client.AuthenticateAsync(serverSettings.SMTPUsername, serverSettings.SMTPPassword);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }

    public static string BuildVerificationLink(string token, string pageUrl)
    {
        if (token.Length > 4096 || !Regex.IsMatch(token, @"\A[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\z"))
            throw new ArgumentException("Invalid queued verification token format.");
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var page) || page.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(page.UserInfo) || !string.IsNullOrEmpty(page.Query) || !string.IsNullOrEmpty(page.Fragment))
            throw new InvalidOperationException("VerificationPageUrl must be an absolute HTTPS page URL without query or fragment.");
        return page.AbsoluteUri + "?authToken=" + Uri.EscapeDataString(token);
    }

    public static async Task<bool> IsActiveEmailTokenAsync(MySqlConnection connection, string email, string token)
    {
        if (token.Length > 4096) return false;
        using var command = new MySqlCommand(@"SELECT COUNT(*) FROM sent_links_users l
            LEFT JOIN users u ON u.username = l.username
            WHERE l.token_hash = @hash AND l.verified_at IS NULL AND l.expires_at > UTC_TIMESTAMP()
            AND (l.email = @email OR (l.email IS NULL AND u.email = @email))", connection);
        command.Parameters.AddWithValue("@hash", SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        command.Parameters.AddWithValue("@email", email);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
    }

    public static async Task SendEmailAccessToken(string email, string token, ServerSettings serverSettings)
    {
        var strb = new StringBuilder(File.ReadAllText("email_register_template.html"));
        // Deliver the exact API-issued JWT. The worker never re-signs or consumes it.
        var link = BuildVerificationLink(token, serverSettings.VerificationPageUrl);
        strb.Replace("{{verification_url}}", WebUtility.HtmlEncode(link));
        strb.Replace("{{static_path}}", serverSettings.ImageStaticPath);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("ICPress", serverSettings?.SMTPUsername));
        message.To.Add(new MailboxAddress(email, email));
        message.Subject = "Your ICPress sign-in link";

        message.Body = new BodyBuilder {
            HtmlBody = strb.ToString(),
            TextBody = "Sign in to ICPress using this link (valid for 30 minutes):\n" + link +
                "\n\nIf you’re new here, choose your username and full name after verification. If you didn’t request this link, ignore this email."
        }.ToMessageBody();

        await SendMessage(message, serverSettings);
    }

    public static async Task SendEmailDeleteMyData(string email, ServerSettings serverSettings)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("ICPress", serverSettings?.SMTPUsername));
        message.To.Add(new MailboxAddress("Storypop", "contact@storypop.net"));
        message.Subject = "[REQUEST] DELETE MY DATA ON Storypop";

        message.Body = new TextPart("html")
        {
            Text = "REQUEST EMAIL: " + email
        };

        await SendMessage(message, serverSettings);
    }
    public static async Task SendEmailWithBody(string email, string body, string subject, ServerSettings serverSettings)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("ICPress", serverSettings?.SMTPUsername));
        message.To.Add(new MailboxAddress(email, email));
        message.Subject = subject;

        message.Body = new TextPart("html")
        {
            Text = body
        };

        await SendMessage(message, serverSettings);
    }
}
