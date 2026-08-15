namespace LegionLoqControl.Application.Broker;

public enum BrokerInstallMode
{
    Development = 0,
    Production = 1,
}

public enum BrokerInstallPlacement
{
    Missing = 0,
    SiblingDevelopment = 1,
    Protected = 2,
    Unprotected = 3,
}

public enum BrokerSignatureStatus
{
    Missing = 0,
    Unsigned = 1,
    Signed = 2,
    Invalid = 3,
}

[Flags]
public enum BrokerDirectoryRights
{
    None = 0,
    Write = 1,
    Delete = 2,
    ChangePermissions = 4,
    TakeOwnership = 8,
}

public sealed record BrokerAccessGrant(
    string IdentitySid,
    BrokerDirectoryRights Rights,
    bool Allow);

public sealed record BrokerDirectoryDescriptor(
    string OwnerSid,
    IReadOnlyList<BrokerAccessGrant> Grants);

public sealed record BrokerInstallAssessment(
    BrokerInstallPlacement Placement,
    BrokerSignatureStatus Signature,
    bool DirectoryProtected,
    bool AllowsDevelopmentRead,
    bool AllowsProductionRelease,
    string StatusCode);

public static class BrokerInstallPolicy
{
    public const string LocalSystemSid = "S-1-5-18";
    public const string BuiltinAdministratorsSid = "S-1-5-32-544";
    public const string TrustedInstallerSid =
        "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    public static readonly IReadOnlySet<string> ProtectedIdentitySids =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            LocalSystemSid,
            BuiltinAdministratorsSid,
            TrustedInstallerSid,
        };

    public static BrokerInstallAssessment Evaluate(
        string brokerPath,
        string clientDirectory,
        bool fileExists,
        BrokerSignatureStatus signature,
        BrokerDirectoryDescriptor? directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientDirectory);
        if (!Enum.IsDefined(signature))
            throw new ArgumentOutOfRangeException(nameof(signature));

        if (!fileExists)
        {
            return new BrokerInstallAssessment(
                BrokerInstallPlacement.Missing,
                BrokerSignatureStatus.Missing,
                DirectoryProtected: false,
                AllowsDevelopmentRead: false,
                AllowsProductionRelease: false,
                "broker_not_found");
        }

        bool sibling = AreSiblingDirectories(brokerPath, clientDirectory);
        bool directoryProtected = directory is not null && IsDirectoryProtected(directory);
        BrokerInstallPlacement placement = !sibling
            ? BrokerInstallPlacement.Unprotected
            : directoryProtected
                ? BrokerInstallPlacement.Protected
                : BrokerInstallPlacement.SiblingDevelopment;

        bool production = sibling &&
            directoryProtected &&
            signature == BrokerSignatureStatus.Signed;
        bool development = sibling &&
            signature is BrokerSignatureStatus.Unsigned or BrokerSignatureStatus.Signed;

        return new BrokerInstallAssessment(
            placement,
            signature,
            directoryProtected,
            development,
            production,
            ResolveStatusCode(placement, signature, development, production));
    }

    public static bool Allows(BrokerInstallAssessment assessment, BrokerInstallMode mode)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));

        return mode == BrokerInstallMode.Production
            ? assessment.AllowsProductionRelease
            : assessment.AllowsDevelopmentRead;
    }

    public static string RefusalCode(
        BrokerInstallAssessment assessment,
        BrokerInstallMode mode)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (Allows(assessment, mode))
        {
            throw new InvalidOperationException(
                "A permitted broker install has no refusal code.");
        }

        if (mode == BrokerInstallMode.Development)
            return assessment.StatusCode;
        if (assessment.Placement == BrokerInstallPlacement.Missing)
            return "broker_not_found";
        if (assessment.Signature == BrokerSignatureStatus.Invalid)
            return "broker_signature_invalid";
        if (!assessment.DirectoryProtected ||
            assessment.Placement == BrokerInstallPlacement.Unprotected)
        {
            return "broker_install_unprotected";
        }

        return "broker_unsigned";
    }

    public static BrokerInstallMode ResolveMode(string? configuredValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
            return BrokerInstallMode.Development;

        return configuredValue.Trim().ToLowerInvariant() switch
        {
            "production" => BrokerInstallMode.Production,
            "development" => BrokerInstallMode.Development,
            _ => throw new ArgumentException(
                "Broker install mode must be development or production.",
                nameof(configuredValue)),
        };
    }

    private static bool AreSiblingDirectories(string brokerPath, string clientDirectory)
    {
        string brokerDirectory = Path.GetDirectoryName(Path.GetFullPath(brokerPath))
            ?? string.Empty;
        return string.Equals(
            NormalizeDirectory(brokerDirectory),
            NormalizeDirectory(clientDirectory),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsDirectoryProtected(BrokerDirectoryDescriptor directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory.OwnerSid);
        ArgumentNullException.ThrowIfNull(directory.Grants);

        if (!ProtectedIdentitySids.Contains(directory.OwnerSid.Trim()))
            return false;

        foreach (BrokerAccessGrant grant in directory.Grants)
        {
            if (grant is null)
                throw new ArgumentException("Directory grants cannot contain null entries.");
            if (!grant.Allow || grant.Rights == BrokerDirectoryRights.None)
                continue;
            if (!ProtectedIdentitySids.Contains(grant.IdentitySid.Trim()))
                return false;
        }

        return true;
    }

    private static string ResolveStatusCode(
        BrokerInstallPlacement placement,
        BrokerSignatureStatus signature,
        bool development,
        bool production)
    {
        if (production)
            return "broker_install_protected";
        if (placement == BrokerInstallPlacement.Unprotected)
            return "broker_install_unprotected";
        if (signature == BrokerSignatureStatus.Invalid)
            return "broker_signature_invalid";
        if (placement == BrokerInstallPlacement.Protected &&
            signature != BrokerSignatureStatus.Signed)
        {
            return "broker_unsigned";
        }

        if (signature == BrokerSignatureStatus.Unsigned && development)
            return "broker_install_development";

        return "broker_install_unprotected";
    }
}
