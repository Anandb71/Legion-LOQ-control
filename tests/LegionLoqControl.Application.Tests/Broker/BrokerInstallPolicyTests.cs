using LegionLoqControl.Application.Broker;
using Xunit;

namespace LegionLoqControl.Application.Tests.Broker;

public sealed class BrokerInstallPolicyTests
{
    [Fact]
    public void Missing_broker_is_never_launchable()
    {
        BrokerInstallAssessment assessment = BrokerInstallPolicy.Evaluate(
            Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
            ClientDirectory,
            fileExists: false,
            BrokerSignatureStatus.Missing,
            directory: null);

        Assert.Equal(BrokerInstallPlacement.Missing, assessment.Placement);
        Assert.Equal("broker_not_found", assessment.StatusCode);
        Assert.False(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Development));
        Assert.False(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Production));
    }

    [Fact]
    public void Unsigned_sibling_in_a_user_writable_directory_is_development_only()
    {
        BrokerInstallAssessment assessment = BrokerInstallPolicy.Evaluate(
            Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
            ClientDirectory,
            fileExists: true,
            BrokerSignatureStatus.Unsigned,
            UserWritableDirectory());

        Assert.Equal(BrokerInstallPlacement.SiblingDevelopment, assessment.Placement);
        Assert.Equal("broker_install_development", assessment.StatusCode);
        Assert.True(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Development));
        Assert.False(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Production));
    }

    [Fact]
    public void Signed_sibling_under_administrator_acls_is_production_ready()
    {
        BrokerInstallAssessment assessment = BrokerInstallPolicy.Evaluate(
            Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
            ClientDirectory,
            fileExists: true,
            BrokerSignatureStatus.Signed,
            ProtectedDirectory());

        Assert.Equal(BrokerInstallPlacement.Protected, assessment.Placement);
        Assert.True(assessment.DirectoryProtected);
        Assert.Equal("broker_install_protected", assessment.StatusCode);
        Assert.True(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Development));
        Assert.True(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Production));
    }

    [Fact]
    public void Protected_but_unsigned_broker_is_not_a_production_release()
    {
        BrokerInstallAssessment assessment = BrokerInstallPolicy.Evaluate(
            Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
            ClientDirectory,
            fileExists: true,
            BrokerSignatureStatus.Unsigned,
            ProtectedDirectory());

        Assert.Equal(BrokerInstallPlacement.Protected, assessment.Placement);
        Assert.Equal("broker_unsigned", assessment.StatusCode);
        Assert.True(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Development));
        Assert.False(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Production));
    }

    [Fact]
    public void Non_sibling_path_is_unprotected_even_when_signed()
    {
        BrokerInstallAssessment assessment = BrokerInstallPolicy.Evaluate(
            Path.Combine(Path.GetTempPath(), "other", "LegionLoqControl.Broker.exe"),
            ClientDirectory,
            fileExists: true,
            BrokerSignatureStatus.Signed,
            ProtectedDirectory());

        Assert.Equal(BrokerInstallPlacement.Unprotected, assessment.Placement);
        Assert.Equal("broker_install_unprotected", assessment.StatusCode);
        Assert.False(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Development));
        Assert.False(BrokerInstallPolicy.Allows(assessment, BrokerInstallMode.Production));
    }

    [Fact]
    public void Users_write_or_non_admin_owner_fail_closed()
    {
        var usersWrite = new BrokerDirectoryDescriptor(
            BrokerInstallPolicy.BuiltinAdministratorsSid,
            [
                new(
                    BrokerInstallPolicy.BuiltinAdministratorsSid,
                    BrokerDirectoryRights.Write,
                    Allow: true),
                new("S-1-5-32-545", BrokerDirectoryRights.Write, Allow: true),
            ]);
        var userOwned = new BrokerDirectoryDescriptor(
            "S-1-5-21-1-2-3-1001",
            [
                new(
                    BrokerInstallPolicy.LocalSystemSid,
                    BrokerDirectoryRights.Write,
                    Allow: true),
            ]);

        Assert.False(
            BrokerInstallPolicy.Evaluate(
                Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
                ClientDirectory,
                fileExists: true,
                BrokerSignatureStatus.Signed,
                usersWrite).DirectoryProtected);
        Assert.False(
            BrokerInstallPolicy.Evaluate(
                Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
                ClientDirectory,
                fileExists: true,
                BrokerSignatureStatus.Signed,
                userOwned).DirectoryProtected);

        var usersDeleteChild = new BrokerDirectoryDescriptor(
            BrokerInstallPolicy.BuiltinAdministratorsSid,
            [
                new(
                    BrokerInstallPolicy.BuiltinAdministratorsSid,
                    BrokerDirectoryRights.Write,
                    Allow: true),
                new("S-1-5-32-545", BrokerDirectoryRights.Delete, Allow: true),
            ]);
        Assert.False(
            BrokerInstallPolicy.Evaluate(
                Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
                ClientDirectory,
                fileExists: true,
                BrokerSignatureStatus.Signed,
                usersDeleteChild).DirectoryProtected);
    }

    [Fact]
    public void Invalid_signature_blocks_every_mode()
    {
        BrokerInstallAssessment assessment = BrokerInstallPolicy.Evaluate(
            Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
            ClientDirectory,
            fileExists: true,
            BrokerSignatureStatus.Invalid,
            ProtectedDirectory());

        Assert.Equal("broker_signature_invalid", assessment.StatusCode);
        Assert.False(assessment.AllowsDevelopmentRead);
        Assert.False(assessment.AllowsProductionRelease);
    }

    [Theory]
    [InlineData(null, BrokerInstallMode.Development)]
    [InlineData("development", BrokerInstallMode.Development)]
    [InlineData("PRODUCTION", BrokerInstallMode.Production)]
    public void Mode_defaults_to_development(string? value, BrokerInstallMode expected)
    {
        Assert.Equal(expected, BrokerInstallPolicy.ResolveMode(value));
    }

    [Fact]
    public void Unknown_mode_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => BrokerInstallPolicy.ResolveMode("loose"));
    }

    [Fact]
    public void Production_refusal_does_not_use_the_development_status_code()
    {
        BrokerInstallAssessment assessment = BrokerInstallPolicy.Evaluate(
            Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
            ClientDirectory,
            fileExists: true,
            BrokerSignatureStatus.Unsigned,
            UserWritableDirectory());

        Assert.Equal(
            "broker_install_unprotected",
            BrokerInstallPolicy.RefusalCode(assessment, BrokerInstallMode.Production));
    }

    [Fact]
    public void Production_refusal_for_a_protected_unsigned_broker_is_unsigned()
    {
        BrokerInstallAssessment assessment = BrokerInstallPolicy.Evaluate(
            Path.Combine(ClientDirectory, "LegionLoqControl.Broker.exe"),
            ClientDirectory,
            fileExists: true,
            BrokerSignatureStatus.Unsigned,
            ProtectedDirectory());

        Assert.Equal(
            "broker_unsigned",
            BrokerInstallPolicy.RefusalCode(assessment, BrokerInstallMode.Production));
    }

    private static string ClientDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "LegionLoqControl-client");

    private static BrokerDirectoryDescriptor UserWritableDirectory() =>
        new(
            "S-1-5-21-1-2-3-1001",
            [
                new("S-1-5-21-1-2-3-1001", BrokerDirectoryRights.Write, Allow: true),
            ]);

    private static BrokerDirectoryDescriptor ProtectedDirectory() =>
        new(
            BrokerInstallPolicy.BuiltinAdministratorsSid,
            [
                new(
                    BrokerInstallPolicy.BuiltinAdministratorsSid,
                    BrokerDirectoryRights.Write | BrokerDirectoryRights.Delete,
                    Allow: true),
                new(
                    BrokerInstallPolicy.LocalSystemSid,
                    BrokerDirectoryRights.Write,
                    Allow: true),
                new(
                    BrokerInstallPolicy.TrustedInstallerSid,
                    BrokerDirectoryRights.ChangePermissions |
                    BrokerDirectoryRights.TakeOwnership,
                    Allow: true),
            ]);
}
