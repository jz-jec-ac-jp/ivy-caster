namespace IvyCaster.Agent.Manager.Services;

internal interface IPrivilegeContext
{
    bool IsElevated();
}

internal sealed class PlatformPrivilegeContext : IPrivilegeContext
{
    private readonly IPrivilegeContext _inner;

    public PlatformPrivilegeContext()
    {
        _inner = CreateForCurrentPlatform();
    }

    public bool IsElevated()
        => _inner.IsElevated();

    private static IPrivilegeContext CreateForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPrivilegeContext();
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new UnixPrivilegeContext();
        }

        return new UnsupportedPlatformPrivilegeContext();
    }
}

internal sealed class UnsupportedPlatformPrivilegeContext : IPrivilegeContext
{
    public bool IsElevated() => false;
}
