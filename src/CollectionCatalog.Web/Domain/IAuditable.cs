namespace CollectionCatalog.Web.Domain;

/// <summary>
/// Entity with the times of creation and last change (UTC). Filled in by <c>AuditInterceptor</c>.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? ModifiedAt { get; set; }
}
