using PCNetworkBackup.Core.Services;

namespace PCNetworkBackup.App.Forms;

/// <summary>Explicit "here is exactly what will happen" confirmation before enabling mirroring.</summary>
public class ConfirmMirrorForm : Form
{
    public ConfirmMirrorForm(string driveLetter, string unc, List<FolderMapping> mappings)
    {
        Text = "Confirm mirror settings";
        ClientSize = new Size(480, 420);
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
            Width = 450,
            Height = 320,
            Font = new Font(FontFamily.GenericMonospace, 9),
        };

        text.Text =
            $"Destination:\r\n{driveLetter}  {unc}\r\n\r\n" +
            "The following folders will be mirrored:\r\n\r\n" +
            string.Join("\r\n\r\n", mappings.Select(m => $"{m.SourcePath}\r\n    ->\r\n{m.DestinationPath}")) +
            "\r\n\r\nWARNING\r\n" +
            "Files that no longer exist in the source may be deleted from the\r\n" +
            "network destination. The network destination should be used only\r\n" +
            "for this user's mirrored folders.";
        Controls.Add(text);

        var cancel = new Button { Text = "Cancel", Left = 260, Top = 350, Width = 90, DialogResult = DialogResult.Cancel };
        var confirm = new Button { Text = "Confirm && Enable", Left = 355, Top = 350, Width = 110, DialogResult = DialogResult.OK };
        Controls.Add(cancel);
        Controls.Add(confirm);
        AcceptButton = confirm;
        CancelButton = cancel;
    }
}
