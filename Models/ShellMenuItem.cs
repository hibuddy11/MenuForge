namespace MenuForge.Models;

public enum RegistrySource
{
    /// <summary>Per-user entries under HKCU\Software\Classes.</summary>
    CurrentUser,

    /// <summary>Machine-wide (all users) entries under HKLM\Software\Classes.</summary>
    LocalMachine
}

public enum MenuScope
{
    /// <summary>Right-click on any file.</summary>
    AllFiles,

    /// <summary>Right-click on any file or folder (AllFilesystemObjects).</summary>
    AllFilesystemObjects,

    /// <summary>Right-click on a folder.</summary>
    Folders,

    /// <summary>Right-click on empty space inside a folder.</summary>
    FolderBackground,

    /// <summary>Right-click on a drive.</summary>
    Drives,

    /// <summary>Right-click on files of a specific extension.</summary>
    FileExtension,
}

/// <summary>
/// A place in the File Explorer context menu where an item can appear.
/// </summary>
public sealed record MenuLocation(MenuScope Scope, string? Extension)
{
    public override string ToString() => Scope switch
    {
        MenuScope.AllFiles => "All files",
        MenuScope.AllFilesystemObjects => "All files and folders",
        MenuScope.Folders => "Folders",
        MenuScope.FolderBackground => "Folder background",
        MenuScope.Drives => "Drives",
        MenuScope.FileExtension => $"{Extension} files",
        _ => Scope.ToString()
    };
}

/// <summary>
/// A custom context-menu entry backed by a shell verb under
/// HKCU\Software\Classes (per user) or HKLM\Software\Classes (all users).
/// </summary>
public sealed class ShellMenuItem
{
    /// <summary>Registry key name of the shell verb, e.g. "MenuForge.OpenWithVSCode".</summary>
    public string Id { get; set; } = "";

    /// <summary>Where the entry is registered: current user or all users.</summary>
    public RegistrySource Source { get; set; } = RegistrySource.CurrentUser;

    /// <summary>Text shown in the context menu (may contain &amp; for the access key).</summary>
    public string Title { get; set; } = "";

    /// <summary>Title exactly as stored in the registry, e.g. "@shell32.dll,-8506" (null for new items).</summary>
    public string? RawTitle { get; set; }

    /// <summary>Title as first loaded (resource references resolved); used to detect user edits.</summary>
    public string? LoadedTitle { get; set; }

    /// <summary>Optional icon specification, e.g. "C:\app\app.exe,0".</summary>
    public string? Icon { get; set; }

    /// <summary>Executable to run (path only, without arguments).</summary>
    public string Command { get; set; } = "";

    /// <summary>Arguments appended to the command. Supports Explorer placeholders (%1, %V, ...).</summary>
    public string Arguments { get; set; } = "";

    /// <summary>Only show the entry while Shift is held (Extended verb).</summary>
    public bool Extended { get; set; }

    /// <summary>All menu locations this item is registered at.</summary>
    public List<MenuLocation> Locations { get; set; } = new();
}
