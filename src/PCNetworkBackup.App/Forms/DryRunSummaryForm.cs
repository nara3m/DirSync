using PCNetworkBackup.Core.Services;

namespace PCNetworkBackup.App.Forms;

/// <summary>Shows the Robocopy /L (list-only, non-destructive) preview before the real mirror runs.</summary>
public class DryRunSummaryForm : Form
{
    public DryRunSummaryForm(List<RobocopyResult> results)
    {
        Text = "Review changes";
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

        int totalCopy = 0, totalDelete = 0;
        var lines = new List<string> { "Here is what would change:\r\n" };

        foreach (var r in results)
        {
            var s = r.Summary;
            int copy = s?.FilesCopied ?? 0;
            int delete = s?.FilesToDelete ?? 0;
            lines.Add($"{r.FolderName}\r\n    Files to copy/update: {copy}\r\n    Files to delete:       {delete}\r\n");
            totalCopy += copy;
            totalDelete += delete;
        }

        lines.Add($"TOTAL\r\n    Copy/update: {totalCopy}\r\n    Delete:      {totalDelete}");
        text.Text = string.Join("\r\n", lines);
        Controls.Add(text);

        var cancel = new Button { Text = "Cancel", Left = 260, Top = 350, Width = 90, DialogResult = DialogResult.Cancel };
        var confirm = new Button { Text = "Confirm", Left = 355, Top = 350, Width = 110, DialogResult = DialogResult.OK };
        Controls.Add(cancel);
        Controls.Add(confirm);
        AcceptButton = confirm;
        CancelButton = cancel;
    }
}
