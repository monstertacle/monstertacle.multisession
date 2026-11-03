using System.Diagnostics;

namespace Monstertacle.Multisession.ValidationHost;

// Names the first frame outside the runtime libraries of a thrown exception as Type.Method, unwrapping async state machines.
// Only type and member names are kept: no message, file path or line.
internal static class ThrowSite
{
    private static readonly string[] _runtimeNamespaces = ["System", "Microsoft"];

    internal static string? Of(Exception failure)
    {
        foreach (StackFrame frame in new StackTrace(failure, fNeedFileInfo: false).GetFrames())
        {
            if (frame.GetMethod() is not { DeclaringType: { } type } method || IsRuntime(type)) { continue; }
            return type.Name.StartsWith('<') && type.DeclaringType is { } owner
                ? owner.Name + "." + type.Name[1..type.Name.IndexOf('>', StringComparison.Ordinal)]
                : type.Name + "." + method.Name;
        }
        return null;
    }

    private static bool IsRuntime(Type type) => type.Namespace is { } name
        && _runtimeNamespaces.Any(root => name == root || name.StartsWith(root + ".", StringComparison.Ordinal));
}
