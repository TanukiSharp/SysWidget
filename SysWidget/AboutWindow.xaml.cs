using System.Windows;
using SysWidget.Services;

namespace SysWidget;

/// <summary>
/// Shows which build is running. "Copy" puts the full identity (version, commit, runtime, OS) on
/// the clipboard — the line worth pasting alongside a crash report.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        string text = $"{AppVersion.Full}  ·  .NET {Environment.Version}  ·  Windows {Environment.OSVersion.Version}";

        try
        {
            // Qualified: WinForms is referenced for the tray icon, so `Clipboard` is ambiguous.
            System.Windows.Clipboard.SetText(text);
            CopyButton.Content = "Copied";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // The clipboard is a shared, lockable resource — another app can be holding it.
            CopyButton.Content = "Failed";
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
