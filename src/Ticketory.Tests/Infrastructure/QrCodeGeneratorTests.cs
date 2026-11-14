using FluentAssertions;
using Ticketory.Infrastructure.Qr;

namespace Ticketory.Tests.Infrastructure;

public class QrCodeGeneratorTests
{
    [Fact]
    public void GeneratePng_ReturnsPngBytes()
    {
        var generator = new QrCodeGenerator();

        var bytes = generator.GeneratePng("TICKETORY:TEST");

        bytes.Should().NotBeEmpty();
        bytes.Take(8).Should().Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
    }
}
