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
}
