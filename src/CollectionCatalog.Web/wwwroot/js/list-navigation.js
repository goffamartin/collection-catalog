// Return from a detail to the list by a step back in the browser history (the list keeps its filters in the URL, so it restores itself).
// Blazor does not store its own state in the history when moving to another page, so the "came from the list" mark
// is written into history.state of the detail entry by us. It survives a reload; a new tab does not have it (a step back would leave the app).

const pendingKey = 'catalog:open-from-list';

/** List: notes that it is opening the given detail (called right before the navigation). */
export function markOpening(path) {
    sessionStorage.setItem(pendingKey, path);
}

/** Detail: moves the note from the list into its own history entry and returns whether it was opened from the list. */
export function cameFromList() {
    const pending = sessionStorage.getItem(pendingKey);
    if (pending !== null) {
        sessionStorage.removeItem(pendingKey);
        if (pending === location.pathname) {
            mark();
        }
    }
    return history.state?.openedFromList === true;
}

/** Writes the mark again – after the history entry was replaced (e.g. "novy" → Id after saving). */
export function mark() {
    history.replaceState({ ...(history.state ?? {}), openedFromList: true }, '');
}

export function back() {
    history.back();
}
