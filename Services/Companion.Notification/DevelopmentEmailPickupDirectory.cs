using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Companion.Notification;

internal static class DevelopmentEmailPickupDirectory
{
    private const UnixFileMode OwnerAccess = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OtherAccess =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    public static void Create(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            if (OperatingSystem.IsWindows())
                CreateWindowsDirectory(directory);
            else
                Directory.CreateDirectory(path, OwnerAccess);
        }

        EnsurePrivate(path);
    }

    public static void EnsurePrivate(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists || directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Development email pickup requires a private, non-linked directory.");

        var isPrivate = OperatingSystem.IsWindows()
            ? HasPrivateWindowsAcl(directory)
            : (File.GetUnixFileMode(path) & (OwnerAccess | OtherAccess)) == OwnerAccess &&
              (!OperatingSystem.IsMacOS() || HasNoMacAcl(path));
        if (!isPrivate)
            throw new InvalidOperationException(
                "Development email pickup requires a private directory. Use mode 0700 on Unix, with no extended ACL on macOS, or restrict the Windows ACL to your account and system administrators.");
    }

    [SupportedOSPlatform("windows")]
    private static void CreateWindowsDirectory(DirectoryInfo directory)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("Could not identify the Windows account for development email pickup.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            user, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        directory.Create(security);
    }

    [SupportedOSPlatform("windows")]
    private static bool HasPrivateWindowsAcl(DirectoryInfo directory)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("Could not identify the Windows account for development email pickup.");
        bool Trusted(IdentityReference? account) =>
            account is SecurityIdentifier sid &&
            (sid.Equals(user) || sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
             sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid));

        var acl = directory.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        return new RawSecurityDescriptor(acl.GetSecurityDescriptorBinaryForm(), 0).DiscretionaryAcl is not null &&
               Trusted(acl.GetOwner(typeof(SecurityIdentifier))) &&
               acl.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                   .OfType<FileSystemAccessRule>()
                   .All(rule => rule.AccessControlType != AccessControlType.Allow || Trusted(rule.IdentityReference));
    }

    [SupportedOSPlatform("macos")]
    private static bool HasNoMacAcl(string path)
    {
        // Darwin ACLs can grant access beyond the Unix mode bits.
        const int extendedAcl = 0x100;
        const int noEntry = 2;
        var acl = GetMacAcl(path, extendedAcl);
        if (acl == 0)
            return Marshal.GetLastPInvokeError() == noEntry;

        FreeMacAcl(acl);
        return false;
    }

    [DllImport("libSystem.B.dylib", EntryPoint = "acl_get_link_np", SetLastError = true)]
    private static extern nint GetMacAcl([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int type);

    [DllImport("libSystem.B.dylib", EntryPoint = "acl_free")]
    private static extern int FreeMacAcl(nint acl);
}
