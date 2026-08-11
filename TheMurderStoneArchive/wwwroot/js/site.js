(function () {
    function trackCtaClick(ctaKey) {
        if (!ctaKey) {
            return;
        }

        var payload = JSON.stringify({
            cta: ctaKey,
            path: window.location.pathname,
            referrer: document.referrer || null
        });

        if (navigator.sendBeacon) {
            var blob = new Blob([payload], { type: "application/json" });
            navigator.sendBeacon("/Home/TrackCta", blob);
            return;
        }

        fetch("/Home/TrackCta", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: payload,
            keepalive: true
        }).catch(function () {
            // Intentionally ignore tracking errors.
        });
    }

    document.addEventListener("click", function (event) {
        var target = event.target;
        if (!target) {
            return;
        }

        var ctaElement = target.closest("[data-cta]");
        if (!ctaElement) {
            return;
        }

        trackCtaClick(ctaElement.getAttribute("data-cta"));
    });
})();
