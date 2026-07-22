using System.ComponentModel.DataAnnotations;

namespace MyWorkHub.Core.Entities;

/// <summary>
/// Encrypted credential storage. <see cref="Key"/> is the EF Core primary key
/// (string PK — no separate generated Id column).
/// </summary>
public class AppCredential
{
    [Key]
    public string Key { get; set; } = "";

    /// <summary>DPAPI-encrypted, base64-encoded value.</summary>
    public string EncryptedValue { get; set; } = "";

    public DateTime UpdatedAt { get; set; }
}
