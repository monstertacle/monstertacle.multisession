using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Monstertacle.Multisession.Windows;

// Security is supplied at creation, before any object can inherit a broader default ACL.
internal sealed class WindowsPrivateSecurity : IDisposable
{
    private nint _memory;
    internal nint Pointer => _memory;
    internal WindowsPrivateSecurity(bool directory)
    {
        SecurityIdentifier owner = CurrentUser();
        RawSecurityDescriptor descriptor = new($"O:{owner.Value}D:P(A;{(directory ? "OICI" : "")};FA;;;{owner.Value})");
        byte[] bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        const int attributesSize = 24; // SECURITY_ATTRIBUTES layout for the explicitly supported Windows x64 ABI.
        _memory = Marshal.AllocHGlobal(attributesSize + bytes.Length);
        Marshal.WriteInt32(_memory, attributesSize);
        Marshal.WriteIntPtr(_memory, 8, _memory + attributesSize);
        Marshal.WriteInt32(_memory, 16, 0);
        Marshal.Copy(bytes, 0, _memory + attributesSize, bytes.Length);
    }
    internal static void Require(string path, bool directory)
    {
        FileSystemSecurity security = directory
            ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access)
            : new FileInfo(path).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier owner = identity.User ?? throw WindowsNative.Failure();
        IdentityReference? actualOwner = security.GetOwner(typeof(SecurityIdentifier));
        bool validOwner = owner.Equals(actualOwner) || (!directory && identity.Owner is { } tokenOwner
            && tokenOwner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) && tokenOwner.Equals(actualOwner));
        AuthorizationRuleCollection rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
        if (!validOwner || rules.Count != 1
            || rules[0] is not FileSystemAccessRule rule || !owner.Equals(rule.IdentityReference)
            || rule.AccessControlType != AccessControlType.Allow || rule.FileSystemRights != FileSystemRights.FullControl
            || rule.PropagationFlags != PropagationFlags.None
            || rule.InheritanceFlags != (directory ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None)
            || (directory && (!security.AreAccessRulesProtected || rule.IsInherited)))
        {
            throw WindowsNative.Failure();
        }
    }
    private static SecurityIdentifier CurrentUser()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return identity.User ?? throw WindowsNative.Failure();
    }
    public void Dispose()
    {
        if (_memory == 0) { return; }
        Marshal.FreeHGlobal(_memory);
        _memory = 0;
    }
}
