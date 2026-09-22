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
}
