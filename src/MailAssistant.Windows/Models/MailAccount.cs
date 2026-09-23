using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MailAssistant.Windows.Models;

/// <summary>
/// Ein einfaches Mail-Konto. Phase 1: reine Daten, keine IMAP-/SMTP-Logik, keine Secrets.
/// </summary>
public sealed record MailAccount : INotifyPropertyChanged
{
    /// <summary>
    /// Gibt an, ob das Konto aktuell in der UI ausgewählt ist.
    /// UI-nur (kein Netzwerk/Persistenz); wird durch die Auswahl in der Liste gesetzt.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => $"{DisplayName} <{EmailAddress}>";

    /// <summary>Eindeutige ID (GUID), wird bei der Erzeugung gesetzt.</summary>
    public required Guid Id { get; init; }

    /// <summary>Anzeigename, z. B. "Arbeit" oder "Privat".</summary>
    public required string DisplayName { get; init; }

    /// <summary>E-Mail-Adresse des Kontos, z. B. "vorname@beispiel.de".</summary>
    public required string EmailAddress { get; init; }
}
