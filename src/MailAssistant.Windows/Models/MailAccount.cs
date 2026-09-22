namespace MailAssistant.Windows.Models;

/// <summary>
/// Ein einfaches Mail-Konto. Phase 1: reine Daten, keine IMAP-/SMTP-Logik, keine Secrets.
/// </summary>
public sealed record MailAccount
{
    /// <summary>Eindeutige ID (GUID), wird bei der Erzeugung gesetzt.</summary>
    public required Guid Id { get; init; }

    /// <summary>Anzeigename, z. B. "Arbeit" oder "Privat".</summary>
    public required string DisplayName { get; init; }

    /// <summary>E-Mail-Adresse des Kontos, z. B. "vorname@beispiel.de".</summary>
    public required string EmailAddress { get; init; }

    public override string ToString() => $"{DisplayName} <{EmailAddress}>";
}
