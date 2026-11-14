namespace Ticketory.Application.Abstractions.Email;

public sealed record EmailInlineImage(
    string ContentId,
    string FileName,
    string ContentType,
    byte[] Content);
