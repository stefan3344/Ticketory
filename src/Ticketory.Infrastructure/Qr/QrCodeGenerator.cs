using QRCoder;
using Ticketory.Application.Abstractions.Qr;

namespace Ticketory.Infrastructure.Qr;

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    private const int PixelsPerModule = 10;

    public string GenerateSvg(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new SvgQRCode(data).GetGraphic(PixelsPerModule);
    }

    public byte[] GeneratePng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
    }
}
