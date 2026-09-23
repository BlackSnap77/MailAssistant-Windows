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
    private readonly IAccountRepository? _repository;
    private readonly ObservableCollection<MailAccount> _accounts = new();

    /// <summary>
    /// Reentrancy-Guard: true, solange der Remove-ContentDialog geöffnet ist.
    /// Verhindert ein zweites ContentDialog-Auslösen, das WinUI mit
    /// „Only a single ContentDialog can be open at any time“ ablehnt.
    /// </summary>
    private bool _isRemoveDialogOpen;

    /// <summary>
    /// Reentrancy-Guard: true, solange der Edit-ContentDialog geöffnet ist.
    /// Verhindert ein zweites ContentDialog-Auslösen, das WinUI mit
    /// „Only a single ContentDialog can be open at any time“ ablehnt.
    /// </summary>
    private bool _isEditDialogOpen;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1000, 700));

        // Phase 5: lokale, persistierte Konten. Konten überleben einen App-Neustart,
        // indem sie in einer JSON-Datei unter %LOCALAPPDATA%\MailAssistant gespeichert
        // werden (keine Registry, keine Datenbank, keine Secrets, kein Netzwerk).
        //
        // Der Konstruktor lädt die Datei: existiert sie noch nicht, wird mit einer
        // leeren Liste gestartet (First-Run, keine Demo-Konten). Ist die Datei
        // beschädigt/unlesbar, wird die Situation kontrolliert über den Statusbereich
        // gemeldet – die App crasht nicht und die bestehende Datei bleibt erhalten.
        try
        {
            _repository = new JsonAccountRepository();
        }
        catch (AccountPersistenceException ex)
        {
            // Repository bleibt null → die UI arbeitet mit einer leeren Liste weiter.
            _repository = null;
            SetStatus("Lokale Konten konnten nicht geladen werden: " + ex.Message, isError: true);
        }

        RefreshAccounts();
        AccountAddedButton.Click += OnAccountAddedButtonClick;
        AccountEditButton.Click += OnAccountEditButtonClick;
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

        // Phase 5: Persistenz kann fehlschlagen (IO/JSON). Dann wird kontrolliert
        // gemeldet – die App crasht nicht, und es erfolgt keine halbgeschriebene Datei.
        try
        {
            _repository?.Add(account);
        }
        catch (AccountPersistenceException ex)
        {
            SetStatus("Konto konnte nicht gespeichert werden: " + ex.Message, isError: true);
            return;
        }

        // Liste sofort aktualisieren und Eingabe leeren.
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
        // _repository kann null sein, wenn beim Laden ein Persistenzfehler auftrat
        // (siehe Konstruktor): Die UI arbeitet dann mit einer leeren Liste weiter.
        foreach (var a in _repository?.GetAll() ?? Array.Empty<MailAccount>())
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

        // ItemsControl ohne Virtualisierung/Recycling: Container sind 1:1
        // über Items und Index erreichbar. (Kein Layout-Umbau.)
        for (var i = 0; i < _accounts.Count; i++)
        {
            // Container zunächst als allgemeiner DependencyObject holen.
            // Beim plain ItemsControl liefert ContainerFromIndex den
            // ContentPresenter (Container), nicht direkt den DataTemplate-Border.
            // Ein "is not Border" Early-Exit würde hier den Border daher
            // für immer überspringen – deshalb nicht pattern-matchen.
            var container = AccountsList.ContainerFromIndex(i);
            if (container is null)
            {
                // Container kann noch nicht existieren (virtualisiert).
                continue;
            }

            // Border auflösen: direkt, wenn der Container selbst der
            // Border ist (ItemsControl), sonst per Visual-Traversal in den
            // Container-Inhalt (ContentPresenter) zum DataTemplate-Border.
            var border = container as Border
                ?? FindVisualChild<Border>((FrameworkElement)container);
            if (border is null)
            {
                // Container/Template-Inhalt noch nicht gerendert.
                continue;
            }

            var account = _accounts[i];

            if (account.IsSelected)
            {
                // Sicherer fester Accent-Status: Theme-Brushes können den
                // Selected-Zustand zu schwach oder identisch mit Neutral
                // darstellen, daher hier bewusst feste Farben.
                border.BorderThickness = new Thickness(2);
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x78, 0xD7));
                border.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1B, 0x2A, 0x3A));
            }
            else
            {
                border.BorderThickness = new Thickness(1);
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x4A, 0x4A, 0x4A));
                border.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E));
            }
        }
    }

    /// <summary>
    /// Sucht rekursiv im Visual-Tree unter <paramref name="root"/> nach dem
    /// ersten Kind vom Typ <typeparamref name="T"/> (hier: dem benannten
    /// Border "AccountCard" innerhalb des ItemsControl-Containers).
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T typedRoot)
        {
            return typedRoot;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// Öffnet einen ContentDialog, um das ausgewählte Konto (Name, E-Mail) zu bearbeiten.
    /// Abbrechen ändert nichts; Speichern aktualisiert exakt das ausgewählte Konto im Repository.
    /// </summary>
    private async void OnAccountEditButtonClick(object? sender, RoutedEventArgs e)
    {
        // Reentrancy-Guard: kein zweiter ContentDialog gleichzeitig.
        if (_isEditDialogOpen)
        {
            return;
        }

        var account = GetSelectedAccount();
        if (account is null)
        {
            return;
        }

        // Dialog-Layout: Title, zwei TextBoxen (vorausgefüllt), Buttons.
        var displayNameTextBox = new TextBox
        {
            Text = account.DisplayName,
            Header = "Anzeigename",
            Margin = new Thickness(0, 0, 0, 8),
            Width = 320,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var emailAddressTextBox = new TextBox
        {
            Text = account.EmailAddress,
            Header = "E-Mail-Adresse",
            Width = 320,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var contentPanel = new StackPanel
        {
            Spacing = 0,
            Width = 320,
        };
        contentPanel.Children.Add(displayNameTextBox);
        contentPanel.Children.Add(emailAddressTextBox);

        var dialog = new ContentDialog
        {
            Title = "Konto bearbeiten",
            Content = contentPanel,
            PrimaryButtonText = "Speichern",
            CloseButtonText = "Abbrechen",
        };

        // XamlRoot explizit setzen – ohne das wirft ShowAsync ein ArgumentException.
        dialog.XamlRoot = AccountEditButton.XamlRoot;

        _isEditDialogOpen = true;
        AccountEditButton.IsEnabled = false;
        try
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                // Abbrechen: keinerlei Datenänderung, Auswahl bleibt bestehen.
                return;
            }

            var newDisplayName = displayNameTextBox.Text.Trim();
            var newEmailAddress = emailAddressTextBox.Text.Trim();

            // Gleiche Validierung wie bei „Konto hinzufügen“.
            if (string.IsNullOrWhiteSpace(newDisplayName))
            {
                SetStatus("Bitte einen Anzeigenamen für das Konto eingeben.", isError: true);
                return;
            }
            if (string.IsNullOrWhiteSpace(newEmailAddress))
            {
                SetStatus("Bitte eine E-Mail-Adresse für das Konto eingeben.", isError: true);
                return;
            }
            if (!newEmailAddress.Contains('@'))
            {
                SetStatus("E-Mail-Adresse ist ungültig: Bitte eine Adresse mit „@“ eingeben.", isError: true);
                return;
            }

            // Exakt das ausgewählte Konto aktualisieren – ID bleibt unverändert.
            // Auswahl (IsSelected) vor dem Refresh merken, damit sie danach erhalten bleibt.
            var selectedId = account.Id;
            var updated = account with { DisplayName = newDisplayName, EmailAddress = newEmailAddress };
            try
            {
                _repository?.Update(updated);
            }
            catch (AccountPersistenceException ex)
            {
                SetStatus("Konto konnte nicht gespeichert werden: " + ex.Message, isError: true);
                return;
            }

            RefreshAccounts();

            // Auswahl wiederherstellen: genau das bearbeitete Konto bleibt markiert.
            foreach (var a in _accounts)
            {
                a.IsSelected = a.Id == selectedId;
            }
            UpdateAccountCardVisuals();
            UpdateRemoveButtonState();
            SetStatus($"Konto „{newDisplayName}“ gespeichert.", isError: false);
        }
        finally
        {
            _isEditDialogOpen = false;
            UpdateRemoveButtonState();
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

            try
            {
                _repository?.Remove(account.Id);
            }
            catch (AccountPersistenceException ex)
            {
                SetStatus("Konto konnte nicht entfernt werden: " + ex.Message, isError: true);
                return;
            }

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
    /// Hält den Zustand beider Aktions-Buttons (Bearbeiten, Entfernen) mit der Auswahl in Einklang:
    /// Ein Button ist nur aktiv, wenn genau ein Konto ausgewählt ist.
    /// </summary>
    private void UpdateRemoveButtonState()
    {
        var hasSelection = GetSelectedAccount() is not null;
        AccountEditButton.IsEnabled = hasSelection;
        AccountRemoveButton.IsEnabled = hasSelection;
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
            // Fehlermeldung: bewusst ein fester, heller Rotton (#F28B82) – dezent und
            // ohne grellen Alarm, aber mit klarem Kontrast im Dark Theme.
            // Bewusst kein Theme-Resource: die bisherigen Aufrufe lieferten null bzw.
            // zu dunkle Brushes und ließen die Meldung praktisch unsichtbar.
            StatusText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF2, 0x8B, 0x82));
        }
        else
        {
            // Normale Statusmeldungen bleiben neutral (sekundärer Text).
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
