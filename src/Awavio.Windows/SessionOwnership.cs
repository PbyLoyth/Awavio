using System.Security.Principal;

namespace Awavio.Windows;

internal static class SessionOwnership
{
    // One signed-in Windows account can have several sessions. Its data folder
    // is shared, so the session owner must be shared across those sessions too.
    public static string MutexName
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return @"Global\Awavio.Windows.Owner." + identity.User!.Value;
        }
    }
}
