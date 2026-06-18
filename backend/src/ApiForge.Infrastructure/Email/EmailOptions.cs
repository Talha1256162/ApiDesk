namespace ApiForge.Infrastructure.Email;

public sealed class EmailOptions
{
    public string PublicBaseUrl { get; init; } = "https://apidesk.runasp.net";
    public SmtpEmailOptions Smtp { get; init; } = new();
}

public sealed class SmtpEmailOptions
{
    public bool Enabled { get; init; }
    public int EmailTypeId { get; init; } = 1;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 465;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromName { get; init; } = "Apeiron";
    public string FromEmail { get; init; } = string.Empty;
    public int EncryptionId { get; init; } = 2;
    public bool IsVerified { get; init; }

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && Port > 0
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(FromEmail);
}
