using System.Text.Json;
using Xunit;

namespace LegionLoqControl.Platform.Tests;

public sealed class HardwareEvidenceFixtureTests
{
    [Fact]
    public void Committed_hardware_evidence_is_present_and_redacted()
    {
        string evidenceDirectory = Path.Combine(AppContext.BaseDirectory, "hardware-evidence");
        string[] files = Directory.GetFiles(evidenceDirectory, "*.json", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        foreach (string file in files)
        {
            string json = File.ReadAllText(file);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement privacy = document.RootElement.GetProperty("privacy");

            Assert.False(privacy.GetProperty("serialNumberCaptured").GetBoolean());
            Assert.False(privacy.GetProperty("devicePathsCaptured").GetBoolean());
            Assert.False(privacy.GetProperty("userIdentifiersCaptured").GetBoolean());
            Assert.False(json.Contains("\"serialNumber\":", StringComparison.OrdinalIgnoreCase));
            Assert.False(json.Contains("\"devicePath\":", StringComparison.OrdinalIgnoreCase));
            Assert.False(json.Contains("\"userName\":", StringComparison.OrdinalIgnoreCase));
        }
    }
}
