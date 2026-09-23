using System.Text.Json;
using System.Text.Json.Serialization;
using MailAssistant.Windows.Models;

namespace MailAssistant.Windows.Services;

/// <summary>
/// Kontrollierte, nicht verheiratete Ausnahme für Fehler beim Laden oder Speichern
/// der lokalen Konten-JSON-Datei (IO-, Serializer- oder Formatfehler).
/// Die UI fängt diese Ausnahme und meldet sie über den Statusbereich,
/// anstatt die App crashen zu lassen.
/// </summary>
public sealed class AccountPersistenceException : Exception
{
    public AccountPersistenceException(string message)
        : base(message)
    {
    }

    public AccountPersistenceException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Persistentes <see cref="IAccountRepository" />: speichert nur die ungefährlichen
/// Account-Metadaten (<c>Id</c>, <c>DisplayName</c>, <c>EmailAddress</c>) in einer lokalen
/// JSON-Datei unter dem appgeeigneten Benutzerpfad (Standard: %LOCALAPPDATA%\MailAssistant\accounts.json).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MailAccount.IsSelected" /> ist UI-Zustand und wird bewusst NICHT
/// persistiert: Beim Laden wird jedes Konto mit <c>IsSelected = false</c> erzeugt.
/// </para>
/// <para>
/// Beim Schreiben wird eine temporäre Datei im selben Ordner erzeugt und
/// anschließend per <see cref="File.Move(string, string, bool)" /> mit Überschreiben
/// atomar ersetzt, sodass eine bestehende Datei nie nur halb überschrieben wird.
/// </para>
/// <para>
/// Es werden keine Passwörter, OAuth-Daten oder sonstigen Secrets gespeichert.
/// </para>
/// </remarks>
public sealed class JsonAccountRepository : IAccountRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Nur Metadaten (ohne IsSelected, ohne Secrets) werden serialisiert.
    // Die IMAP-Metadaten sind optional (Default-Marker über get-only init),
    // damit alte Phase-5-JSON-Dateien ohne diese Felder weiterhin lesbar bleiben.
    private sealed record AccountEntry(
        Guid Id,
        string DisplayName,
        string EmailAddress,
        [property: JsonPropertyName("ImapHost"), JsonInclude] string ImapHost,
        [property: JsonPropertyName("ImapPort"), JsonInclude] int ImapPort,
        [property: JsonPropertyName("LoginUser"), JsonInclude] string LoginUser)
    {
        public AccountEntry(Guid id, string displayName, string emailAddress)
            : this(id, displayName, emailAddress, "", 0, "")
        {
        }
    }

    private readonly string _filePath;
    private readonly List<MailAccount> _accounts = new();

    /// <summary>
    /// Erzeugt das Repository für den Standardpfad
    /// <c>%LOCALAPPDATA%\MailAssistant\accounts.json</c> und lädt die vorhandene
    /// Datei (oder eine leere Liste, falls keine Datei existiert).
    /// </summary>
    public JsonAccountRepository()
        : this(DefaultFilePath)
    {
    }

    /// <summary>
    /// Erzeugt das Repository für einen beliebigen Pfad und lädt die vorhandene Datei.
    /// </summary>
    public JsonAccountRepository(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    /// <summary>
    /// Standarddatei: <c>%LOCALAPPDATA%\MailAssistant\accounts.json</c>.
    /// </summary>
    public static string DefaultFilePath
    {
        get
        {
            var appData =
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.Create)
                ?? throw new AccountPersistenceException(
                    "Der lokale ApplicationData-Pfad konnte nicht ermittelt werden.");
            return Path.Combine(appData, "MailAssistant", "accounts.json");
        }
    }

    public IReadOnlyCollection<MailAccount> GetAll() => _accounts;

    public void Add(MailAccount account)
    {
        _accounts.Add(account);
        Save();
    }

    public bool Update(MailAccount account)
    {
        // Direkt die bestehende Instanz mutieren, um Identität (GUID + Objektreferenz)
        // zu erhalten – keine neue Instanz erzeugen (wie in der InMemory-Variante).
        var existing = _accounts.FirstOrDefault(a => a.Id == account.Id);
        if (existing is null)
        {
            return false;
        }

        existing.DisplayName = account.DisplayName;
        existing.EmailAddress = account.EmailAddress;
        existing.ImapHost = account.ImapHost;
        existing.ImapPort = account.ImapPort;
        existing.LoginUser = account.LoginUser;
        Save();
        return true;
    }

    public bool Remove(Guid id)
    {
        var removed = _accounts.RemoveAll(account => account.Id == id) > 0;
        if (removed)
        {
            Save();
        }

        return removed;
    }

    private void Load()
    {
        // Datei existiert nicht → leere Liste, kein Fehler (First-Run).
        if (!File.Exists(_filePath))
        {
            return;
        }

        string raw;
        try
        {
            raw = File.ReadAllText(_filePath);
        }
        catch (Exception ex)
        {
            // Nicht lesbar (z. B. Zugriff verweigert) → kontrolliert melden,
            // Datei auf jeden Fall erhalten (nicht überschreiben/leeren).
            throw new AccountPersistenceException(
                "Lokale Kontendatei konnte nicht gelesen werden: " + ex.Message, ex);
        }

        var accounts = Deserialize(raw);
        if (accounts is null)
        {
            // Datei existiert, ist aber kaputt/ungültig: NICHT überschreiben
            // (keine stillschweigende Datenzerstörung), stattdessen melden.
            throw new AccountPersistenceException(
                "Lokale Kontendatei ist beschädigt und konnte nicht gelesen werden. " +
                "Die Datei wurde nicht verändert.");
        }

        // IsSelected ist UI-Zustand → beim Laden immer neu (false).
        _accounts.Clear();
        _accounts.AddRange(accounts);
    }

    private List<MailAccount> Deserialize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Leere Datei gilt als „noch keine Konten“ (z. B. nach erstem,
            // ordnungsgemäßem Start ohne Konto).
            return new List<MailAccount>();
        }

        List<AccountEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<AccountEntry>>(raw, SerializerOptions);
        }
        catch (JsonException ex)
        {
            // Kaputtes JSON: Die Datei bewusst NICHT anrühren (kein Überschreiben/
            // Löschen/„Reparieren“), und die Situation kontrolliert über eine
            // AccountPersistenceException (mit der JsonException als InnerException)
            // melden – die App crasht nicht.
            throw new AccountPersistenceException(
                "Lokale Kontendatei enthält ungültiges JSON und konnte nicht gelesen werden. " +
                "Die Datei wurde nicht verändert.", ex);
        }

        if (entries is null)
        {
            throw new AccountPersistenceException(
                "Lokale Kontendatei enthält keine gültige Kontoliste und konnte nicht gelesen werden. " +
                "Die Datei wurde nicht verändert.");
        }

        // Ungültige Einträge (z. B. keine gültige E-Mail) werden verworfen,
        // damit sie die übrigen Konten nicht blockieren.
        var result = new List<MailAccount>();
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.DisplayName)
                || string.IsNullOrWhiteSpace(entry.EmailAddress)
                || !entry.EmailAddress.Contains('@'))
            {
                continue;
            }

            // Backward-Kompatibilität: Fehlt ImapHost/LoginUser → "";
            // fehlt ImapPort oder ist 0 → 993 (IMAPS-Standardport).
            var imapHost = entry.ImapHost ?? "";
            var loginUser = entry.LoginUser ?? "";
            var imapPort = (entry.ImapPort <= 0) ? 993 : entry.ImapPort;

            result.Add(new MailAccount
            {
                Id = entry.Id,
                DisplayName = entry.DisplayName,
                EmailAddress = entry.EmailAddress,
                ImapHost = imapHost,
                ImapPort = imapPort,
                LoginUser = loginUser,
            });
        }
        return result;
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                throw new AccountPersistenceException(
                    "Speicherordner für Konten konnte nicht angelegt werden: " + ex.Message, ex);
            }
        }

        var json = JsonSerializer.Serialize(_accounts, SerializerOptions);

        // Atomares Schreiben: erst in eine Temp-Datei im selben Ordner,
        // dann per Move mit Überschreiben → bestehende Datei wird nie
        // nur halb überschrieben.
        var tempPath = _filePath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Temp-Datei räumen, falls sie angelegt wurde.
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Aufräumen ist „best effort“; den eigentlichen Fehler nicht überdecken.
            }

            throw new AccountPersistenceException(
                "Konten konnten nicht gespeichert werden: " + ex.Message, ex);
        }
    }
}
