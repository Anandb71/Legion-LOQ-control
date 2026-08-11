using Microsoft.Win32;

namespace LegionLoqControl.Services;

internal interface IDiagnosticsExportDestinationPicker
{
    string? PickDestination();
}

internal sealed class DiagnosticsExportDestinationPicker : IDiagnosticsExportDestinationPicker
{
    public string? PickDestination()
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            CheckPathExists = true,
            CreatePrompt = false,
            DefaultExt = ".json",
            FileName =
                $"LegionLoqControl-diagnostics-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}.json",
            Filter = "JSON diagnostics (*.json)|*.json",
            OverwritePrompt = true,
            Title = "Export redacted diagnostics",
            ValidateNames = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
