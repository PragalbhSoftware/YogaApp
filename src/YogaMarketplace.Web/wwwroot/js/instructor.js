(function () {
    if (!window.fetch || !window.FormData || !window.DOMParser) {
        return;
    }

    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (!form || !form.hasAttribute || !form.hasAttribute("data-ajax")) {
            return;
        }
        if (form.getAttribute("data-sent") === "1") {
            event.preventDefault();
            return;
        }

        event.preventDefault();
        form.setAttribute("data-sent", "1");
        var button = event.submitter || form.querySelector("button[type='submit']");
        var pending = form.getAttribute("data-pending") || "";
        var failed = form.getAttribute("data-failed") || "";
        if (button) {
            button.setAttribute("aria-busy", "true");
        }
        setLive(pending);

        window.fetch(form.action, {
            method: "POST",
            body: new FormData(form),
            credentials: "same-origin",
            headers: {
                "Accept": "text/html",
                "X-Requested-With": "fetch"
            }
        }).then(function (response) {
            if (response.redirected) {
                window.location.assign(response.url);
                return null;
            }
            if (!response.ok) {
                throw new Error(String(response.status));
            }
            return response.text();
        }).then(function (html) {
            if (html == null) {
                return;
            }
            var doc = new DOMParser().parseFromString(html, "text/html");
            var mutation = doc.querySelector("[data-mutation]");
            if (!mutation) {
                throw new Error("fragment");
            }
            apply(form, mutation);
        }).catch(function () {
            release(form);
            showFallback(failed);
        });
    });

    function apply(form, mutation) {
        paintNotice(mutation);
        setLive("");
        if (mutation.getAttribute("data-ok") !== "true") {
            release(form);
            return;
        }

        var article = form.closest("article");
        var incoming = mutation.querySelector("article");
        var stays = mutation.getAttribute("data-stays") !== "false";
        if (stays && incoming && article) {
            article.replaceWith(document.importNode(incoming, true));
            return;
        }

        if (!stays && article) {
            var list = article.closest("[data-booking-list]");
            article.remove();
            if (list && !list.querySelector("article")) {
                var template = document.getElementById("inbox-empty");
                if (template && template.content) {
                    list.replaceWith(template.content.cloneNode(true));
                }
            }
        }
    }

    function paintNotice(mutation) {
        var host = document.querySelector("[data-notice-host]");
        var notice = mutation.querySelector("[data-notice]");
        if (!host || !notice) {
            return;
        }
        host.replaceChildren(document.importNode(notice, true));
    }

    function showFallback(text) {
        if (!text) {
            return;
        }
        var host = document.querySelector("[data-notice-host]");
        if (!host) {
            setLive(text);
            return;
        }
        var box = document.createElement("div");
        box.className = "notice notice-alert";
        box.setAttribute("role", "alert");
        var paragraph = document.createElement("p");
        paragraph.textContent = text;
        box.appendChild(paragraph);
        host.replaceChildren(box);
        setLive("");
    }

    function release(form) {
        form.removeAttribute("data-sent");
        var button = form.querySelector("button[type='submit']");
        if (button) {
            button.removeAttribute("aria-busy");
        }
    }

    function setLive(text) {
        var live = document.querySelector("[data-action-status]");
        if (live) {
            live.textContent = text || "";
        }
    }
})();
