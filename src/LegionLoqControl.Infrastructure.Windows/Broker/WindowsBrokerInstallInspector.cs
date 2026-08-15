using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using LegionLoqControl.Application.Broker;

namespace LegionLoqControl.Infrastructure.Windows.Broker;

public static class WindowsBrokerInstallInspector
{
    private const int SeFileObject = 1;
    private const int OwnerSecurityInformation = 0x1;
    private const int DaclSecurityInformation = 0x4;
    private const int FileWriteData = 0x0002;
    private const int FileAppendData = 0x0004;
    private const int FileDeleteChild = 0x0040;
    private const int Delete = 0x00010000;
    private const int WriteDac = 0x00040000;
    private const int WriteOwner = 0x00080000;
    private const int GenericWrite = 0x40000000;
    private const int GenericAll = 0x10000000;

    public static BrokerInstallAssessment Assess(
        string brokerPath,
        string clientDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientDirectory);

        string fullBrokerPath = Path.GetFullPath(brokerPath);
        bool exists = File.Exists(fullBrokerPath);
        BrokerSignatureStatus signature = exists
            ? ReadSignature(fullBrokerPath)
            : BrokerSignatureStatus.Missing;
        BrokerDirectoryDescriptor? directory = null;
        string? directoryPath = Path.GetDirectoryName(fullBrokerPath);
        if (directoryPath is not null && Directory.Exists(directoryPath))
            directory = ReadDirectory(directoryPath);

        return BrokerInstallPolicy.Evaluate(
            fullBrokerPath,
            clientDirectory,
            exists,
            signature,
            directory);
    }

    internal static BrokerSignatureStatus ReadSignature(string brokerPath)
    {
        try
        {
#pragma warning disable SYSLIB0057 // PE Authenticode has no X509CertificateLoader equivalent.
            using var certificate = X509Certificate.CreateFromSignedFile(brokerPath);
#pragma warning restore SYSLIB0057
            return certificate.Handle == nint.Zero
                ? BrokerSignatureStatus.Unsigned
                : BrokerSignatureStatus.Signed;
        }
        catch (CryptographicException)
        {
            return BrokerSignatureStatus.Unsigned;
        }
        catch (Exception)
        {
            return BrokerSignatureStatus.Invalid;
        }
    }

    internal static BrokerDirectoryDescriptor ReadDirectory(string directoryPath)
    {
        uint error = GetNamedSecurityInfo(
            directoryPath,
            SeFileObject,
            OwnerSecurityInformation | DaclSecurityInformation,
            out _,
            out _,
            out _,
            out _,
            out nint securityDescriptor);
        if (error != 0)
            throw new System.ComponentModel.Win32Exception(unchecked((int)error));

        try
        {
            int length = GetSecurityDescriptorLength(securityDescriptor);
            byte[] bytes = new byte[length];
            Marshal.Copy(securityDescriptor, bytes, 0, length);
            var raw = new RawSecurityDescriptor(bytes, 0);
            string owner = raw.Owner?.Value
                ?? throw new InvalidDataException("The directory owner SID is missing.");
            var grants = new List<BrokerAccessGrant>();
            if (raw.DiscretionaryAcl is not null)
            {
                foreach (GenericAce ace in raw.DiscretionaryAcl)
                {
                    if (ace is not CommonAce common ||
                        (common.AceFlags & AceFlags.InheritOnly) == AceFlags.InheritOnly)
                    {
                        continue;
                    }

                    BrokerDirectoryRights rights = MapRights(common.AccessMask);
                    if (rights == BrokerDirectoryRights.None)
                        continue;

                    grants.Add(new BrokerAccessGrant(
                        common.SecurityIdentifier.Value,
                        rights,
                        common.AceType == AceType.AccessAllowed));
                }
            }

            return new BrokerDirectoryDescriptor(owner, grants);
        }
        finally
        {
            _ = LocalFree(securityDescriptor);
        }
    }

    private static BrokerDirectoryRights MapRights(int accessMask)
    {
        if ((accessMask & GenericAll) != 0)
        {
            return BrokerDirectoryRights.Write |
                BrokerDirectoryRights.Delete |
                BrokerDirectoryRights.ChangePermissions |
                BrokerDirectoryRights.TakeOwnership;
        }

        BrokerDirectoryRights rights = BrokerDirectoryRights.None;
        if ((accessMask & (FileWriteData | FileAppendData | GenericWrite)) != 0)
            rights |= BrokerDirectoryRights.Write;
        if ((accessMask & (Delete | FileDeleteChild)) != 0)
            rights |= BrokerDirectoryRights.Delete;
        if ((accessMask & WriteDac) != 0)
            rights |= BrokerDirectoryRights.ChangePermissions;
        if ((accessMask & WriteOwner) != 0)
            rights |= BrokerDirectoryRights.TakeOwnership;
        return rights;
    }

    [DllImport("advapi32.dll", EntryPoint = "GetNamedSecurityInfoW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetNamedSecurityInfo(
        string objectName,
        int objectType,
        int securityInfo,
        out nint sidOwner,
        out nint sidGroup,
        out nint dacl,
        out nint sacl,
        out nint securityDescriptor);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetSecurityDescriptorLength(nint securityDescriptor);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint LocalFree(nint handle);
}
