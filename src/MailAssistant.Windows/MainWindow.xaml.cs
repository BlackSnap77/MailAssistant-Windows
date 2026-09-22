using Microsoft.UI.Xaml;

namespace MailAssistant.Windows;

using MailAssistant.Windows.Services;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1000, 700));

        // Phase 1: lokale Demo-Konten, kein Netzwerk, keine Secrets.
        IAccountRepository repository = new InMemoryAccountRepository();
        AccountsList.ItemsSource = repository.GetAll();
    }
}
