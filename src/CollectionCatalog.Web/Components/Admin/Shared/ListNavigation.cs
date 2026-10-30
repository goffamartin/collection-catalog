using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CollectionCatalog.Web.Components.Admin.Shared;

/// <summary>
/// Returning from the detail to the item list by a step back in the browser history (see wwwroot/js/list-navigation.js).
/// </summary>
/// <remarks>
/// The list keeps its filters in the URL, so a step back restores it. When the detail was not opened from the list (new tab,
/// saved link), a step back would leave the application – then the list is opened normally.
/// </remarks>
public sealed class ListNavigation(IJSRuntime js, NavigationManager navigation) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <summary>List: opens the detail so that a step back returns to the list.</summary>
    public async Task OpenFromListAsync(string url)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("markOpening", new Uri(navigation.ToAbsoluteUri(url).ToString()).AbsolutePath);
        navigation.NavigateTo(url);
    }

    /// <summary>Detail: finds out (and remembers in the history) whether it was opened from the list.</summary>
    public async Task<bool> CameFromListAsync() => await (await GetModuleAsync()).InvokeAsync<bool>("cameFromList");

    /// <summary>Detail: restores the mark after the history entry was replaced.</summary>
    public async Task MarkAsync() => await (await GetModuleAsync()).InvokeVoidAsync("mark");

    /// <summary>Detail: back to the list.</summary>
    public async Task BackAsync(bool cameFromList)
    {
        if (cameFromList)
        {
            await (await GetModuleAsync()).InvokeVoidAsync("back");
        }
        else
        {
            navigation.NavigateTo(AdminUrls.Items);
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/list-navigation.js");

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already disconnected.
            }
        }
    }
}
