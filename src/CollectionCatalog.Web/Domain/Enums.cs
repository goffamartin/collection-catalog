namespace CollectionCatalog.Web.Domain;

// UI labels of the values are in EnumLabels (translated).

/// <summary>
/// State of preservation of an item.
/// </summary>
public enum ConditionState
{
    Excellent,
    Good,
    Average,
    Poor,
    Damaged,
}

/// <summary>
/// Originality of an item.
/// </summary>
public enum Originality
{
    Unknown,
    Original,
    Copy,
    Replica,
}

/// <summary>
/// What the photo shows. The display order is <see cref="ItemImage.SortOrder"/>, not this type.
/// </summary>
public enum ImageType
{
    Front,
    Back,
    Detail,
    Packaging,      // Box, case, original packaging
    Certificate,    // Certificate, label, documentation
    Other,
}

/// <summary>
/// Visibility of an item in the public collection.
/// </summary>
public enum ItemVisibility
{
    Private,    // Administration only
    Public,     // In the public collection
}

public enum ListingStatus
{
    Draft,      // Prepared, not public
    Active,     // Publicly offered
    Reserved,   // Reserved for a buyer
    Sold,
    Withdrawn,  // Withdrawn from the offer
}

public enum InquiryStatus
{
    New,
    Read,
    Replied,
    Accepted,
    Declined,
    Archived,
}
