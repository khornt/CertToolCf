using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Media;

namespace CertTool.Services;

public static class PlatformDialogs
{
    public static Window? Owner { get; set; }
    public static async Task<string?> SelectFolderAsync(string? previous = null)
    {
        var owner = Owner ?? throw new InvalidOperationException("Main window is not initialized.");
        IStorageFolder? start = null;
        if (!string.IsNullOrEmpty(previous))
            start = await owner.StorageProvider.TryGetFolderFromPathAsync(previous);
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select certificate output directory", AllowMultiple = false, SuggestedStartLocation = start
        });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
    public static async Task<string?> OpenFileAsync()
    {
        var owner = Owner ?? throw new InvalidOperationException("Main window is not initialized.");
        var results = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Open public key PEM", AllowMultiple = false });
        return results.Count == 0 ? null : results[0].TryGetLocalPath();
    }
    public static async Task<string?> SaveFileAsync(string suggestedName)
    {
        var owner = Owner ?? throw new InvalidOperationException("Main window is not initialized.");
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        { Title = "Save JWK", SuggestedFileName = suggestedName, DefaultExtension = "jwk" });
        return file?.TryGetLocalPath();
    }
    public static async Task CopyAsync(string text)
    {
        if (Owner?.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
    }
    public static async Task<bool> ConfirmAsync(string text) => await ShowDialogAsync(text, true);
    public static void ShowMessage(string? text) => _ = ShowDialogAsync(text ?? "Unknown error", false);
    private static async Task<bool> ShowDialogAsync(string text, bool confirm)
    {
        var owner = Owner ?? throw new InvalidOperationException("Main window is not initialized.");
        var dialog = new Window { Title = "CertTool", Width = 450, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        var yes = new Button { Content = confirm ? "Yes" : "OK", MinWidth = 80 };
        yes.Click += (_, _) => dialog.Close(true);
        if (confirm)
        {
            var no = new Button { Content = "No", MinWidth = 80 };
            no.Click += (_, _) => dialog.Close(false);
            row.Children.Add(no);
        }
        row.Children.Add(yes);
        dialog.Content = new StackPanel { Margin = new Thickness(20), Spacing = 16, Children =
        { new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, row } };
        return await dialog.ShowDialog<bool>(owner);
    }
    public static async Task<string?> AskAcmePassphraseAsync()
    {
        var owner = Owner ?? throw new InvalidOperationException("Main window is not initialized.");
        var dialog = new Window { Title = "Unlock / Protect ACME Account", Width = 440, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var first = new TextBox { PasswordChar = '●' };
        var second = new TextBox { PasswordChar = '●' };
        var error = new TextBlock { Foreground = Brushes.IndianRed };
        var show = new CheckBox { Content = "Show passphrase" };
        show.Checked += (_, _) => { first.RevealPassword = true; second.RevealPassword = true; };
        show.Unchecked += (_, _) => { first.RevealPassword = false; second.RevealPassword = false; };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
        var cancel = new Button { Content = "Cancel" }; var ok = new Button { Content = "Continue" };
        cancel.Click += (_, _) => dialog.Close((string?)null);
        ok.Click += (_, _) =>
        {
            if ((first.Text ?? "").Length < 12) error.Text = "Use a passphrase of at least 12 characters.";
            else if (first.Text != second.Text) error.Text = "Passphrases do not match.";
            else dialog.Close(first.Text);
        };
        buttons.Children.Add(cancel); buttons.Children.Add(ok);
        dialog.Content = new StackPanel { Spacing = 9, Margin = new Thickness(20), Children =
        { new TextBlock { Text = "ACME account passphrase (separate from PFX password). Use the same passphrase to reuse the account.", TextWrapping = TextWrapping.Wrap },
          new TextBlock { Text = "Passphrase" }, first, new TextBlock { Text = "Confirm passphrase" }, second, show, error, buttons } };
        return await dialog.ShowDialog<string?>(owner);
    }
}
