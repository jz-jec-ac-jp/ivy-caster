using System.Security.Principal;
using System.Runtime.Versioning;

namespace IvyCaster.Agent.Manager.Services;

[SupportedOSPlatform("windows")]
internal sealed class WindowsPrivilegeContext : IPrivilegeContext
{
    public bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
