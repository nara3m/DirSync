using PCNetworkBackup.Core.Models;
using PCNetworkBackup.Core.Services;

namespace PCNetworkBackup.App.Forms;

/// <summary>
/// Main configuration window. Deliberately avoids jargon (Robocopy, UNC,
/// Task Scheduler, exit codes) - that detail lives behind "Advanced / debug
/// info" only. All destructive-mirror safety gates (validation -> existing-
/// data warning -> confirmation -> dry-run preview -> confirmation) live in
/// OnSaveAndEnableAsync, run in that exact order, every time the destination
/// or folder selection changes.
/// </summary>
public class MainForm : Form
{
    private readonly ComboBox _driveCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _driveStatusLabel = new() { AutoSize = true, ForeColor = Color.Gray };
    private readonly CheckedListBox _folderList = new() { CheckOnClick = true };
    private readonly ComboBox _intervalCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _startWithWindowsCheck = new() { Text = "Start automatically with Windows", AutoSize = true };
    private readonly Label _statusLabel = new() { AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold) };
    private readonly Label _lastSyncLabel = new() { AutoSize = true };
    private readonly Label _nextSyncLabel = new() { AutoSize = true };
    private readonly Button _syncNowButton = new() { Text = "Sync Now" };
    private readonly Button _saveEnableButton = new() { Text = "Save && Enable" };
    private readonly Button _disableButton = new() { Text = "Disable Backup" };
    private readonly LinkLabel _advancedLink = new() { Text = "Advanced / debug info" };

    private List<MappedDrive> _drives = new();
    private AppConfig _config = ConfigService.Load();

    public MainForm()
    {
        Text = "PC Network Backup";
        ClientSize = new Size(460, 530);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        LoadDrives();
        LoadConfigIntoUi();
        RefreshStatusDisplay();

        _driveCombo.SelectedIndexChanged += (_, _) => UpdateDriveStatusLabel();
        _syncNowButton.Click += async (_, _) => await OnSyncNowAsync();
        _saveEnableButton.Click += async (_, _) => await OnSaveAndEnableAsync();
        _disableButton.Click += (_, _) => OnDisable();
        _advancedLink.LinkClicked += (_, _) => ShowAdvanced();
    }

    private void BuildLayout()
    {
        int y = 15;

        Controls.Add(new Label { Text = "Network drive", Left = 15, Top = y, AutoSize = true });
        y += 20;
        _driveCombo.Left = 15; _driveCombo.Top = y; _driveCombo.Width = 420;
        Controls.Add(_driveCombo);
        y += 30;
        _driveStatusLabel.Left = 15; _driveStatusLabel.Top = y;
        Controls.Add(_driveStatusLabel);
        y += 35;

        Controls.Add(new Label { Text = "Folders to mirror", Left = 15, Top = y, AutoSize = true });
        y += 20;
        _folderList.Left = 15; _folderList.Top = y; _folderList.Width = 420; _folderList.Height = 140;
        foreach (var name in KnownFolderService.SupportedFolderNames)
            _folderList.Items.Add(name);
        Controls.Add(_folderList);
        y += 150;

        Controls.Add(new Label { Text = "Sync frequency", Left = 15, Top = y, AutoSize = true });
        y += 20;
        _intervalCombo.Left = 15; _intervalCombo.Top = y; _intervalCombo.Width = 200;
        foreach (var minutes in AppConfig.AllowedIntervals)
            _intervalCombo.Items.Add(FormatInterval(minutes));
        Controls.Add(_intervalCombo);
        y += 35;

        _startWithWindowsCheck.Left = 15; _startWithWindowsCheck.Top = y;
        Controls.Add(_startWithWindowsCheck);
        y += 35;

        Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, Left = 15, Top = y, Width = 420, Height = 2, Text = "" });
        y += 15;

        Controls.Add(new Label { Text = "Status:", Left = 15, Top = y, AutoSize = true });
        y += 20;
        _statusLabel.Left = 15; _statusLabel.Top = y;
        Controls.Add(_statusLabel);
        y += 25;
        _lastSyncLabel.Left = 15; _lastSyncLabel.Top = y;
        Controls.Add(_lastSyncLabel);
        y += 20;
        _nextSyncLabel.Left = 15; _nextSyncLabel.Top = y;
        Controls.Add(_nextSyncLabel);
        y += 30;

        _syncNowButton.Left = 15; _syncNowButton.Top = y; _syncNowButton.Width = 130;
        Controls.Add(_syncNowButton);
        _saveEnableButton.Left = 155; _saveEnableButton.Top = y; _saveEnableButton.Width = 130;
        Controls.Add(_saveEnableButton);
        _disableButton.Left = 295; _disableButton.Top = y; _disableButton.Width = 140;
        Controls.Add(_disableButton);
        y += 35;

        _advancedLink.Left = 15; _advancedLink.Top = y;
        Controls.Add(_advancedLink);
    }

    private static string FormatInterval(int minutes) => minutes switch
    {
        60 => "Every 1 hour",
        120 => "Every 2 hours",
        _ => $"Every {minutes} minutes",
    };

    private void LoadDrives()
    {
        _drives = NetworkDriveService.GetMappedDrives();
        _driveCombo.Items.Clear();
        foreach (var d in _drives)
            _driveCombo.Items.Add($"{d.DriveLetter}   {d.UncPath}");

        if (_driveCombo.Items.Count == 0)
            _driveCombo.Items.Add("(no mapped network drives found)");

        _driveCombo.SelectedIndex = 0;
    }

    private void LoadConfigIntoUi()
    {
        var idx = _drives.FindIndex(d => d.DriveLetter.Equals(_config.DestinationDrive, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            _driveCombo.SelectedIndex = idx;

        for (int i = 0; i < _folderList.Items.Count; i++)
            _folderList.SetItemChecked(i, _config.Folders.Contains((string)_folderList.Items[i]!));

        var intervalIdx = Array.IndexOf(AppConfig.AllowedIntervals, _config.IntervalMinutes);
        _intervalCombo.SelectedIndex = intervalIdx >= 0 ? intervalIdx : 1; // default: 10 minutes

        _startWithWindowsCheck.Checked = _config.StartWithWindows;
        UpdateDriveStatusLabel();
    }

    private void UpdateDriveStatusLabel()
    {
        if (_drives.Count == 0 || _driveCombo.SelectedIndex < 0 || _driveCombo.SelectedIndex >= _drives.Count)
        {
            _driveStatusLabel.Text = "No network drive selected";
            _driveStatusLabel.ForeColor = Color.DarkOrange;
            return;
        }

        var drive = _drives[_driveCombo.SelectedIndex];
        var result = SyncEngine.ValidateDestination(drive.DriveLetter);
        _driveStatusLabel.Text = result.IsValid ? "\u2713 Network drive available" : $"\u2715 {result.Message}";
        _driveStatusLabel.ForeColor = result.IsValid ? Color.Green : Color.Red;
    }

    private List<string> GetSelectedFolders()
    {
        var result = new List<string>();
        for (int i = 0; i < _folderList.Items.Count; i++)
            if (_folderList.GetItemChecked(i))
                result.Add((string)_folderList.Items[i]!);
        return result;
    }

    private AppConfig BuildConfigFromUi()
    {
        var drive = _drives.Count > 0 && _driveCombo.SelectedIndex >= 0 && _driveCombo.SelectedIndex < _drives.Count
            ? _drives[_driveCombo.SelectedIndex]
            : null;

        return new AppConfig
        {
            DestinationDrive = drive?.DriveLetter ?? "",
            DestinationUnc = drive?.UncPath ?? "",
            Folders = GetSelectedFolders(),
            IntervalMinutes = AppConfig.AllowedIntervals[_intervalCombo.SelectedIndex],
            StartWithWindows = _startWithWindowsCheck.Checked,
            Enabled = _config.Enabled, // only flipped to true after the full safety flow below
            FirstRunCompleted = _config.FirstRunCompleted,
            LastSyncUtc = _config.LastSyncUtc,
            ExcludeFiles = _config.ExcludeFiles,
        };
    }

    private async Task OnSaveAndEnableAsync()
    {
        var newConfig = BuildConfigFromUi();

        if (string.IsNullOrEmpty(newConfig.DestinationDrive))
        {
            MessageBox.Show(this, "Please select a network drive first.", "PC Network Backup",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (newConfig.Folders.Count == 0)
        {
            MessageBox.Show(this, "Please select at least one folder to mirror.", "PC Network Backup",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Gate 1: destination must actually be a reachable, writable network drive.
        var validation = SyncEngine.ValidateDestination(newConfig.DestinationDrive);
        if (!validation.IsValid)
        {
            MessageBox.Show(this, $"The selected network drive isn't ready:\n\n{validation.Message}",
                "PC Network Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var mappings = SyncEngine.BuildMappings(newConfig);

        // Gate 2: warn if destination folders already contain unrelated data.
        var preExisting = mappings
            .Where(m => Directory.Exists(m.DestinationPath) && Directory.EnumerateFileSystemEntries(m.DestinationPath).Any())
            .ToList();

        if (preExisting.Count > 0)
        {
            var msg = "The following destination folders already contain files:\n\n" +
                      string.Join("\n", preExisting.Select(m => m.DestinationPath)) +
                      "\n\nA mirror operation may remove files that don't exist on your PC.\n" +
                      "Please make sure this is the correct destination.\n\nContinue?";
            if (MessageBox.Show(this, msg, "Existing data found", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
        }

        // Gate 3: explicit confirmation of exactly what will be mirrored, where.
        using (var confirmForm = new ConfirmMirrorForm(newConfig.DestinationDrive, newConfig.DestinationUnc, mappings))
        {
            if (confirmForm.ShowDialog(this) != DialogResult.OK)
                return;
        }

        // Gate 4: safe dry-run analysis (Robocopy /L) so the user sees real counts
        // of what would be copied/updated/deleted before anything destructive runs.
        _statusLabel.Text = "Analyzing changes...";
        Enabled = false;
        List<RobocopyResult> dryRunResults;
        try
        {
            dryRunResults = await Task.Run(() => SyncEngine.RunDryRun(newConfig));
        }
        finally
        {
            Enabled = true;
        }

        using (var dryRunForm = new DryRunSummaryForm(dryRunResults))
        {
            if (dryRunForm.ShowDialog(this) != DialogResult.OK)
            {
                _statusLabel.Text = "Not enabled.";
                return;
            }
        }

        // All gates passed - persist, enable, and register the scheduled task.
        newConfig.Enabled = true;
        newConfig.FirstRunCompleted = true;
        _config = newConfig;
        ConfigService.Save(_config);

        var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
        var (created, output) = TaskSchedulerService.CreateOrUpdateTask(exePath, _config.IntervalMinutes);
        if (!created)
        {
            MessageBox.Show(this,
                $"Configuration was saved, but the scheduled background task could not be created:\n\n{output}",
                "PC Network Backup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        RefreshStatusDisplay();
        MessageBox.Show(this, "Backup is now configured and enabled.", "PC Network Backup",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task OnSyncNowAsync()
    {
        if (string.IsNullOrEmpty(_config.DestinationDrive) || _config.Folders.Count == 0)
        {
            MessageBox.Show(this, "Please configure and enable backup first (Save & Enable).", "PC Network Backup",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _syncNowButton.Enabled = false;
        _statusLabel.Text = "Synchronizing...";
        try
        {
            using var guard = new SingleInstanceGuard();
            if (!guard.Acquired)
            {
                _statusLabel.Text = "A synchronization is already in progress.";
                return;
            }

            var validation = SyncEngine.ValidateDestination(_config.DestinationDrive);
            if (!validation.IsValid)
            {
                _statusLabel.Text = "\u26A0 Network drive unavailable. Will retry automatically.";
                return;
            }

            var result = await Task.Run(() => SyncEngine.RunMirror(_config));
            _config.LastSyncUtc = DateTime.UtcNow;
            ConfigService.Save(_config);

            if (result.AnyFatal)
                _statusLabel.Text = "\u2715 Backup could not complete. See Advanced for details.";
            else if (result.AnyErrors)
                _statusLabel.Text = "\u26A0 Backup finished; some files were skipped (in use). Will retry next cycle.";
            else
                _statusLabel.Text = "\u2713 Backup is up to date";
        }
        finally
        {
            _syncNowButton.Enabled = true;
            RefreshStatusDisplay();
        }
    }

    private void OnDisable()
    {
        var msg = "Automatic mirroring will be disabled.\n\n" +
                  "Existing files on the network drive will NOT be deleted.\n\nContinue?";
        if (MessageBox.Show(this, msg, "Disable Backup", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            return;

        _config.Enabled = false;
        ConfigService.Save(_config);
        TaskSchedulerService.DeleteTask();
        RefreshStatusDisplay();
    }

    private void RefreshStatusDisplay()
    {
        _statusLabel.Text = _config.Enabled ? "\u2713 Backup is configured and running" : "Backup is not enabled";
        _statusLabel.ForeColor = _config.Enabled ? Color.Green : Color.Gray;

        _lastSyncLabel.Text = _config.LastSyncUtc.HasValue
            ? $"Last sync: {_config.LastSyncUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm}"
            : "Last sync: never";

        _nextSyncLabel.Text = _config.Enabled
            ? $"Sync interval: every {_config.IntervalMinutes} minute(s)"
            : "";
    }

    private void ShowAdvanced()
    {
        using var form = new AdvancedInfoForm(_config);
        form.ShowDialog(this);
    }
}
