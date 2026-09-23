using MailAssistant.Windows.Models;

namespace MailAssistant.Windows.Services;

/// <summary>
/// Zugriffspunkt auf die Liste der konfigurierten Mail-Konten.
/// Phase 1: nur Lesezugriff + Add, keine Persistenz, kein Netzwerk.
/// </summary>
public interface IAccountRepository
{
    /// <summary>Liefert alle aktuell bekannten Konten (immutable Kopie).</summary>
    IReadOnlyCollection<MailAccount> GetAll();

    /// <summary>Fügt ein neues Konto hinzu.</summary>
    void Add(MailAccount account);

    /// <summary>
    /// Aktualisiert das Konto mit der angegebenen <paramref name="id" /> mit den neuen
    /// <see cref="MailAccount.DisplayName" /> und <see cref="MailAccount.EmailAddress" />-Werten.
    /// Die ID und die Objektidentität bleiben unverändert.
    /// </summary>
    /// <param name="account">Konto mit gültigen, bereits validierten Werten.</param>
    /// <returns><c>true</c>, wenn ein Konto aktualisiert wurde; andernfalls <c>false</c>.</returns>
    bool Update(MailAccount account);

    /// <summary>
    /// Entfernt das Konto mit der angegebenen <paramref name="id" />.
    /// </summary>
    /// <param name="id">Eindeutige ID des zu entfernenden Kontos.</param>
    /// <returns><c>true</c>, wenn ein Konto entfernt wurde; andernfalls <c>false</c>.</returns>
    bool Remove(Guid id);
}
