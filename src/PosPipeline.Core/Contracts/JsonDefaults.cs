using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PosPipeline.Core.Contracts;

/// <summary>One serializer configuration for everything this system reads and writes.</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        // The default encoder escapes characters that are dangerous in HTML, which turns the
        // quotes in an error message into '. Everything here is served as application/json
        // or stored as a JSON object, never interpolated into a page, so the readable form is
        // both safe and much easier to work with from the CLI.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"Payload did not deserialize into {typeof(T).Name}.");
}
