using System.IO;
using System.Runtime.InteropServices;

namespace IvyCaster.Agent.Manager.Services;

internal sealed class UnixPrivilegeContext : IPrivilegeContext
{
    public bool IsElevated() => GetEffectiveUid() == 0;

    private static int? GetEffectiveUid()
    {
        try
        {
            return checked((int)geteuid());
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            if (OperatingSystem.IsLinux() && TryReadEffectiveUidFromProcStatus(out var procUid))
            {
                return procUid;
            }

            return null;
        }
    }

    private static bool TryReadEffectiveUidFromProcStatus(out int effectiveUid)
    {
        effectiveUid = -1;
        const string procStatusPath = "/proc/self/status";
        if (!File.Exists(procStatusPath))
        {
            return false;
        }

        foreach (var line in File.ReadLines(procStatusPath))
        {
            if (!line.StartsWith("Uid:", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return false;
            }

            return int.TryParse(parts[2], out effectiveUid);
        }

        return false;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();
}
