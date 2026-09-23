using System;
using System.Collections.ObjectModel;
using System.Linq;
using MailAssistant.Windows.Models;
using MailAssistant.Windows.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MailAssistant.Windows;

public sealed partial class MainWindow : Window
{
    private readonly IAccountRepository _repository;
    private readonly ObservableCollection<MailAccount> _accounts = new();

    /// <summary>
    /// Reentrancy-Guard: true, solange der Remove-ContentDialog geöffnet ist.
    /// Verhindert ein zweites ContentDialog-Auslösen, das WinUI mit
    /// „Only a single ContentDialog can be open at any time“ ablehnt.
    /// </summary>
    private bool _isRemoveDialogOpen;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1000, 700));

        // Phase 2: lokale Demo-Konten, kein Netzwerk, keine Secrets.
        _repository = new InMemoryAccountRepository();
        RefreshAccounts();
        AccountAddedButton.Click += OnAccountAddedButtonClick;
        AccountRemoveButton.Click += OnAccountRemoveButtonClick;
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
        RefreshAccounts();
        DisplayNameTextBox.Text = string.Empty;
        EmailAddressTextBox.Text = string.Empty;
        SetStatus($"Konto „{account.DisplayName}“ hinzugefügt.", isError: false);
    }

    /// <summary>
    /// Lädt die Kontenliste aus dem Repository neu und setzt die Auswahl zurück.
    /// </summary>
    private void RefreshAccounts()
    {
        _accounts.Clear();
        foreach (var a in _repository.GetAll())
        {
            _accounts.Add(a with { IsSelected = false });
        }

        AccountsList.ItemsSource = _accounts;
        UpdateRemoveButtonState();
    }

    /// <summary>
    /// Wird vom Tapped-Event einer Konto-Card aufgerufen.
    /// Markiert das geklickte Konto als ausgewählt und aktualisiert die Visualisierung.
    /// </summary>
    private void OnAccountCardTapped(object sender, TappedRoutedEventArgs e)
    {
        // sender ist der Border aus dem DataTemplate.
        // Der DataContext des Borders ist das MailAccount-Objekt.
        if (sender is not Border card)
        {
            return;
        }

        if (card.DataContext is not MailAccount account)
        {
            return;
        }

        // Alle anderen Konten zurücksetzen, dieses markieren.
        foreach (var a in _accounts)
        {
            a.IsSelected = a.Id == account.Id;
        }

        // Die Visualisierung aller Karten aktualisieren.
        UpdateAccountCardVisuals();
        UpdateRemoveButtonState();
    }

    /// <summary>
    /// Aktualisiert die BorderBrush und Background aller Konto-Cards
    /// je nach Auswahlstatus.
    /// </summary>
    private void UpdateAccountCardVisuals()
    {
        // Über die ItemsControl auf die generierten Borders zugreifen.
        // Das ItemsControl erzeugt für jedes Item einen Container,
        // dessen Inhalt das DataTemplate ist.
        // Wir nutzen ItemContainerGenerator, um auf die Container zuzugreifen.

        for (var i = 0; i < _accounts.Count; i++)
        {
            if (AccountsList.ContainerFromIndex(i) is not Border card)
            {
                // Container kann noch nicht existieren (virtualisiert).
                continue;
            }

            var account = _accounts[i];
            if (account.IsSelected)
            {
                card.BorderThickness = new Thickness(2);
                card.BorderBrush = TryGetThemeBrush("ControlAccentBrush")
                    ?? new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x78, 0xD7));
                card.Background = TryGetThemeBrush("CardBackgroundFillColorSecondaryBrush")
                    ?? new SolidColorBrush(Color.FromArgb(0xFF, 0x2D, 0x2D, 0x2D));
            }
            else
            {
                card.BorderThickness = new Thickness(1);
                card.BorderBrush = TryGetThemeBrush("CardStrokeColorDefaultBrush")
                    ?? new SolidColorBrush(Color.FromArgb(0xFF, 0x4A, 0x4A, 0x4A));
                card.Background = TryGetThemeBrush("CardBackgroundFillColorDefaultBrush")
                    ?? new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E));
            }
        }
    }

    /// <summary>
    /// Entfernt das aktuell ausgewählte Konto: Der Nutzer wird mit einem klaren
    /// Bestätigungstext (Name und E-Mail des Kontos) gefragt. Abbrechen ändert nichts;
    /// Bestätigen entfernt exakt dieses Konto über das Repository, aktualisiert die Liste,
    /// setzt die Auswahl zurück und zeigt einen kurzen Statushinweis.
    /// </summary>
    private async void OnAccountRemoveButtonClick(object? sender, RoutedEventArgs e)
    {
        // Reentrancy-Guard: Ist bereits ein Remove-Dialog offen, ignorieren wir
        // weitere Klicks. Sonst wirft WinUI eine COMException, weil nur ein
        // ContentDialog gleichzeitig offen sein darf.
        if (_isRemoveDialogOpen)
        {
            return;
        }

        var account = GetSelectedAccount();
        if (account is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Konto entfernen",
            Content = new TextBlock
            {
                Text = $"Soll das Konto „{account.DisplayName}“ ({account.EmailAddress}) wirklich entfernt werden?",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360,
            },
            PrimaryButtonText = "Entfernen",
            CloseButtonText = "Abbrechen",
        };

        // Ohne explizites XamlRoot wirft ContentDialog.ShowAsync() einen
        // ArgumentException ("This element does not have a XamlRoot...").
        // Der XamlRoot-Verweis auf den Window-Button ist derselbe wie das
        // ContentRoot der Window und stabil für die Lebensdauer des Dialogs.
        dialog.XamlRoot = AccountRemoveButton.XamlRoot;

        _isRemoveDialogOpen = true;
        AccountRemoveButton.IsEnabled = false;
        try
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            _repository.Remove(account.Id);
            RefreshAccounts();
            SetStatus($"Konto „{account.DisplayName}“ entfernt.", isError: false);
        }
        finally
        {
            _isRemoveDialogOpen = false;
            UpdateRemoveButtonState();
        }
    }
    /// <summary>
    /// Liefert das aktuell ausgewählte Konto oder <c>null</c>, wenn keines ausgewählt ist.
    /// </summary>
    private MailAccount? GetSelectedAccount()
        => _accounts.FirstOrDefault(a => a.IsSelected);

    /// <summary>
    /// Hält den Zustand des Entfernen-Buttons mit der Auswahl in Einklang:
    /// Der Button ist nur aktiv, wenn genau ein Konto ausgewählt ist.
    /// </summary>
    private void UpdateRemoveButtonState()
    {
        AccountRemoveButton.IsEnabled = GetSelectedAccount() is not null;
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
            StatusText.Foreground = TryGetThemeBrush("CardStrokeColorDefaultBrush")
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
