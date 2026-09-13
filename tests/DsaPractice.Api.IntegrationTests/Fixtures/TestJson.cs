using System.Text.Json;
using System.Text.Json.Serialization;

namespace DsaPractice.Api.IntegrationTests.Fixtures;

internal static class TestJson
{
    // Mirrors Program.cs's ConfigureHttpJsonOptions. Integer enum values are rejected so any test
    // deserializing a response fails if an enum ever goes back over the wire as a number.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
}
