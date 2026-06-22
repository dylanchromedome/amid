using System.Diagnostics;
using Forms = System.Windows.Forms;

namespace AMID.Setup;

internal sealed class ChromeExtensionInstructionsForm : Forms.Form
{
    private readonly string _extensionDirectory;
    private readonly Forms.Label _statusLabel;

    public ChromeExtensionInstructionsForm(string extensionDirectory)
    {
        _extensionDirectory = extensionDirectory;

        Text = "AMID Chrome Extension";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        FormBorderStyle = Forms.FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new System.Drawing.Size(560, 340);
        Font = new System.Drawing.Font("Segoe UI", 9F);

        var root = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Fill,
            Padding = new Forms.Padding(16),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));
        root.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));

        var titleLabel = new Forms.Label
        {
            AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
            Text = "Load the AMID Chrome extension"
        };

        var instructionsLabel = new Forms.Label
        {
            AutoSize = true,
            Margin = new Forms.Padding(0, 10, 0, 10),
            Text = "1. Start AMID.\r\n" +
                   "2. Open Chrome and go to chrome://extensions.\r\n" +
                   "3. Enable Developer mode.\r\n" +
                   "4. Click Load unpacked.\r\n" +
                   "5. Select the folder below."
        };

        var pathBox = new Forms.TextBox
        {
            Dock = Forms.DockStyle.Top,
            ReadOnly = true,
            Text = _extensionDirectory
        };

        var buttonPanel = new Forms.FlowLayoutPanel
        {
            AutoSize = true,
            Dock = Forms.DockStyle.Top,
            FlowDirection = Forms.FlowDirection.LeftToRight,
            Margin = new Forms.Padding(0, 10, 0, 0)
        };

        var copyButton = new Forms.Button
        {
            AutoSize = true,
            Text = "Copy path"
        };
        copyButton.Click += (_, _) => CopyPath();

        var openFolderButton = new Forms.Button
        {
            AutoSize = true,
            Text = "Open folder"
        };
        openFolderButton.Click += (_, _) => OpenFolder();

        var openChromeButton = new Forms.Button
        {
            AutoSize = true,
            Text = "Copy chrome:// URL"
        };
        openChromeButton.Click += (_, _) => CopyChromeExtensionsAddress();

        buttonPanel.Controls.Add(copyButton);
        buttonPanel.Controls.Add(openFolderButton);
        buttonPanel.Controls.Add(openChromeButton);

        _statusLabel = new Forms.Label
        {
            AutoSize = true,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Margin = new Forms.Padding(0, 10, 0, 0),
            Text = "Chrome will download normally whenever AMID is closed or unreachable."
        };

        var closeButton = new Forms.Button
        {
            Anchor = Forms.AnchorStyles.Right,
            DialogResult = Forms.DialogResult.OK,
            Text = "Close"
        };

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(instructionsLabel, 0, 1);
        root.Controls.Add(pathBox, 0, 2);
        root.Controls.Add(buttonPanel, 0, 3);
        root.Controls.Add(_statusLabel, 0, 4);
        root.Controls.Add(closeButton, 0, 5);

        AcceptButton = closeButton;
        CancelButton = closeButton;
        Controls.Add(root);
    }

    private void CopyPath()
    {
        try
        {
            Forms.Clipboard.SetText(_extensionDirectory);
            _statusLabel.Text = "Copied the Chrome extension folder path.";
        }
        catch (Exception ex)
        {
            Forms.MessageBox.Show(
                this,
                $"Could not copy the extension path:\r\n\r\n{ex.Message}",
                "AMID Chrome Extension",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Warning);
        }
    }

    private void OpenFolder()
    {
        if (!Directory.Exists(_extensionDirectory))
        {
            Forms.MessageBox.Show(
                this,
                $"The extension folder was not found:\r\n\r\n{_extensionDirectory}",
                "AMID Chrome Extension",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _extensionDirectory,
            UseShellExecute = true
        });
        _statusLabel.Text = "Opened the Chrome extension folder.";
    }

    private void CopyChromeExtensionsAddress()
    {
        try
        {
            Forms.Clipboard.SetText("chrome://extensions");
            _statusLabel.Text = "Copied chrome://extensions. Paste it into Chrome's address bar.";
        }
        catch (Exception ex)
        {
            Forms.MessageBox.Show(
                this,
                "Could not copy chrome://extensions.\r\n\r\n" +
                "Open Chrome manually and go to chrome://extensions.\r\n\r\n" +
                ex.Message,
                "AMID Chrome Extension",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Warning);
        }
    }
}
