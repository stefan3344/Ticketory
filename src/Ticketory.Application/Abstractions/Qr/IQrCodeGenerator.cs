namespace Ticketory.Application.Abstractions.Qr;

public interface IQrCodeGenerator
{
    string GenerateSvg(string payload);

    byte[] GeneratePng(string payload);
}
