(function () {
    document.querySelectorAll("form[data-once]").forEach(function (form) {
        form.addEventListener("submit", function (event) {
            if (form.getAttribute("data-sent") === "1") {
                event.preventDefault();
                return;
            }
            form.setAttribute("data-sent", "1");
            var busy = form.getAttribute("data-busy");
            var button = event.submitter || form.querySelector("button[type='submit']");
            if (button) {
                button.setAttribute("aria-busy", "true");
                if (busy) {
                    button.textContent = busy;
                }
            }
            var status = form.querySelector("[data-form-status]");
            if (status && busy) {
                status.textContent = busy;
            }
        });
    });

    document.querySelectorAll("[data-refresh-slots]").forEach(function (button) {
        button.addEventListener("click", function () {
            var panel = button.closest("[data-slot-panel]");
            var url = button.getAttribute("data-url");
            if (!panel || !url || !window.fetch || !window.DOMParser) {
                return;
            }
            if (button.getAttribute("aria-busy") === "true") {
                return;
            }

            var status = panel.querySelector("[data-slot-status]");
            var pending = button.getAttribute("data-pending") || "";
            var done = button.getAttribute("data-done") || "";
            var failed = button.getAttribute("data-failed") || "";
            button.setAttribute("aria-busy", "true");
            if (status) {
                status.textContent = pending;
            }

            window.fetch(url, { headers: { "Accept": "text/html" }, credentials: "same-origin" })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error(String(response.status));
                    }
                    return response.text();
                })
                .then(function (html) {
                    var doc = new DOMParser().parseFromString(html, "text/html");
                    var message = panel.querySelector("[data-slot-message]");
                    var error = doc.querySelector("[data-slot-error]");
                    var empty = doc.querySelector("[data-slot-empty]");
                    if (message) {
                        if (error) {
                            message.innerHTML = error.outerHTML;
                        } else if (empty) {
                            message.innerHTML = empty.outerHTML;
                        } else {
                            message.textContent = "";
                        }
                    }
                    var incoming = doc.querySelector("[data-slot-options]");
                    var select = panel.querySelector("select[name='slotId']");
                    var form = panel.querySelector("form.reschedule");
                    if (incoming && select) {
                        select.innerHTML = incoming.innerHTML;
                    }
                    if (form && incoming) {
                        form.hidden = !incoming.querySelector("option[value]:not([value=''])");
                    }
                    if (status) {
                        status.textContent = done;
                    }
                })
                .catch(function () {
                    if (status) {
                        status.textContent = failed;
                    }
                })
                .then(function () {
                    button.removeAttribute("aria-busy");
                });
        });
    });
})();
