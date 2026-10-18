using System.Security.AccessControl;
using System.Security.Principal;

namespace Atlas.Installer;

internal static class SecureDirectory
{
    public static DirectoryInfo Create(string path)
    {
        var directory = Directory.CreateDirectory(path);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("O diretório seguro não pode ser um ponto de redirecionamento.");
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("O usuário atual não possui SID.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        Add(security, user);
        Add(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
        Add(security, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        FileSystemAclExtensions.SetAccessControl(directory, security);
        return directory;
    }

    private static void Add(DirectorySecurity security, SecurityIdentifier identity) =>
        security.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
}
