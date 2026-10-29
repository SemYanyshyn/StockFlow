namespace StockFlow.Domain;

public class Supplier
{
    public const int MaxNameLength = 100;
    public const int MaxPhoneLength = 30;
    public const int MaxEmailLength = 100;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public bool Validate() =>
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Trim().Length <= MaxNameLength &&
        (string.IsNullOrWhiteSpace(Phone) || Phone.Trim().Length <= MaxPhoneLength) &&
        (string.IsNullOrWhiteSpace(Email) || IsValidEmail(Email.Trim()));

    public bool MatchesSearch(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var text = query.Trim();
        return Contains(Name, text) || Contains(Phone, text) || Contains(Email, text);
    }

    private static bool Contains(string? value, string text) =>
        value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase);

    private static bool IsValidEmail(string email)
    {
        var at = email.IndexOf('@');
        return email.Length <= MaxEmailLength &&
            at > 0 &&
            at == email.LastIndexOf('@') &&
            at < email.Length - 1 &&
            !email.Contains(' ');
    }
}
