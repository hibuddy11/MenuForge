using MenuForge.Models;
using Microsoft.Win32;

namespace MenuForge.Services;

/// <summary>
/// Reads and writes custom shell verbs (context-menu entries) under
/// HKCU\Software\Classes (per user) and HKLM\Software\Classes (all users).
/// </summary>
public static class ShellMenuRegistry
{
    private const string Root = @"Software\Classes";

    /// <summary>Registry values that make Explorer skip a verb.</summary>
    private static readonly string[] HideMarkers =
    {
        "LegacyDisable", "ProgrammaticAccessOnly", "HideBasedOnVelocityId"
    };

    /// <summary>Loads every command-style custom entry found under the managed locations.</summary>
    public static List<ShellMenuItem> LoadAll()
    {
        var byKey = new Dictionary<(RegistrySource Source, string Id), ShellMenuItem>();

        foreach (var source in new[] { RegistrySource.CurrentUser, RegistrySource.LocalMachine })
        {
            foreach (var location in EnumerateLocations(source))
            {
                using var shellKey = OpenClasses(source).OpenSubKey(GetShellPath(location));
                if (shellKey is null) continue;

                foreach (var id in shellKey.GetSubKeyNames())
                {
                    using var key = shellKey.OpenSubKey(id);
                    if (key is null) continue;

                    using var commandKey = key.OpenSubKey("command");
                    if (commandKey is null) continue; // skip cascade menus and shell extensions

                    var itemKey = (source, id);
                    if (byKey.TryGetValue(itemKey, out var item))
                    {
                        item.Locations.Add(location);
                        continue;
                    }

                    var (command, arguments) = SplitCommand(commandKey.GetValue("") as string ?? "");
                    var rawTitle = key.GetValue("MUIVerb") as string
                                   ?? key.GetValue("") as string;
                    var title = string.IsNullOrWhiteSpace(rawTitle)
                        ? id
                        : NativeMethods.ResolveIndirectString(rawTitle.Trim()) ?? id;
                    item = new ShellMenuItem
                    {
                        Id = id,
                        Source = source,
                        Title = title,
                        RawTitle = rawTitle,
                        LoadedTitle = title,
                        Icon = key.GetValue("Icon") as string,
                        Command = command,
                        Arguments = arguments,
                        Extended = key.GetValueNames().Contains("Extended"),
                        Hidden = HideMarkers.Any(m => key.GetValue(m) is not null),
                        Locations = { location }
                    };
                    byKey[itemKey] = item;
                }
            }
        }

        return byKey.Values
            .OrderBy(i => i.Source) // per-user entries first
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Creates or updates an item: writes it at every location listed in <see cref="ShellMenuItem.Locations"/>
    /// and removes it from locations that are no longer selected.
    /// </summary>
    public static void Save(ShellMenuItem item)
    {
        if (item.Locations.Count == 0)
            throw new InvalidOperationException("At least one menu location is required.");

        if (string.IsNullOrWhiteSpace(item.Id))
            item.Id = GenerateId(item.Title);

        foreach (var location in FindLocations(item.Source, item.Id))
        {
            if (!HasLocation(item.Locations, location))
                DeleteAt(item.Source, location, item.Id);
        }

        foreach (var location in item.Locations)
        {
            WriteItem(item.Source, location, item);
        }

        NativeMethods.NotifyShellOfChange();
    }

    /// <summary>Removes the item from every location it currently occupies.</summary>
    public static void Delete(ShellMenuItem item)
    {
        foreach (var location in FindLocations(item.Source, item.Id))
        {
            DeleteAt(item.Source, location, item.Id);
        }
        NativeMethods.NotifyShellOfChange();
    }

    private static RegistryKey OpenClasses(RegistrySource source) => source switch
    {
        RegistrySource.CurrentUser => Registry.CurrentUser,
        RegistrySource.LocalMachine => Registry.LocalMachine,
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    private static void WriteItem(RegistrySource source, MenuLocation location, ShellMenuItem item)
    {
        using var key = OpenClasses(source).CreateSubKey(GetShellPath(location) + "\\" + item.Id);

        var titleUnchanged = item.RawTitle is not null && item.Title == item.LoadedTitle;
        if (!titleUnchanged)
        {
            key.SetValue("", item.Title);
            key.DeleteValue("MUIVerb", throwOnMissingValue: false); // MUIVerb would override the new title
        }
        else if (key.GetValue("MUIVerb") is null && key.GetValue("") is null)
        {
            // Newly added location of an existing item: keep the original (possibly resource) title.
            key.SetValue("MUIVerb", item.RawTitle!);
        }

        if (!string.IsNullOrWhiteSpace(item.Icon))
            key.SetValue("Icon", item.Icon);
        else
            key.DeleteValue("Icon", throwOnMissingValue: false);

        if (item.Extended)
            key.SetValue("Extended", "");
        else
            key.DeleteValue("Extended", throwOnMissingValue: false);

        if (item.Hidden)
        {
            key.SetValue("LegacyDisable", "");
        }
        else
        {
            foreach (var marker in HideMarkers)
                key.DeleteValue(marker, throwOnMissingValue: false);
        }

        using var commandKey = key.CreateSubKey("command");
        commandKey.SetValue("", JoinCommand(item.Command, item.Arguments));
    }

    private static void DeleteAt(RegistrySource source, MenuLocation location, string id)
    {
        OpenClasses(source).DeleteSubKeyTree(GetShellPath(location) + "\\" + id, throwOnMissingSubKey: false);
    }

    private static IEnumerable<MenuLocation> EnumerateLocations(RegistrySource source)
    {
        yield return new MenuLocation(MenuScope.AllFilesystemObjects, null);
        yield return new MenuLocation(MenuScope.AllFiles, null);
        yield return new MenuLocation(MenuScope.Folders, null);
        yield return new MenuLocation(MenuScope.FolderBackground, null);
        yield return new MenuLocation(MenuScope.Drives, null);

        using var sysAssoc = OpenClasses(source).OpenSubKey(Root + @"\SystemFileAssociations");
        if (sysAssoc is null) yield break;

        foreach (var name in sysAssoc.GetSubKeyNames())
        {
            if (!name.StartsWith('.')) continue;
            using var shellKey = sysAssoc.OpenSubKey(name + @"\shell");
            if (shellKey is null) continue;
            yield return new MenuLocation(MenuScope.FileExtension, name);
        }
    }

    private static List<MenuLocation> FindLocations(RegistrySource source, string id)
    {
        var result = new List<MenuLocation>();
        foreach (var location in EnumerateLocations(source))
        {
            using var shellKey = OpenClasses(source).OpenSubKey(GetShellPath(location));
            if (shellKey?.GetSubKeyNames().Contains(id, StringComparer.OrdinalIgnoreCase) == true)
                result.Add(location);
        }
        return result;
    }

    private static bool HasLocation(IReadOnlyList<MenuLocation> locations, MenuLocation location) =>
        locations.Any(l =>
            l.Scope == location.Scope &&
            (l.Scope != MenuScope.FileExtension ||
             string.Equals(NormalizeExtension(l.Extension), NormalizeExtension(location.Extension),
                 StringComparison.OrdinalIgnoreCase)));

    private static string GetShellPath(MenuLocation location) => location.Scope switch
    {
        MenuScope.AllFilesystemObjects => Root + @"\AllFilesystemObjects\shell",
        MenuScope.AllFiles => Root + @"\*\shell",
        MenuScope.Folders => Root + @"\Directory\shell",
        MenuScope.FolderBackground => Root + @"\Directory\Background\shell",
        MenuScope.Drives => Root + @"\Drive\shell",
        MenuScope.FileExtension => Root + @"\SystemFileAssociations\" + NormalizeExtension(location.Extension) + @"\shell",
        _ => throw new ArgumentOutOfRangeException(nameof(location))
    };

    /// <summary>
    /// True for machine-wide entries that belong to Windows itself: the command points into the
    /// Windows directory (or is empty, e.g. DelegateExecute handlers). Per-user entries are never system entries.
    /// </summary>
    public static bool IsWindowsEntry(ShellMenuItem item)
    {
        if (item.Source != RegistrySource.LocalMachine) return false;
        if (string.IsNullOrWhiteSpace(item.Command)) return true;

        var command = Environment.ExpandEnvironmentVariables(item.Command.Trim());
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows)
            .TrimEnd('\\') + "\\";

        if (!Path.IsPathRooted(command))
            return File.Exists(Path.Combine(Environment.SystemDirectory, command)); // e.g. "rundll32.exe"

        return command.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Turns "txt", ".txt" or "*.TXT" into ".txt".</summary>
    public static string NormalizeExtension(string? extension)
    {
        var value = (extension ?? "").Trim().TrimStart('*', '.').Trim();
        return value.Length == 0 ? "" : "." + value.ToLowerInvariant();
    }

    private static string GenerateId(string title)
    {
        var slug = new System.Text.StringBuilder();
        foreach (var part in title.Split(new[] { ' ', '\t', '-', '_', '&', '.', ',' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var letters = new string(part.Where(char.IsLetterOrDigit).ToArray());
            if (letters.Length == 0) continue;
            slug.Append(char.ToUpperInvariant(letters[0]));
            if (letters.Length > 1)
                slug.Append(letters[1..].ToLowerInvariant());
        }
        if (slug.Length == 0)
            slug.Append("Item");

        var id = "MenuForge." + slug;
        var suffix = 2;
        while (FindLocations(RegistrySource.CurrentUser, id).Count > 0) // new items are per-user
            id = $"MenuForge.{slug}{suffix++}";
        return id;
    }

    /// <summary>Splits "C:\app.exe" --arg "%1" into the executable and the argument string.</summary>
    internal static (string Command, string Arguments) SplitCommand(string full)
    {
        var value = full.Trim();
        if (value.Length == 0) return ("", "");

        if (value[0] == '"')
        {
            var end = value.IndexOf('"', 1);
            if (end > 1)
                return (value[1..end], value[(end + 1)..].Trim());
        }

        var space = value.IndexOf(' ');
        return space < 0 ? (value, "") : (value[..space], value[(space + 1)..].Trim());
    }

    private static string JoinCommand(string command, string arguments)
    {
        var exe = command.Trim().Trim('"').Trim();
        return string.IsNullOrWhiteSpace(arguments)
            ? $"\"{exe}\""
            : $"\"{exe}\" {arguments.Trim()}";
    }
}
