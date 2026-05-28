using MailKit.Net.Smtp;
using MailKit;
using MimeKit;
using System.Text;

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

    public static async Task SendEmailAccessToken(string email, string token, ServerSettings serverSettings)
    {
        var strb = new StringBuilder(File.ReadAllText("email_register_template.html"));
        strb.Replace("{{authtoken}}", token);
        strb.Replace("{{static_path}}", serverSettings.ImageStaticPath);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("ICPress", serverSettings?.SMTPUsername));
        message.To.Add(new MailboxAddress(email, email));
        message.Subject = "Confirm your email on ICPress";

        message.Body = new TextPart("html")
        {
            Text = strb.ToString()
        };

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