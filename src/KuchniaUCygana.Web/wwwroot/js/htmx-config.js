/**
 * htmx-config.js — Globalna konfiguracja HTMX dla Kuchnia u Cygana
 * Ładowany raz w _Layout.cshtml, po htmx.min.js
 */

// ── CSRF Token ──────────────────────────────────────────────────
// Automatycznie dołącza RequestVerificationToken do każdego żądania HTMX (POST/PUT/DELETE).
// Token czytany z <meta name="csrf-token"> w _Layout.cshtml.
document.body.addEventListener('htmx:configRequest', function (e) {
    var csrfMeta = document.querySelector('meta[name="csrf-token"]');
    if (csrfMeta) {
        e.detail.headers['RequestVerificationToken'] = csrfMeta.content;
    }
});

// ── Globalna obsługa błędów ─────────────────────────────────────
// Wyświetla alert Bootstrap przy błędach serwera (4xx/5xx).
document.body.addEventListener('htmx:responseError', function (e) {
    var status = e.detail.xhr.status;
    var msg = status === 403 ? 'Brak uprawnień do tej akcji.'
            : status === 404 ? 'Nie znaleziono zasobu.'
            : status === 422 ? 'Błąd walidacji — sprawdź formularz.'
            : 'Wystąpił błąd serwera (' + status + '). Spróbuj ponownie.';

    // Wstaw alert do kontenera #htmx-alerts (jeśli istnieje w layout)
    var alertContainer = document.getElementById('htmx-alerts');
    if (alertContainer) {
        alertContainer.innerHTML =
            '<div class="alert alert-danger alert-dismissible fade show" role="alert">' +
                msg +
                '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>' +
            '</div>';
    } else {
        console.error('[HTMX Error]', status, e.detail.xhr.responseText);
    }
});

// ── Rewalidacja jQuery Validation po HTMX swap ──────────────────
// Po podmiance DOM, jQuery Validation musi ponownie zainicjalizować walidację
// na nowych formularzach (jeśli partial zawiera <form> z data-val-*).
document.body.addEventListener('htmx:afterSwap', function (e) {
    if (typeof $ !== 'undefined' && $.validator && $.validator.unobtrusive) {
        var forms = e.detail.target.querySelectorAll('form');
        forms.forEach(function (form) {
            $.validator.unobtrusive.parse(form);
        });
    }
});
