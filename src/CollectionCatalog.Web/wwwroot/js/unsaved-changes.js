// Warning before leaving with unsaved changes (see Components/Admin/Shared/UnsavedChangesGuard.razor).
//
// The NavigationLock of an interactive page guards only navigation driven by its circuit. Links and forms
// in the static layout of the administration (side menu, logo, sign-out) are handled by the enhanced navigation of
// blazor.web.js – they are intercepted here, in the capture phase, before Blazor takes them.

// Translated question, passed in by UnsavedChangesGuard.
let message = 'You have unsaved changes. Leave without saving?';
const confirmValidityMs = 2000;

let enabled = false;
let confirmedAt = 0;

/**
 * Asks before leaving; one confirmation applies to the following navigation, whoever then handles it
 * (an intercepted link as well as NavigationLock) – the user does not get two questions in a row.
 */
export function confirmLeave() {
    if (!enabled || Date.now() - confirmedAt < confirmValidityMs) {
        return true;
    }
    if (window.confirm(message)) {
        confirmedAt = Date.now();
        return true;
    }
    return false;
}

export function setEnabled(value, question) {
    enabled = value;
    if (question) {
        message = question;
    }
    if (!value) {
        confirmedAt = 0;
    }
}

function block(event) {
    event.preventDefault();
    event.stopImmediatePropagation();
}

document.addEventListener('click', event => {
    if (!enabled || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return;
    }
    const link = event.target.closest('a[href]');
    if (!link || link.target === '_blank' || link.hasAttribute('download') || link.origin !== location.origin) {
        return;
    }
    const url = new URL(link.href);
    if (url.pathname === location.pathname && url.search === location.search) {
        return; // anchor on the same page
    }
    if (!confirmLeave()) {
        block(event);
    }
}, true);

// Only forms with their own address (sign-out…) – the interactive EditForm of the page has no action and its saving must not be blocked.
document.addEventListener('submit', event => {
    if (enabled && event.target.hasAttribute('action') && !confirmLeave()) {
        block(event);
    }
}, true);
