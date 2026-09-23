using MailAssistant.Windows.Models;

namespace MailAssistant.Windows.Services;

/// <summary>
/// Einfache In-Memory-Implementierung von <see cref="IAccountRepository"/>.
/// Enthält nur Demo-Daten ohne Secrets; keine Persistenz, kein Netzwerk.
/// </summary>
public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly List<MailAccount> _accounts = new();

    /// <summary>
    /// Erzeugt das Repository und befüllt es mit lokalen Demo-Konten.
    /// E-Mail-Adressen sind Platzhalter auf nicht existierenden Domains.
    /// </summary>
    public InMemoryAccountRepository()
    {
        _accounts.Add(new MailAccount
        {
            Id = Guid.NewGuid(),
            DisplayName = "Demo: Arbeit",
            EmailAddress = "demo.arbeit@beispielkonten.de",
        });

        _accounts.Add(new MailAccount
        {
            Id = Guid.NewGuid(),
            DisplayName = "Demo: Privat",
            EmailAddress = "demo.privat@beispielkonten.net",
        });
    }

    public IReadOnlyCollection<MailAccount> GetAll() => _accounts;

    public void Add(MailAccount account) => _accounts.Add(account);

    public bool Update(MailAccount account)
    {
        // Direkt die bestehende Instanz mutieren, um Identität (GUID + Objektreferenz)
        // zu erhalten – keine neue Instanz erzeugen.
        var existing = _accounts.FirstOrDefault(a => a.Id == account.Id);
        if (existing is null)
        {
            return false;
        }

        existing.DisplayName = account.DisplayName;
        existing.EmailAddress = account.EmailAddress;
        return true;
    }

    public bool Remove(Guid id) => _accounts.RemoveAll(account => account.Id == id) > 0;
}
