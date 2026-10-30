namespace CollectionCatalog.Web.Services.Text;

public static class PhoneNumber
{
    /// <summary>A <c>tel:</c> link – digits and „+“ only; spaces and dashes of a hand-typed number are dropped.</summary>
    public static string TelUri(string phone) => "tel:" + new string(phone.Where(c => char.IsAsciiDigit(c) || c == '+').ToArray());
}
