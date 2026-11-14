using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Ticketory.Application.Abstractions.Email;
using Ticketory.Infrastructure.Options;

namespace Ticketory.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            to,
            subject,
            htmlBody,
            Array.Empty<EmailInlineImage>(),
            cancellationToken);
    }

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        IReadOnlyList<EmailInlineImage> inlineImages,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.Value.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };

        foreach (var image in inlineImages)
        {
            var contentType = ContentType.Parse(image.ContentType);
            var part = new MimePart(contentType.MediaType, contentType.MediaSubtype)
            {
                Content = new MimeContent(new MemoryStream(image.Content)),
                ContentId = image.ContentId,
                ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                FileName = image.FileName
            };

            builder.LinkedResources.Add(part);
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = options.Value.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(options.Value.Host, options.Value.Port, socketOptions, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Email '{Subject}' sent to {To}", subject, to);
    }
}
