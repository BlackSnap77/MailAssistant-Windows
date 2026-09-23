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
    /// Entfernt das Konto mit der angegebenen <paramref name="id" />.
    /// </summary>
    /// <param name="id">Eindeutige ID des zu entfernenden Kontos.</param>
    /// <returns><c>true</c>, wenn ein Konto entfernt wurde; andernfalls <c>false</c>.</returns>
    bool Remove(Guid id);
}
