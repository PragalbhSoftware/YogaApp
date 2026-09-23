(function () {
    var bar = document.querySelector(".site-header");
    var button = document.querySelector("[data-to-top]");
    var threshold = 280;

    function sync() {
        var down = window.scrollY > 8;
        if (bar) {
            bar.classList.toggle("is-stuck", down);
        }
        if (button) {
            button.hidden = window.scrollY <= threshold;
        }
    }

    if (button) {
        button.addEventListener("click", function () {
            var reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            window.scrollTo({ top: 0, behavior: reduce ? "auto" : "smooth" });
            var brand = document.querySelector(".brand");
            if (brand) {
                brand.focus();
            }
        });
    }

    window.addEventListener("scroll", sync, { passive: true });
    sync();
})();
