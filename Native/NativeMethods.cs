using System.Runtime.InteropServices;

namespace MenuForge;

/// <summary>Small Win32 interop helpers.</summary>
internal static class NativeMethods
{
    private const uint SHCNE_ASSOCCHANGED = 0x08000000; // file associations changed
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>Tells Explorer to pick up association (context menu) changes immediately.</summary>
    public static void NotifyShellOfChange() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
}
