using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Atlas.Cad.Core.Bridge;

namespace Atlas.AutoCAD.Adapter;

internal sealed class SecureBridgeFileStore
{
    private readonly string _root;

    public SecureBridgeFileStore()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtlasCad",
            "bridge",
            "v1");
    }

    public string EnsureRoot()
    {
        var directory = Directory.CreateDirectory(_root);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Bridge directory cannot be a reparse point.");

        RestrictToCurrentUser(directory);
        return directory.FullName;
    }

    public DrawMlineRequest ReadRequest(string requestId)
    {
        var path = RequestPath(requestId);
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new BridgeCommandException("request_not_found", "Request payload was not found.");
        if (file.Length <= 0 || file.Length > BridgeProtocol.MaximumRequestBytes)
            throw new BridgeCommandException("request_size_invalid", "Request payload size is invalid.");

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        return BridgeJson.ReadRequest(stream);
    }

    public EnsureStyleRequest ReadEnsureStyleRequest(string requestId)
    {
        var path = RequestPath(requestId);
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new BridgeCommandException("request_not_found", "Request payload was not found.");
        if (file.Length <= 0 || file.Length > BridgeProtocol.MaximumRequestBytes)
            throw new BridgeCommandException("request_size_invalid", "Request payload size is invalid.");

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        return BridgeJson.ReadEnsureStyleRequest(stream);
    }

    public void DeleteRequest(string requestId)
    {
        var path = RequestPath(requestId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void WriteResponse(string requestId, DrawMlineResponse response)
    {
        WriteResponseFile(requestId, stream => BridgeJson.WriteResponse(stream, response));
    }

    public void WriteResponse(string requestId, EnsureStyleResponse response)
    {
        WriteResponseFile(requestId, stream => BridgeJson.WriteEnsureStyleResponse(stream, response));
    }

    private void WriteResponseFile(string requestId, Action<Stream> write)
    {
        var responsePath = ResponsePath(requestId);
        var temporaryPath = responsePath + ".tmp";
        DeleteIfPresent(temporaryPath);
        DeleteIfPresent(responsePath);

        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                write(stream);
                stream.Flush();
            }

            File.Move(temporaryPath, responsePath);
        }
        finally
        {
            DeleteIfPresent(temporaryPath);
        }
    }

    private string RequestPath(string requestId) => PathFor(requestId, BridgeProtocol.RequestSuffix);

    private string ResponsePath(string requestId) => PathFor(requestId, BridgeProtocol.ResponseSuffix);

    private string PathFor(string requestId, string suffix)
    {
        if (!BridgeProtocol.IsValidRequestId(requestId))
            throw new BridgeCommandException("request_id_invalid", "Request ID is invalid.");

        var root = EnsureRoot();
        var path = Path.GetFullPath(Path.Combine(root, requestId + suffix));
        var expectedPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new BridgeCommandException("request_path_invalid", "Request path escaped the bridge directory.");
        return path;
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    internal static void RestrictToCurrentUser(DirectoryInfo directory)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("Current Windows user has no SID.");
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        AddFullControl(security, user);
        AddFullControl(security, system);
        AddFullControl(security, administrators);

#if NETFRAMEWORK
        directory.SetAccessControl(security);
#else
        FileSystemAclExtensions.SetAccessControl(directory, security);
#endif
    }

    private static void AddFullControl(DirectorySecurity security, SecurityIdentifier identity)
    {
        security.AddAccessRule(new FileSystemAccessRule(
            identity,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
    }
}

internal sealed class BridgeCommandException : Exception
{
    public BridgeCommandException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
