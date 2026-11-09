using System.Collections;
using System.Diagnostics;
using MenuForge.Models;
using MenuForge.Services;

namespace MenuForge.UI;

public sealed class MainForm : Form
{
    private static readonly string[] ColumnNames =
    {
        "Menu title", "Source", "Shows on", "Command", "Arguments", "Icon", "Shift only"
    };

    private static readonly int[] ColumnWidths = { 230, 90, 190, 230, 170, 160, 70 };

    private readonly ListView _list = new();
    private readonly Button _btnAdd = new() { Text = "&Add...", AutoSize = true };
    private readonly Button _btnEdit = new() { Text = "&Edit...", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "&Delete", AutoSize = true };
    private readonly Button _btnRefresh = new() { Text = "&Refresh", AutoSize = true };
    private readonly Label _status = new();
    private readonly Label _hintAdmin = new();
    private readonly Label _hintWin11 = new();
    private int _sortColumn = -1;

    public MainForm()
    {
        Text = "MenuForge";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 520);
        Size = new Size(1080, 660);
        Font = new Font("Segoe UI", 9F);

        BuildList();
        BuildTopBar();
        BuildBottomBar();

        Load += (s, e) => LoadItems();
    }

    private void BuildList()
    {
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.MultiSelect = false;
        _list.Dock = DockStyle.Fill;
        for (var i = 0; i < ColumnNames.Length; i++)
            _list.Columns.Add(ColumnNames[i], ColumnWidths[i]);
        _list.Columns[6].TextAlign = HorizontalAlignment.Center;

        _list.DoubleClick += (s, e) => EditSelected();
        _list.SelectedIndexChanged += (s, e) => UpdateButtons();
        _list.ColumnClick += (s, e) => OnColumnClick(e.Column);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("&Edit...", null, (s, e) => EditSelected());
        contextMenu.Items.Add("&Delete", null, (s, e) => DeleteSelected());
        contextMenu.Opening += (s, e) =>
        {
            var hit = _list.HitTest(_list.PointToClient(Cursor.Position));
            if (hit.Item is null)
            {
                e.Cancel = true;
                return;
            }
            hit.Item.Selected = true;
        };
        _list.ContextMenuStrip = contextMenu;

        Controls.Add(_list);
    }

    private void BuildTopBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8, 8, 8, 4)
        };
        _btnAdd.Click += (s, e) => AddNew();
        _btnEdit.Click += (s, e) => EditSelected();
        _btnDelete.Click += (s, e) => DeleteSelected();
        _btnRefresh.Click += (s, e) => LoadItems();
        bar.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete, _btnRefresh });
        Controls.Add(bar);
    }

    private void BuildBottomBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(8, 4, 8, 6) };
        _status.Dock = DockStyle.Top;
        _status.Height = 18;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _hintAdmin.Dock = DockStyle.Top;
        _hintAdmin.Height = 16;
        _hintAdmin.TextAlign = ContentAlignment.MiddleLeft;
        _hintAdmin.ForeColor = SystemColors.GrayText;
        _hintAdmin.Text = "Entries from all users (HKLM) are listed too; changing them requires administrator rights.";
        _hintWin11.Dock = DockStyle.Fill;
        _hintWin11.TextAlign = ContentAlignment.MiddleLeft;
        _hintWin11.ForeColor = SystemColors.GrayText;
        _hintWin11.Text = "Changes apply to Explorer immediately. On Windows 11 classic entries appear under \"Show more options\" (Shift + right-click).";
        bar.Controls.Add(_hintWin11);
        bar.Controls.Add(_hintAdmin);
        bar.Controls.Add(_status);
        Controls.Add(bar);
    }

    private void OnColumnClick(int column)
    {
        var ascending = _sortColumn != column || _list.Sorting != SortOrder.Ascending;
        _sortColumn = column;
        for (var i = 0; i < _list.Columns.Count; i++)
            _list.Columns[i].Text = ColumnNames[i];
        _list.Columns[column].Text = ColumnNames[column] + (ascending ? "  ▲" : "  ▼");
        _list.ListViewItemSorter = new ListViewComparer(column, ascending);
    }

    private void LoadItems()
    {
        List<ShellMenuItem> items;
        try
        {
            items = ShellMenuRegistry.LoadAll();
        }
        catch (Exception ex)
        {
            ShowError("Failed to read the context menu entries:\r\n" + ex.Message);
            return;
        }

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var item in items)
        {
            var row = new ListViewItem(DisplayTitle(item))
            {
                Tag = item
            };
            row.SubItems.Add(item.Source == RegistrySource.LocalMachine ? "All users" : "This user");
            row.SubItems.Add(string.Join(", ", item.Locations.Select(l => l.ToString())));
            row.SubItems.Add(item.Command);
            row.SubItems.Add(item.Arguments);
            row.SubItems.Add(item.Icon ?? "");
            row.SubItems.Add(item.Extended ? "Yes" : "No");
            _list.Items.Add(row);
        }
        _list.EndUpdate();

        _status.Text = _list.Items.Count == 1 ? "1 item" : $"{_list.Items.Count} items";
        UpdateButtons();
    }

    private static string DisplayTitle(ShellMenuItem item)
    {
        var title = string.IsNullOrWhiteSpace(item.Title) ? item.Id : item.Title;
        return title.Replace("&&", "\0").Replace("&", "").Replace("\0", "&");
    }

    private void AddNew()
    {
        using var form = new EditItemForm(new ShellMenuItem());
        if (form.ShowDialog(this) != DialogResult.OK) return;
        if (!SaveItem(form.Item)) return;
        LoadItems();
    }

    private void EditSelected()
    {
        if (_list.SelectedItems.Count == 0) return;
        if (_list.SelectedItems[0].Tag is not ShellMenuItem item) return;

        using var form = new EditItemForm(item);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        if (!SaveItem(item)) return;
        LoadItems();
    }

    private void DeleteSelected()
    {
        if (_list.SelectedItems.Count == 0) return;
        if (_list.SelectedItems[0].Tag is not ShellMenuItem item) return;

        var result = MessageBox.Show(this,
            $"Delete \"{item.Title}\" from the context menu?",
            "MenuForge",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        try
        {
            ShellMenuRegistry.Delete(item);
        }
        catch (UnauthorizedAccessException)
        {
            HandleAccessDenied();
            return;
        }
        catch (System.Security.SecurityException)
        {
            HandleAccessDenied();
            return;
        }
        catch (Exception ex)
        {
            ShowError("Failed to delete the menu item:\r\n" + ex.Message);
            return;
        }
        LoadItems();
    }

    private bool SaveItem(ShellMenuItem item)
    {
        try
        {
            ShellMenuRegistry.Save(item);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            HandleAccessDenied();
            return false;
        }
        catch (System.Security.SecurityException)
        {
            HandleAccessDenied();
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Failed to save the menu item:\r\n" + ex.Message,
                "MenuForge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    /// <summary>
    /// All-users (HKLM) entries need elevation; offer to relaunch as administrator.
    /// </summary>
    private void HandleAccessDenied()
    {
        var result = MessageBox.Show(this,
            "This entry is registered for all users (machine level). Changing it requires administrator rights.\r\n\r\nRestart MenuForge as administrator now?",
            "MenuForge",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;
        try
        {
            Process.Start(new ProcessStartInfo(Application.ExecutablePath) { Verb = "runas" });
            Application.Exit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // UAC prompt was cancelled by the user.
        }
    }

    private void UpdateButtons()
    {
        var hasSelection = _list.SelectedItems.Count > 0;
        _btnEdit.Enabled = hasSelection;
        _btnDelete.Enabled = hasSelection;
    }

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "MenuForge", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private sealed class ListViewComparer : IComparer
    {
        private readonly int _column;
        private readonly bool _ascending;

        public ListViewComparer(int column, bool ascending)
        {
            _column = column;
            _ascending = ascending;
        }

        public int Compare(object? x, object? y)
        {
            var a = (ListViewItem)x!;
            var b = (ListViewItem)y!;
            var result = string.Compare(CellText(a), CellText(b), StringComparison.OrdinalIgnoreCase);
            return _ascending ? result : -result;
        }

        private string CellText(ListViewItem item)
        {
            if (_column <= 0) return item.Text;
            return _column < item.SubItems.Count ? item.SubItems[_column].Text : "";
        }
    }
}
