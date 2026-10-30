namespace CollectionCatalog.Web.Components.Admin.Shared;

/// <summary>
/// Runs an administration action (save, delete…) so that an error (locked database, full disk) does not tear down the
/// whole Blazor circuit – the user would lose unsaved changes and see only the generic "An error has occurred" bar.
/// Logs the error and returns <c>false</c>; the component shows <see cref="Message"/> (a UI text key: <c>T[SafeAction.Message]</c>).
/// </summary>
public static class SafeAction
{
    public const string Message = "The action failed. Please try again; if the problem persists, reload the page.";

    public static async Task<bool> RunAsync(ILogger logger, string operation, Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Administration action failed: {Operation}", operation);
            return false;
        }
    }
}
