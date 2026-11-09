using System.Runtime.InteropServices;
using System.Text;

namespace MenuForge;

/// <summary>Small Win32 interop helpers.</summary>
internal static class NativeMethods
{
    private const uint SHCNE_ASSOCCHANGED = 0x08000000; // file associations changed
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf, IntPtr ppvReserved);

    /// <summary>Tells Explorer to pick up association (context menu) changes immediately.</summary>
    public static void NotifyShellOfChange() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    /// <summary>
    /// Resolves indirect resource strings such as "@shell32.dll,-8506" to their localized text.
    /// Plain strings are returned unchanged; returns null when a resource reference cannot be resolved.
    /// </summary>
    public static string? ResolveIndirectString(string value)
    {
        if (!value.StartsWith('@')) return value;

        var buffer = new StringBuilder(512);
        var hr = SHLoadIndirectString(value, buffer, (uint)buffer.Capacity, IntPtr.Zero);
        if (hr != 0) return null;

        var text = buffer.ToString();
        return text.Length == 0 ? null : text;
    }
}
