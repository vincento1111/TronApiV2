using System.Security.Claims;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace TronApi.Infrastructure;

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
    public static ApiException BadRequest(string message) => new(400, message);
    public static ApiException NotFound(string message) => new(404, message);
}

public static partial class Input
{
    public static int PlayerId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0
            ? id : throw new ApiException(401, "Please sign in.");

    public static string Text(string? value, string label, int min, int max)
    {
        var result = value?.Trim() ?? "";
        if (result.Length < min || result.Length > max || result.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
            throw ApiException.BadRequest($"{label} must contain {min}–{max} characters.");
        return result;
    }

    public static string DisplayName(string? value)
    {
        var name = Text(value, "Display name", 2, 24);
        if (!NamePattern().IsMatch(name)) throw ApiException.BadRequest("Use letters, numbers, spaces, hyphens or underscores for your display name.");
        return name;
    }

    public static string Email(string? value)
    {
        var email = Text(value, "Email", 3, 254);
        if (!MailAddress.TryCreate(email, out var parsed) || !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase) || !email.Contains('@'))
            throw ApiException.BadRequest("Enter a valid email address.");
        return email;
    }

    public static string Password(string? value)
    {
        if (value is null || value.Length < 10 || value.Length > 128)
            throw ApiException.BadRequest("Use a password with 10–128 characters.");
        return value;
    }

    public static int Quantity(int value, int max = 1000) =>
        value >= 1 && value <= max ? value : throw ApiException.BadRequest($"Quantity must be between 1 and {max}.");

    [GeneratedRegex(@"^[\p{L}\p{N} _-]+$")]
    private static partial Regex NamePattern();
}
