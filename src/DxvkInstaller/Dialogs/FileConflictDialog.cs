using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DxvkInstaller.Dialogs;

public sealed class FileConflictDialog : ContentDialog
{
    public FileConflictDialog(IReadOnlyList<string> conflicts)
    {
        Title = "Existing DXVK files found";
        PrimaryButtonText = "Overwrite";
        SecondaryButtonText = "Uninstall";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        var fileList = string.Join("\n", conflicts.Select(file => "  " + file));
        var body = new TextBlock
        {
            Text = "The game folder already contains:\n\n"
                + fileList
                + "\n\nOverwrite: replace these files with the selected edition and finish the install."
                + "\nUninstall: delete the listed files from the folder and stop (nothing is installed)."
                + "\nCancel: close this dialog and do nothing.",
            TextWrapping = TextWrapping.Wrap
        };

        Content = body;
    }
}
