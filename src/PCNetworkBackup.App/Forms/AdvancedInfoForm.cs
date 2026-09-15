using PCNetworkBackup.Core.Models;
using PCNetworkBackup.Core.Services;

namespace PCNetworkBackup.App.Forms;

/// <summary>Technical details deliberately kept off the main screen (config paths, log location, task name, etc).</summary>
public class AdvancedInfoForm : Form
{
    public AdvancedInfoForm(AppConfig config)
    {
        Text = "Advanced / debug info";
        ClientSize = new Size(500, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Left = 15,
            Top = 15,
            Width = 470,
            Height = 310,
            Font = new Font(FontFamily.GenericMonospace, 9),
        };

        text.Text =
            $"Config file:      {ConfigService.ConfigPath}\r\n" +
            $"Log directory:    {LogService.LogDirectory}\r\n" +
            $"Scheduled task:   {TaskSchedulerService.TaskName}\r\n" +
            $"Task registered:  {TaskSchedulerService.TaskExists()}\r\n\r\n" +
            $"Destination drive: {config.DestinationDrive}\r\n" +
            $"Destination UNC:   {config.DestinationUnc}\r\n" +
            $"Folders:           {string.Join(", ", config.Folders)}\r\n" +
            $"Interval:          {config.IntervalMinutes} minutes\r\n" +
            $"Enabled:           {config.Enabled}\r\n" +
            $"Last sync (UTC):   {config.LastSyncUtc}\r\n" +
            $"Exclude patterns:  {string.Join(", ", config.ExcludeFiles)}\r\n";
        Controls.Add(text);

        var close = new Button { Text = "Close", Left = 400, Top = 335, Width = 85, DialogResult = DialogResult.OK };
        Controls.Add(close);
        AcceptButton = close;
    }
}
