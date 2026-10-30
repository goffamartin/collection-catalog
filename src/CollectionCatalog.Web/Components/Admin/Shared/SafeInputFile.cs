using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace CollectionCatalog.Web.Components.Admin.Shared;

/// <summary>
/// <see cref="InputFile"/> whose JS initialization does not fail when the element has meanwhile left the DOM.
/// </summary>
/// <remarks>
/// InputFile is initialized through JS after rendering. When the user leaves the page quickly (navigation),
/// the element no longer exists, JS throws and the exception would end the whole Blazor circuit – the page stops responding.
/// </remarks>
public sealed class SafeInputFile : InputFile
{
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await base.OnAfterRenderAsync(firstRender);
        }
        catch (JSException)
        {
            // The element is no longer in the DOM (the page has meanwhile changed) – nothing to initialize.
        }
        catch (JSDisconnectedException)
        {
            // The circuit disconnected (closed browser / navigation out of the application).
        }
    }
}
