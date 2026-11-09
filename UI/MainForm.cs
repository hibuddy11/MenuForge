using MenuForge.Models;
using MenuForge.Services;

namespace MenuForge.UI;

public sealed class MainForm : Form
{
    private readonly ListView _list = new();
    private readonly Button _btnAdd = new() { Text = "&Add...", AutoSize = true };
    private readonly Button _btnEdit = new() { Text = "&Edit...", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "&Delete", AutoSize = true };
    private readonly Button _btnRefresh = new() { Text = "&Refresh", AutoSize = true };
    private readonly Label _status = new();

    public MainForm()
    {
        Text = "MenuForge";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 520);
        Size = new Size(1000, 640);
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
        _list.Columns.Add("Menu title", 220);
        _list.Columns.Add("Shows on", 180);
        _list.Columns.Add("Command", 230);
        _list.Columns.Add("Arguments", 170);
        _list.Columns.Add("Icon", 160);
        _list.Columns.Add("Shift only", 70, HorizontalAlignment.Center);

        _list.DoubleClick += (s, e) => EditSelected();
        _list.SelectedIndexChanged += (s, e) => UpdateButtons();

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
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(8, 4, 8, 6) };
        _status.Dock = DockStyle.Top;
        _status.Height = 18;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        var hint = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Changes apply to Explorer immediately. On Windows 11 classic entries appear under \"Show more options\" (Shift + right-click).",
            ForeColor = SystemColors.GrayText,
            TextAlign = ContentAlignment.MiddleLeft
        };
        bar.Controls.Add(hint);
        bar.Controls.Add(_status);
        Controls.Add(bar);
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
            var row = new ListViewItem(string.IsNullOrWhiteSpace(item.Title) ? item.Id : item.Title)
            {
                Tag = item
            };
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
        catch (Exception ex)
        {
            ShowError("Failed to delete the menu item:\r\n" + ex.Message);
            return;
        }
        LoadItems();
    }

    private static bool SaveItem(ShellMenuItem item)
    {
        try
        {
            ShellMenuRegistry.Save(item);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(null,
                "Failed to save the menu item:\r\n" + ex.Message,
                "MenuForge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
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
}
