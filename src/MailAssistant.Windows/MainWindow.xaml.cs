using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MailAssistant.Windows.Models;
using MailAssistant.Windows.Services;
using Windows.UI;

namespace MailAssistant.Windows;

public sealed partial class MainWindow : Window
{
    private readonly IAccountRepository _repository;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1000, 700));

        // Phase 2: lokale Demo-Konten, kein Netzwerk, keine Secrets.
        _repository = new InMemoryAccountRepository();
        AccountsList.ItemsSource = _repository.GetAll();
        AccountAddedButton.Click += OnAccountAddedButtonClick;
    }

    /// <summary>
    /// Fügt ein neues Konto hinzu: validiert die Eingaben, speichert über das
    /// Repository, aktualisiert die Liste und zeigt einen Hinweis.
    /// </summary>
    private void OnAccountAddedButtonClick(object? sender, RoutedEventArgs e)
    {
        var displayName = DisplayNameTextBox.Text.Trim();
        var emailAddress = EmailAddressTextBox.Text.Trim();

        // Validierung: Anzeigename und E-Mail nicht leer, E-Mail enthält "@".
        if (string.IsNullOrWhiteSpace(displayName))
        {
            SetStatus("Bitte einen Anzeigenamen für das Konto eingeben.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            SetStatus("Bitte eine E-Mail-Adresse für das Konto eingeben.", isError: true);
            return;
        }

        if (!emailAddress.Contains('@'))
        {
            SetStatus("E-Mail-Adresse ist ungültig: Bitte eine Adresse mit „@“ eingeben.", isError: true);
            return;
        }

        // Erfolg: neues Konto mit frischer GUID anlegen und speichern.
        var account = new MailAccount
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            EmailAddress = emailAddress,
        };

        _repository.Add(account);

        // Liste sofort aktualisieren (Demo-Konten bleiben erhalten) und Eingabe leeren.
        AccountsList.ItemsSource = _repository.GetAll();
        DisplayNameTextBox.Text = string.Empty;
        EmailAddressTextBox.Text = string.Empty;
        SetStatus($"Konto „{account.DisplayName}“ hinzugefügt.", isError: false);
    }

    /// <summary>
    /// Zeigt einen Statushinweis in der Eingabemaske an.
    /// Fehlerhinweise verwenden die thematische Fehlerfarbe (ControlErrorForegroundBrush),
    /// Erfolgshinweise die normale, zurückhaltende Sekundärfarbe des Themes.
    /// Ein moderater Farb-Fallback sichert die Anzeige, falls die Theme-Resource
    /// im aktuellen Kontext nicht aufgelöst werden kann.
    /// </summary>
    private void SetStatus(string text, bool isError)
    {
        StatusText.Text = text;
        StatusText.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (isError)
        {
            StatusText.Foreground = TryGetThemeBrush("ControlErrorForegroundBrush")
                ?? new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0x2F, 0x21));
        }
        else
        {
            StatusText.Foreground = TryGetThemeBrush("TextFillColorSecondaryBrush")
                ?? new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A));
        }
    }

    /// <summary>
    /// Liest eine Brush-Theme-Resource aus den aktuellen Application-Resources.
    /// Liefert <c>null</c>, wenn die Resource nicht vorhanden ist oder kein
    /// <see cref="SolidColorBrush"/> ist.
    /// </summary>
    private static SolidColorBrush? TryGetThemeBrush(string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
        {
            return brush;
        }
        return null;
    }
}
