using System.Text.Json;
using System.Text.Json.Serialization;

namespace DsaPractice.Contracts;

/// <summary>
/// How these contracts go on the wire. It lives next to the contracts because the publisher and
/// the consumer have to agree: with the default options an enum is serialized as its ordinal, so
/// a message would carry `"verdict": 0` and inserting an enum member would silently change what
/// every already-queued message means.
/// </summary>
public static class ContractJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
