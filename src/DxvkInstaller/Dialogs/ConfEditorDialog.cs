using DxvkInstaller.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DxvkInstaller.Dialogs;

public sealed class ConfEditorDialog : ContentDialog
{
    private readonly TextBox _textBox;

    public string ConfText => _textBox.Text;

    public ConfEditorDialog(DxvkEdition edition, string initialText)
    {
        var label = edition == DxvkEdition.Upstream ? "DXVK (upstream)" : "DXVK-GPLAsync";
        Title = $"dxvk.conf ({label})";
        PrimaryButtonText = "Save";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        _textBox = new TextBox
        {
            Text = initialText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            MinWidth = 520,
            Height = 260
        };

        var hint = new TextBlock
        {
            Text = "Saved per edition. Empty text means no dxvk.conf is created when this edition is installed.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var panel = new StackPanel();
        panel.Children.Add(_textBox);
        panel.Children.Add(hint);
        Content = panel;
    }
}
