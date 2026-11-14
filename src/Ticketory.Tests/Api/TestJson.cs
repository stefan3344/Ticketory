using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ticketory.Tests.Api;

public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
