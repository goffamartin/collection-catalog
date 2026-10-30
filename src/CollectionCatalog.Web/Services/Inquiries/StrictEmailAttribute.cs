using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace CollectionCatalog.Web.Services.Inquiries;

/// <summary>
/// Stricter e-mail validation than <see cref="EmailAddressAttribute"/> (which only checks for a single "@").
/// The address from the form goes into the <c>mailto:</c> reply link – characters like "?" or "&amp;" would allow
/// injecting a hidden copy (bcc) or changing the subject of the reply.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class StrictEmailAttribute : ValidationAttribute
{
    private static readonly char[] _forbidden = ['?', '&', '%', ',', ';', '<', '>', '"', '\'', '(', ')', '[', ']', '\\', '/', '=', '#'];

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320 || email.IndexOfAny(_forbidden) >= 0 || email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        // MailAddress also accepts "Name <a@b>" – the result must be exactly the entered address.
        return MailAddress.TryCreate(email, out var address) && address.Address == email && address.Host.Contains('.');
    }

    public override bool IsValid(object? value) => value is null || (value is string s && IsValid(s.Trim()));
}
