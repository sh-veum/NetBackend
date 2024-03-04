namespace NetBackend.Models.Keys;

public class AccessKey
{
    public int Id { get; set; }
    // public string? EncryptedKey { get; set; }
    public string? KeyHash { get; set; }
    public Guid? ApiKeyID { get; set; }
    public IApiKey? ApiKey { get; set; }
}