using CollectionCatalog.Web.Resources;

namespace CollectionCatalog.Web.Domain;

/// <summary>
/// Labels of enums for the UI, translated by the UI resources (<see cref="AppText"/>).
/// </summary>
public static class EnumLabels
{
    public static string Label(this ConditionState value) => AppText.Get(value switch
    {
        ConditionState.Excellent => "Excellent",
        ConditionState.Good => "Good",
        ConditionState.Average => "Average",
        ConditionState.Poor => "Poor",
        ConditionState.Damaged => "Damaged",
        _ => value.ToString(),
    });

    public static string Label(this Originality value) => AppText.Get(value switch
    {
        Originality.Unknown => "Not determined",
        Originality.Original => "Original",
        Originality.Copy => "Copy",
        Originality.Replica => "Replica",
        _ => value.ToString(),
    });

    public static string Label(this ImageType value) => AppText.Get(value switch
    {
        ImageType.Front => "Front",
        ImageType.Back => "Back",
        ImageType.Detail => "Detail",
        ImageType.Packaging => "Packaging",
        ImageType.Certificate => "Certificate",
        ImageType.Other => "Other",
        _ => value.ToString(),
    });

    public static string Label(this ItemVisibility value) => AppText.Get(value switch
    {
        ItemVisibility.Private => "Private",
        ItemVisibility.Public => "Public",
        _ => value.ToString(),
    });

    /// <summary>Statuses share words with other labels but differ in Czech grammar – keys carry the context „listing“.</summary>
    public static string Label(this ListingStatus value) => AppText.In("listing", value switch
    {
        ListingStatus.Draft => "Draft",
        ListingStatus.Active => "Active",
        ListingStatus.Reserved => "Reserved",
        ListingStatus.Sold => "Sold",
        ListingStatus.Withdrawn => "Withdrawn",
        _ => value.ToString(),
    });

    /// <summary>Statuses share words with other labels but differ in Czech grammar – keys carry the context „inquiry“.</summary>
    public static string Label(this InquiryStatus value) => AppText.In("inquiry", value switch
    {
        InquiryStatus.New => "New",
        InquiryStatus.Read => "Read",
        InquiryStatus.Replied => "Replied",
        InquiryStatus.Accepted => "Accepted",
        InquiryStatus.Declined => "Declined",
        InquiryStatus.Archived => "Archived",
        _ => value.ToString(),
    });
}
