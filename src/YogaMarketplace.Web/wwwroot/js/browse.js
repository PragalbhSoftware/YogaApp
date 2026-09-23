(function () {
    var form = document.querySelector("[data-browse-filters]");
    var results = document.querySelector("[data-browse-results]");
    var status = document.querySelector("[data-browse-status]");
    if (!form || !results || !window.fetch || !window.URLSearchParams || !window.FormData) {
        return;
    }

    var requestId = 0;

    function query(includeHandler) {
        var params = new URLSearchParams(new FormData(form));
        if (!params.get("mode")) {
            params.delete("mode");
        }
        if (includeHandler) {
            params.set("handler", "List");
        }
        return params.toString();
    }

    function selectedText(select) {
        return select.options[select.selectedIndex] ? select.options[select.selectedIndex].text : "";
    }

    function checkedModeLabel() {
        var picked = form.querySelector('input[name="mode"]:checked');
        if (!picked || !picked.value) {
            return "";
        }
        var label = picked.closest ? picked.closest("label") : null;
        var text = label ? label.querySelector("span") : null;
        return text && text.textContent ? text.textContent.trim() : picked.value;
    }

    function paint() {
        var areaSelect = form.querySelector('[name="area"]');
        var heading = document.getElementById("browse-heading");
        if (!areaSelect || !heading) {
            return;
        }
        var area = selectedText(areaSelect);
        var mode = checkedModeLabel();
        var headingPattern = heading.getAttribute("data-heading") || "";
        var modeHeading = heading.getAttribute("data-title-mode") || "";
        heading.textContent = mode && modeHeading
            ? modeHeading.replace("{0}", mode).replace("{1}", area)
            : headingPattern.replace("{0}", area);
        var titlePattern = mode ? heading.getAttribute("data-title-mode") : heading.getAttribute("data-title");
        var title = (titlePattern || "").replace("{0}", mode || area).replace("{1}", area);
        var app = heading.getAttribute("data-app") || "";
        if (title && app) {
            document.title = title + " \u00B7 " + app;
        }
        var descriptionPattern = mode ? heading.getAttribute("data-description-mode") : heading.getAttribute("data-description");
        var description = (descriptionPattern || "").replace("{0}", mode || area).replace("{1}", area);
        var meta = document.querySelector('meta[name="description"]');
        if (meta && description) {
            meta.setAttribute("content", description);
        }
        var ogTitle = document.querySelector('meta[property="og:title"]');
        if (ogTitle && title && app) {
            ogTitle.setAttribute("content", title + " \u00B7 " + app);
        }
        var ogDescription = document.querySelector('meta[property="og:description"]');
        if (ogDescription && description) {
            ogDescription.setAttribute("content", description);
        }
    }

    function pageUrl(search) {
        var action = form.getAttribute("action") || window.location.pathname;
        return search ? action + "?" + search : action;
    }

    function setStatus(text) {
        if (status) {
            status.textContent = text || "";
        }
    }

    function load() {
        var id = ++requestId;
        results.setAttribute("aria-busy", "true");
        setStatus(results.getAttribute("data-updating"));

        fetch(pageUrl(query(true)), {
            headers: { "Accept": "text/html" },
            credentials: "same-origin"
        }).then(function (response) {
            if (id !== requestId) {
                return null;
            }
            if (response.redirected) {
                window.location.assign(response.url);
                return null;
            }
            if (!response.ok) {
                throw new Error("browse");
            }
            return response.text();
        }).then(function (html) {
            if (html == null || id !== requestId) {
                return;
            }
            results.innerHTML = html;
            if (window.history && history.replaceState) {
                history.replaceState(null, "", pageUrl(query(false)));
            }
            var summary = results.querySelector("[data-results-summary]");
            setStatus(summary ? summary.textContent : "");
            paint();
        }).catch(function () {
            if (id !== requestId) {
                return;
            }
            HTMLFormElement.prototype.submit.call(form);
        }).then(function () {
            if (id === requestId) {
                results.setAttribute("aria-busy", "false");
            }
        });
    }

    form.addEventListener("submit", function (event) {
        event.preventDefault();
        load();
    });

    form.addEventListener("change", function (event) {
        var target = event.target;
        if (!target || target.form !== form) {
            return;
        }
        load();
    });
})();
