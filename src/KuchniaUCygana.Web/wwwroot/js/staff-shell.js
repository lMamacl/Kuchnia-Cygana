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

(function () {
    var indexElement = document.getElementById("staff-search-index");
    var searchRoots = Array.prototype.slice.call(document.querySelectorAll("[data-staff-search]"));

    if (!indexElement || searchRoots.length === 0) {
        return;
    }

    var items = [];
    try {
        items = JSON.parse(indexElement.textContent || "[]");
    } catch (_error) {
        items = [];
    }

    function normalize(value) {
        return (value || "").toString().toLowerCase();
    }

    function getMatches(query) {
        var value = normalize(query).trim();
        if (!value) {
            return [];
        }

        return items
            .filter(function (item) {
                return normalize(item.title).indexOf(value) !== -1 ||
                    normalize(item.section).indexOf(value) !== -1 ||
                    normalize(item.description).indexOf(value) !== -1 ||
                    normalize(item.keywords).indexOf(value) !== -1;
            })
            .slice(0, 8);
    }

    function hideResults(root) {
        var results = root.querySelector("[data-staff-search-results]");
        if (results) {
            results.classList.remove("show");
            results.innerHTML = "";
        }
    }

    function renderResults(root, matches, query) {
        var results = root.querySelector("[data-staff-search-results]");
        if (!results) {
            return;
        }

        results.innerHTML = "";
        if (!query.trim()) {
            results.classList.remove("show");
            return;
        }

        if (matches.length === 0) {
            var empty = document.createElement("div");
            empty.className = "dropdown-item-text text-secondary staff-search-empty";
            empty.textContent = "Brak widokow dla: " + query;
            results.appendChild(empty);
            results.classList.add("show");
            return;
        }

        matches.forEach(function (item) {
            var link = document.createElement("a");
            link.className = "dropdown-item staff-search-result";
            link.href = item.href;

            var title = document.createElement("span");
            title.className = "staff-search-result-title";
            title.textContent = item.title;

            var meta = document.createElement("span");
            meta.className = "staff-search-result-meta";
            meta.textContent = item.section + " - " + item.description;

            link.appendChild(title);
            link.appendChild(meta);
            results.appendChild(link);
        });

        results.classList.add("show");
    }

    searchRoots.forEach(function (root) {
        var input = root.querySelector("[data-staff-search-input]");
        if (!input) {
            return;
        }

        var currentMatches = [];

        input.addEventListener("input", function () {
            currentMatches = getMatches(input.value);
            renderResults(root, currentMatches, input.value);
        });

        input.addEventListener("focus", function () {
            currentMatches = getMatches(input.value);
            renderResults(root, currentMatches, input.value);
        });

        input.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                input.value = "";
                currentMatches = [];
                hideResults(root);
                input.blur();
                return;
            }

            if (event.key === "Enter") {
                event.preventDefault();
                if (currentMatches.length > 0) {
                    window.location.href = currentMatches[0].href;
                }
            }
        });
    });

    document.addEventListener("click", function (event) {
        searchRoots.forEach(function (root) {
            if (!root.contains(event.target)) {
                hideResults(root);
            }
        });
    });
})();
