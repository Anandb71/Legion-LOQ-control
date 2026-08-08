namespace LegionLoqControl.Infrastructure.Windows.Management;

internal sealed record WmiClassMetadata(bool Exists, IReadOnlySet<string> Methods);

internal interface IWindowsManagementReader
{
    IReadOnlyDictionary<string, string?> ReadFirstInstance(
        string namespacePath,
        string className,
        IReadOnlyCollection<string> properties);

    WmiClassMetadata ReadClassMetadata(string namespacePath, string className);
}
