// Small enhancements of the website. Everything through event delegation on the document – it keeps working after
// Blazor's "enhanced navigation", which swaps the page content without re-running scripts.

// === Dropdowns (<details data-dropdown>): close on a click outside or on Escape ===
document.addEventListener('click', event => {
    document.querySelectorAll('details[data-dropdown][open]').forEach(dropdown => {
        if (!dropdown.contains(event.target)) {
            dropdown.open = false;
        }
    });
});

document.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
        document.querySelectorAll('details[data-dropdown][open]').forEach(dropdown => dropdown.open = false);
    }
});

// === Collection filters: submit after a select changes, leave empty fields out of the URL ===
document.addEventListener('change', event => {
    const form = event.target.closest('form[data-autosubmit]');
    if (form && event.target.matches('select')) {
        form.requestSubmit();
    }
});

document.addEventListener('formdata', event => {
    if (!event.target.matches('form[data-autosubmit]')) {
        return;
    }
    for (const [name, value] of [...event.formData.entries()]) {
        if (value === '') {
            event.formData.delete(name);
        }
    }
}, true);

// === "Skip to content" link ===
// Because of <base href="/"> "#obsah" would lead to the home page (and Blazor would intercept it as navigation) –
// instead just move the focus to the main content of the current page.
document.addEventListener('click', event => {
    const link = event.target.closest('a[data-skip-link]');
    const main = document.getElementById('obsah');
    if (!link || !main) {
        return;
    }
    event.preventDefault();
    event.stopPropagation();
    main.focus();
    main.scrollIntoView();
}, true);

// === Gallery of the item detail ===
function galleryImages(gallery) {
    return [...gallery.querySelectorAll('[data-gallery-thumb]')].map(thumb => ({
        src: thumb.dataset.src,
        large: thumb.dataset.srcLarge,
        caption: thumb.dataset.caption || '',
        alt: thumb.dataset.alt || '',
    }));
}

function currentIndex(gallery) {
    return Number(gallery.dataset.index || 0);
}

function select(gallery, index) {
    const images = galleryImages(gallery);
    if (images.length === 0) {
        return;
    }
    index = (index + images.length) % images.length;
    gallery.dataset.index = index;

    const image = images[index];
    const main = gallery.querySelector('[data-gallery-main]');
    main.srcset = `${image.src} 800w, ${image.large} 1600w`;
    main.src = image.src;
    main.alt = image.alt;
    const blur = gallery.querySelector('[data-gallery-blur]');
    if (blur) {
        blur.src = image.src;
    }
    gallery.querySelector('[data-gallery-caption]').textContent = image.caption;
    gallery.querySelectorAll('[data-gallery-thumb]').forEach((thumb, i) => {
        if (i === index) {
            thumb.setAttribute('aria-current', 'true');
        } else {
            thumb.removeAttribute('aria-current');
        }
    });

    const dialog = gallery.querySelector('[data-gallery-dialog]');
    if (dialog?.open) {
        showLarge(gallery, dialog);
    }
}

function showLarge(gallery, dialog) {
    const images = galleryImages(gallery);
    const index = currentIndex(gallery);
    const image = images[index];
    const large = dialog.querySelector('[data-gallery-large]');
    large.src = image.large;
    large.alt = image.alt;
    dialog.querySelector('[data-gallery-large-caption]').textContent = image.caption;
    dialog.querySelector('[data-gallery-counter]').textContent = images.length > 1 ? `${index + 1} / ${images.length}` : '';
    dialog.querySelectorAll('[data-gallery-prev], [data-gallery-next]').forEach(button => button.hidden = images.length < 2);
}

document.addEventListener('click', event => {
    const gallery = event.target.closest('[data-gallery]');
    if (!gallery) {
        return;
    }
    const dialog = gallery.querySelector('[data-gallery-dialog]');

    const thumb = event.target.closest('[data-gallery-thumb]');
    if (thumb) {
        select(gallery, [...gallery.querySelectorAll('[data-gallery-thumb]')].indexOf(thumb));
    } else if (event.target.closest('[data-gallery-open]') && dialog) {
        showLarge(gallery, dialog);
        dialog.showModal();
    } else if (event.target.closest('[data-gallery-prev]')) {
        select(gallery, currentIndex(gallery) - 1);
    } else if (event.target.closest('[data-gallery-next]')) {
        select(gallery, currentIndex(gallery) + 1);
    } else if (event.target.closest('[data-gallery-close]') || event.target.matches('[data-gallery-backdrop]')) {
        // The cross or a click outside the photo closes it.
        dialog?.close();
    }
});

document.addEventListener('keydown', event => {
    const dialog = document.querySelector('[data-gallery-dialog][open]');
    if (!dialog) {
        return;
    }
    const gallery = dialog.closest('[data-gallery]');
    if (event.key === 'ArrowLeft') {
        select(gallery, currentIndex(gallery) - 1);
    } else if (event.key === 'ArrowRight') {
        select(gallery, currentIndex(gallery) + 1);
    }
});

// Swiping in the full-screen view.
let touchStartX = null;
document.addEventListener('touchstart', event => {
    touchStartX = event.target.closest('[data-gallery-dialog]') ? event.touches[0].clientX : null;
}, { passive: true });
document.addEventListener('touchend', event => {
    if (touchStartX === null) {
        return;
    }
    const deltaX = event.changedTouches[0].clientX - touchStartX;
    touchStartX = null;
    if (Math.abs(deltaX) > 50) {
        const gallery = event.target.closest('[data-gallery]');
        select(gallery, currentIndex(gallery) + (deltaX < 0 ? 1 : -1));
    }
}, { passive: true });
