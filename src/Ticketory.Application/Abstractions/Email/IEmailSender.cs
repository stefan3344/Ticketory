namespace Ticketory.Application.Abstractions.Email;

public interface IEmailSender
{
    Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        IReadOnlyList<EmailInlineImage> inlineImages,
        CancellationToken cancellationToken = default);
}
