using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegionLoqControl.Application.Diagnostics;

namespace LegionLoqControl.Infrastructure.Windows.Diagnostics;

public static class DiagnosticsJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static byte[] Serialize(DiagnosticsExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
    }

    public static string SerializeToString(DiagnosticsExportDocument document) =>
        Encoding.UTF8.GetString(Serialize(document));

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 8,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
