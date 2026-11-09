using MenuForge.Models;
using MenuForge.Services;

namespace MenuForge.UI;

/// <summary>
/// Dialog for adding or editing a context-menu entry, including command,
/// arguments and the menu locations it should appear at.
/// </summary>
public sealed class EditItemForm : Form
{
    private readonly ShellMenuItem _item;

    private readonly TextBox _txtTitle = new();
    private readonly TextBox _txtIcon = new();
    private readonly TextBox _txtCommand = new();
    private readonly TextBox _txtArguments = new();
    private readonly TextBox _txtExtensions = new();
    private readonly ComboBox _cmbPlaceholder = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _chkVisible = new()
    {
        Text = "Visible in the context menu",
        AutoSize = true,
        Checked = true
    };
    private readonly CheckBox _chkExtended = new()
    {
        Text = "Show only while Shift is held",
        AutoSize = true
    };
    private readonly CheckBox _chkAllFsObjects = new() { Text = "All files and folders", AutoSize = true };
    private readonly CheckBox _chkAllFiles = new() { Text = "All files", AutoSize = true };
    private readonly CheckBox _chkFolders = new() { Text = "Folders", AutoSize = true };
    private readonly CheckBox _chkFolderBackground = new()
    {
        Text = "Folder background (empty space inside a folder)",
        AutoSize = true
    };
    private readonly CheckBox _chkDrives = new() { Text = "Drives", AutoSize = true };
    private readonly CheckBox _chkFileExtension = new() { Text = "Specific file type(s):", AutoSize = true };

    /// <summary>The item being created or edited.</summary>
    public ShellMenuItem Item => _item;

    public EditItemForm(ShellMenuItem item)
    {
        _item = item;

        Text = item.Id.Length == 0 ? "Add menu item" : "Edit menu item";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;

        BuildForm();
        LoadFromItem();
    }

    private void BuildForm()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 3,
            RowCount = 9
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));

        // Menu title
        AddLabel(table, "Menu &title:", 0);
        table.Controls.Add(Fill(_txtTitle), 1, 0);
        table.SetColumnSpan(_txtTitle, 2);

        // Icon
        AddLabel(table, "&Icon (optional):", 1);
        table.Controls.Add(Fill(_txtIcon), 1, 1);
        table.Controls.Add(BrowseButton(BrowseIcon), 2, 1);

        // Command
        AddLabel(table, "&Command:", 2);
        table.Controls.Add(Fill(_txtCommand), 1, 2);
        table.Controls.Add(BrowseButton(BrowseCommand), 2, 2);

        // Arguments
        AddLabel(table, "&Arguments:", 3);
        table.Controls.Add(Fill(_txtArguments), 1, 3);
        table.SetColumnSpan(_txtArguments, 2);

        // Placeholder insert row
        AddLabel(table, "", 4);
        var placeholders = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        placeholders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        placeholders.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _cmbPlaceholder.Items.AddRange(new object[]
        {
            "%1 - Selected file or folder",
            "%V - Selected folder (current folder for background menu)",
            "%L - Long path version of %1",
            "%W - Working directory"
        });
        _cmbPlaceholder.SelectedIndex = 0;
        _cmbPlaceholder.Dock = DockStyle.Fill;
        var insert = new Button { Text = "&Insert", AutoSize = true, Margin = new Padding(4, 2, 0, 0) };
        insert.Click += (s, e) => InsertPlaceholder();
        placeholders.Controls.Add(_cmbPlaceholder, 0, 0);
        placeholders.Controls.Add(insert, 1, 0);
        table.Controls.Add(placeholders, 1, 4);
        table.SetColumnSpan(placeholders, 2);

        // Example
        var example = new Label
        {
            Text = "Example: Command = C:\\Windows\\System32\\cmd.exe   Arguments = /k cd /d \"%V\"",
            ForeColor = SystemColors.GrayText,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 2, 0, 0)
        };
        table.Controls.Add(example, 1, 5);
        table.SetColumnSpan(example, 2);

        // Visible + Extended (Shift-only)
        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        _chkVisible.Margin = new Padding(0, 0, 16, 0);
        _chkExtended.Margin = new Padding(0);
        options.Controls.Add(_chkVisible);
        options.Controls.Add(_chkExtended);
        table.Controls.Add(options, 1, 6);
        table.SetColumnSpan(options, 2);

        // Scopes
        var scopes = BuildScopesGroup();
        scopes.Dock = DockStyle.Fill;
        table.Controls.Add(scopes, 0, 7);
        table.SetColumnSpan(scopes, 3);

        // Buttons
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Margin = new Padding(0, 10, 0, 0)
        };
        var ok = new Button { Text = "OK", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (s, e) =>
        {
            if (!TryApply()) return;
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 1, 8);
        table.SetColumnSpan(buttons, 2);
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AcceptButton = ok;
        CancelButton = cancel;

        ClientSize = new Size(640, 648);
        Controls.Add(table);
    }

    private GroupBox BuildScopesGroup()
    {
        var group = new GroupBox { Text = "Show on", Padding = new Padding(8, 2, 8, 8) };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(10, 6, 4, 4)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        grid.Controls.Add(_chkAllFsObjects, 0, 0);
        grid.SetColumnSpan(_chkAllFsObjects, 2);
        grid.Controls.Add(_chkAllFiles, 0, 1);
        grid.SetColumnSpan(_chkAllFiles, 2);
        grid.Controls.Add(_chkFolders, 0, 2);
        grid.SetColumnSpan(_chkFolders, 2);
        grid.Controls.Add(_chkFolderBackground, 0, 3);
        grid.SetColumnSpan(_chkFolderBackground, 2);
        grid.Controls.Add(_chkDrives, 0, 4);
        grid.SetColumnSpan(_chkDrives, 2);

        grid.Controls.Add(_chkFileExtension, 0, 5);
        _txtExtensions.Dock = DockStyle.Fill;
        _txtExtensions.Enabled = false;
        grid.Controls.Add(_txtExtensions, 1, 5);
        _chkFileExtension.CheckedChanged += (s, e) => _txtExtensions.Enabled = _chkFileExtension.Checked;

        var extHint = new Label
        {
            Text = "Separate several extensions with spaces, e.g.  .txt .md",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Margin = new Padding(4, 2, 0, 0)
        };
        grid.Controls.Add(extHint, 1, 6);

        group.Controls.Add(grid);
        return group;
    }

    private static void AddLabel(TableLayoutPanel table, string text, int row)
    {
        var label = new Label
        {
            Text = text,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleLeft
        };
        table.Controls.Add(label, 0, row);
    }

    private static Control Fill(Control control)
    {
        control.Dock = DockStyle.Fill;
        return control;
    }

    private static Control BrowseButton(Action onClick)
    {
        var button = new Button { Text = "&Browse...", AutoSize = true, Anchor = AnchorStyles.Left };
        button.Click += (s, e) => onClick();
        return button;
    }

    private void BrowseIcon()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose an icon source",
            Filter = "Programs and libraries (*.exe;*.dll;*.ico)|*.exe;*.dll;*.ico|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _txtIcon.Text = dialog.FileName;
    }

    private void BrowseCommand()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a command",
            Filter = "Programs (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _txtCommand.Text = dialog.FileName;
    }

    private void InsertPlaceholder()
    {
        if (_cmbPlaceholder.SelectedItem is not string selected) return;
        var token = selected.Split(" - ")[0];
        var position = _txtArguments.SelectionStart;
        _txtArguments.Text = _txtArguments.Text.Insert(position, token);
        _txtArguments.SelectionStart = position + token.Length;
        _txtArguments.Focus();
    }

    private void LoadFromItem()
    {
        _txtTitle.Text = _item.Title;
        _txtIcon.Text = _item.Icon ?? "";
        _txtCommand.Text = _item.Command;
        _txtArguments.Text = _item.Arguments;
        _chkExtended.Checked = _item.Extended;
        _chkVisible.Checked = !_item.Hidden;

        var extensions = new List<string>();
        foreach (var location in _item.Locations)
        {
            switch (location.Scope)
            {
                case MenuScope.AllFilesystemObjects:
                    _chkAllFsObjects.Checked = true;
                    break;
                case MenuScope.AllFiles:
                    _chkAllFiles.Checked = true;
                    break;
                case MenuScope.Folders:
                    _chkFolders.Checked = true;
                    break;
                case MenuScope.FolderBackground:
                    _chkFolderBackground.Checked = true;
                    break;
                case MenuScope.Drives:
                    _chkDrives.Checked = true;
                    break;
                case MenuScope.FileExtension:
                    _chkFileExtension.Checked = true;
                    if (!extensions.Contains(location.Extension ?? ""))
                        extensions.Add(location.Extension ?? "");
                    break;
            }
        }
        _txtExtensions.Text = string.Join(" ", extensions);
    }

    private bool TryApply()
    {
        var title = _txtTitle.Text.Trim();
        var command = _txtCommand.Text.Trim().Trim('"').Trim();

        if (title.Length == 0)
            return Fail("Enter a menu title.", _txtTitle);
        if (command.Length == 0)
            return Fail("Choose the command to run.", _txtCommand);

        var locations = new List<MenuLocation>();
        if (_chkAllFsObjects.Checked)
            locations.Add(new MenuLocation(MenuScope.AllFilesystemObjects, null));
        if (_chkAllFiles.Checked)
            locations.Add(new MenuLocation(MenuScope.AllFiles, null));
        if (_chkFolders.Checked)
            locations.Add(new MenuLocation(MenuScope.Folders, null));
        if (_chkFolderBackground.Checked)
            locations.Add(new MenuLocation(MenuScope.FolderBackground, null));
        if (_chkDrives.Checked)
            locations.Add(new MenuLocation(MenuScope.Drives, null));
        if (_chkFileExtension.Checked)
        {
            var extensions = _txtExtensions.Text
                .Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ShellMenuRegistry.NormalizeExtension)
                .Where(e => e.Length > 0)
                .Distinct()
                .ToList();

            if (extensions.Count == 0)
                return Fail("Enter at least one file extension, e.g. .txt", _txtExtensions);

            locations.AddRange(extensions.Select(e => new MenuLocation(MenuScope.FileExtension, e)));
        }

        if (locations.Count == 0)
            return Fail("Select at least one location the menu item should appear at.", _chkAllFiles);

        _item.Title = title;
        _item.Command = command;
        _item.Arguments = _txtArguments.Text.Trim();
        _item.Icon = string.IsNullOrWhiteSpace(_txtIcon.Text) ? null : _txtIcon.Text.Trim();
        _item.Extended = _chkExtended.Checked;
        _item.Hidden = !_chkVisible.Checked;
        _item.Locations = locations;
        return true;
    }

    private bool Fail(string message, Control control)
    {
        MessageBox.Show(this, message, "MenuForge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        control.Focus();
        return false;
    }
}
