namespace NetBackend.Models.User;

public class AccessKey
{
    public int Id { get; set; }
    // public string? EncryptedKey { get; set; }
    public string? KeyHash { get; set; }
}