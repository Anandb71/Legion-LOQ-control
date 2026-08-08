using System.Globalization;
using System.Management;

namespace LegionLoqControl.Infrastructure.Windows.Management;

internal sealed class SystemManagementReader : IWindowsManagementReader
{
    public IReadOnlyDictionary<string, string?> ReadFirstInstance(
        string namespacePath,
        string className,
        IReadOnlyCollection<string> properties)
    {
        ValidateIdentifier(className, nameof(className));
        ArgumentNullException.ThrowIfNull(properties);
        if (properties.Count == 0)
            throw new ArgumentException("At least one property is required.", nameof(properties));

        foreach (string property in properties)
            ValidateIdentifier(property, nameof(properties));

        string query = $"SELECT {string.Join(", ", properties)} FROM {className}";
        using var searcher = new ManagementObjectSearcher(namespacePath, query);
        using ManagementObjectCollection results = searcher.Get();

        foreach (ManagementBaseObject result in results)
        {
            using (result)
            {
                return properties.ToDictionary(
                    static property => property,
                    property => Convert.ToString(result[property], CultureInfo.InvariantCulture),
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    public WmiClassMetadata ReadClassMetadata(string namespacePath, string className)
    {
        ValidateIdentifier(className, nameof(className));

        try
        {
            using var managementClass = new ManagementClass(namespacePath, className, null);
            managementClass.Get();
            IReadOnlySet<string> methods = managementClass.Methods
                .Cast<MethodData>()
                .Select(static method => method.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new WmiClassMetadata(true, methods);
        }
        catch (ManagementException exception) when (
            exception.ErrorCode is ManagementStatus.InvalidClass or ManagementStatus.NotFound)
        {
            return new WmiClassMetadata(false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("WMI identifiers may contain only ASCII letters, digits, and underscores.", parameterName);
    }
}
