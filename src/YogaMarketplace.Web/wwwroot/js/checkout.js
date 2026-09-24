(function () {
    var open = document.getElementById("razorpay-open");
    var node = document.getElementById("rzp-order");
    if (!open || !node || typeof Razorpay === "undefined") {
        return;
    }

    var paid = false;
    open.addEventListener("click", function () {
        open.setAttribute("aria-busy", "true");
        var checkout = new Razorpay({
            key: node.dataset.key,
            amount: node.dataset.amount,
            currency: node.dataset.currency,
            name: node.dataset.name,
            description: node.dataset.description,
            order_id: node.dataset.order,
            handler: function (response) {
                paid = true;
                var payment = document.getElementById("paymentId");
                var signature = document.getElementById("signature");
                var order = document.getElementById("orderId");
                if (payment) {
                    payment.value = response.razorpay_payment_id || "";
                }
                if (signature) {
                    signature.value = response.razorpay_signature || "";
                }
                if (order && response.razorpay_order_id) {
                    order.value = response.razorpay_order_id;
                }
                var confirmForm = document.getElementById("confirm-form");
                if (confirmForm) {
                    confirmForm.submit();
                }
            },
            modal: {
                ondismiss: function () {
                    open.removeAttribute("aria-busy");
                    var abandonForm = document.getElementById("abandon-form");
                    if (!paid && abandonForm) {
                        abandonForm.submit();
                    }
                }
            },
            theme: { color: "#1f6b4a" }
        });
        checkout.on("payment.failed", function () {
            open.removeAttribute("aria-busy");
            var failForm = document.getElementById("fail-form");
            if (!paid && failForm) {
                failForm.submit();
            }
        });
        checkout.open();
    });
})();
