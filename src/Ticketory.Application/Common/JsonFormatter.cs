using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Ticketory.Application.Common;

public static class JsonFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string PrettyPrint(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return payload;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return JsonSerializer.Serialize(document.RootElement, Options);
        }
        catch (JsonException)
        {
            return payload;
        }
    }
}
