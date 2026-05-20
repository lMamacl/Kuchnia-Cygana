(function () {
    var storageKey = "kuc.staff.theme";
    var toggle = document.querySelector("[data-staff-theme-toggle]");

    function applyTheme(theme) {
        if (theme === "dark") {
            document.documentElement.setAttribute("data-bs-theme", "dark");
        } else {
            document.documentElement.removeAttribute("data-bs-theme");
        }

        if (toggle) {
            toggle.setAttribute("aria-pressed", theme === "dark" ? "true" : "false");
            toggle.setAttribute("title", theme === "dark" ? "Tryb jasny" : "Tryb ciemny");
        }
    }

    var savedTheme = localStorage.getItem(storageKey) || "light";
    applyTheme(savedTheme);

    if (toggle) {
        toggle.addEventListener("click", function () {
            var currentTheme = document.documentElement.getAttribute("data-bs-theme") === "dark" ? "dark" : "light";
            var nextTheme = currentTheme === "dark" ? "light" : "dark";
            localStorage.setItem(storageKey, nextTheme);
            applyTheme(nextTheme);
        });
    }
})();
