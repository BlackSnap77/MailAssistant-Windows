using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MailAssistant.Windows.Services;

/// <summary>
/// DPAPI-basierte Secret-Speicherung pro Benutzerkontext (%LOCALAPPDATA%\MailAssistant\secrets).
/// Secrets werden mit <see cref="ProtectedData"/> (Scope: CurrentUser) verschlüsselt
/// und als UTF-8-String gespeichert. Keine Secrets in Logs oder Exception-Nachrichten.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly string StoreDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MailAssistant",
            "secrets");

    /// <inheritdoc />
    public void Save(Guid accountId, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        Directory.CreateDirectory(StoreDirectory);

        string targetPath = GetSecretFilePath(accountId);
        byte[] plainBytes = Encoding.UTF8.GetBytes(secret);
        byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

        // Temp-Datei im selben Verzeichnis -> atomares Replace
        string tempPath = targetPath + ".tmp";
        try
        {
            File.WriteAllBytes(tempPath, protectedBytes);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            // Temp-Datei auerraumen, falls sie noch existiert (best effort, ohne Secret in Logs)
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }

    /// <inheritdoc />
    public string? Load(Guid accountId)
    {
        string targetPath = GetSecretFilePath(accountId);
        if (!File.Exists(targetPath))
            return null;

        byte[] protectedBytes = File.ReadAllBytes(targetPath);
        byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <inheritdoc />
    public void Delete(Guid accountId)
    {
        string targetPath = GetSecretFilePath(accountId);
        if (!File.Exists(targetPath))
            return;

        File.Delete(targetPath);
    }

    private static string GetSecretFilePath(Guid accountId)
        => Path.Combine(StoreDirectory, $"{accountId:N}.secret");
}
