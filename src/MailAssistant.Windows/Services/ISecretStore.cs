namespace MailAssistant.Windows.Services
{
    /// <summary>
    /// Abstraktion für das sichere Speichern, Laden und Löschen von Secrets (z. B. IMAP-Passwörter)
    /// pro E-Mail-Konto, isoliert vom UI- und Modell-Layer.
    /// </summary>
    public interface ISecretStore
    {
        /// <summary>
        /// Speichert das übergebene Secret für das angegebene Konto.
        /// Überschreibt ein bestehendes Secret für dieses Konto.
        /// </summary>
        /// <param name="accountId">Eindeutige Konto-ID.</param>
        /// <param name="secret">Klartext-Secret; wird verschlüsselt abgelegt, nicht in Logs oder Exceptions.</param>
        void Save(Guid accountId, string secret);

        /// <summary>
        /// Lädt und entschlüsselt das für das Konto gespeicherte Secret.
        /// </summary>
        /// <param name="accountId">Eindeutige Konto-ID.</param>
        /// <returns>Das entschlüsselte Secret; <c>null</c>, wenn kein Secret gespeichert ist.</returns>
        string? Load(Guid accountId);

        /// <summary>
        /// Löscht das gespeicherte Secret für das angegebene Konto.
        /// </summary>
        /// <param name="accountId">Eindeutige Konto-ID.</param>
        void Delete(Guid accountId);
    }
}
