/* =====================================================================
   Bibliotheca – Public Self-Service Kiosk behaviour
   - Live clock
   - On-screen keypad for card numbers / item codes
   - Idle detection: warning overlay, then automatic session end
   - Lockout countdown after too many failed card attempts
   - Optional camera QR/barcode scanning (html5-qrcode, loaded on demand)
   Everything degrades gracefully: without JS the kiosk still works with a
   keyboard or USB barcode scanner, and the server enforces the idle timeout.
   ===================================================================== */
(function () {
    'use strict';

    var body = document.body;
    var identified = body.dataset.kioskIdentified === 'true';
    var idleSeconds = parseInt(body.dataset.kioskIdleSeconds || '90', 10);
    var warningSeconds = parseInt(body.dataset.kioskWarningSeconds || '15', 10);
    var homeLink = document.querySelector('.kiosk-brand');
    var homeUrl = homeLink ? homeLink.getAttribute('href') : '/Kiosk';

    /* ---------- Clock ---------- */
    var clock = document.getElementById('kiosk-clock');
    function tick() {
        if (!clock) return;
        var now = new Date();
        clock.textContent = now.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' }) +
            '  ' + now.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    }
    tick();
    setInterval(tick, 15000);

    /* ---------- On-screen keypad ---------- */
    document.querySelectorAll('.kiosk-keypad').forEach(function (pad) {
        var input = document.getElementById(pad.dataset.target);
        if (!input) return;

        var prefixes = (pad.dataset.prefixes || '').split(',').filter(Boolean);
        var keys = [];
        prefixes.forEach(function (p) { keys.push({ label: p, value: p, cls: 'kiosk-key-prefix', prefix: true }); });
        '1234567890'.split('').forEach(function (d) { keys.push({ label: d, value: d }); });
        keys.push({ label: '-', value: '-' });
        keys.push({ label: '⌫', aria: 'Delete last character', action: 'back', cls: 'kiosk-key-action' });
        keys.push({ label: 'Clear', action: 'clear', cls: 'kiosk-key-action kiosk-key-wide' });

        keys.forEach(function (k) {
            var b = document.createElement('button');
            b.type = 'button';
            b.className = 'kiosk-key ' + (k.cls || '');
            b.textContent = k.label;
            if (k.aria) b.setAttribute('aria-label', k.aria);
            b.addEventListener('click', function () {
                if (input.disabled) return;
                if (k.action === 'back') input.value = input.value.slice(0, -1);
                else if (k.action === 'clear') input.value = '';
                else if (k.prefix) input.value = k.value; // a prefix starts a new code
                else input.value = (input.value + k.value).slice(0, 30);
                input.focus({ preventScroll: true });   // keep focus so a USB scanner still types into the box
            });
            pad.appendChild(b);
        });
    });

    /* ---------- Auto-submit category chips ---------- */
    document.querySelectorAll('[data-autosubmit]').forEach(function (el) {
        el.addEventListener('change', function () { if (el.form) el.form.submit(); });
    });

    /* ---------- Prevent double submit on confirm ---------- */
    document.querySelectorAll('[data-once]').forEach(function (btn) {
        if (!btn.form) return;
        btn.form.addEventListener('submit', function () {
            setTimeout(function () { btn.disabled = true; }, 0);
        });
    });

    /* ---------- Lockout countdown ---------- */
    var lock = document.querySelector('[data-kiosk-lockout]');
    if (lock) {
        var remaining = parseInt(lock.dataset.kioskLockout, 10);
        var count = lock.querySelector('[data-kiosk-lockout-count]');
        var t = setInterval(function () {
            remaining -= 1;
            if (count) count.textContent = Math.max(remaining, 0);
            if (remaining <= 0) { clearInterval(t); window.location.href = homeUrl; }
        }, 1000);
    }

    /* ---------- Idle detection ---------- */
    var idleTimer = null, warnTimer = null, warnLeft = warningSeconds;
    var overlay = document.getElementById('kiosk-idle');
    var overlayCount = document.getElementById('kiosk-idle-count');
    var endForm = document.getElementById('kiosk-end-form');

    function endSession(timedOut) {
        if (!endForm) { window.location.href = homeUrl; return; }
        var flag = document.getElementById('kiosk-timed-out');
        if (flag) flag.value = timedOut ? 'true' : 'false';
        endForm.submit();
    }

    function showWarning() {
        if (!overlay) { endSession(true); return; }
        warnLeft = warningSeconds;
        if (overlayCount) overlayCount.textContent = warnLeft;
        overlay.hidden = false;
        var stay = document.getElementById('kiosk-idle-stay');
        if (stay) stay.focus();
        warnTimer = setInterval(function () {
            warnLeft -= 1;
            if (overlayCount) overlayCount.textContent = Math.max(warnLeft, 0);
            if (warnLeft <= 0) { clearInterval(warnTimer); endSession(true); }
        }, 1000);
    }

    function onIdle() {
        if (identified) {
            showWarning();
        } else {
            // Nobody signed in: tidy up for the next person.
            document.querySelectorAll('.kiosk-input').forEach(function (i) { i.value = ''; });
            var path = window.location.pathname.replace(/\/+$/, '').toLowerCase();
            if (path !== homeUrl.replace(/\/+$/, '').toLowerCase() && path !== '/kiosk/index') {
                window.location.href = homeUrl;
            }
        }
    }

    function resetIdle() {
        if (overlay && !overlay.hidden) return; // must press a button on the warning
        clearTimeout(idleTimer);
        idleTimer = setTimeout(onIdle, idleSeconds * 1000);
    }

    ['pointerdown', 'keydown', 'touchstart', 'wheel', 'input'].forEach(function (evt) {
        document.addEventListener(evt, resetIdle, { passive: true });
    });
    resetIdle();

    if (overlay) {
        document.getElementById('kiosk-idle-stay').addEventListener('click', function () {
            clearInterval(warnTimer);
            overlay.hidden = true;
            resetIdle();
            // Ping the server so its idle clock is refreshed too.
            fetch(window.location.href, { credentials: 'same-origin', cache: 'no-store' }).catch(function () { });
        });
        document.getElementById('kiosk-idle-end').addEventListener('click', function () {
            clearInterval(warnTimer);
            endSession(false);
        });
    }

    /* ---------- Camera QR / barcode scanning (optional) ---------- */
    var scannerLibUrl = 'https://cdn.jsdelivr.net/npm/html5-qrcode@2.3.8/html5-qrcode.min.js';
    var scannerLoading = null;

    function loadScannerLib() {
        if (window.Html5Qrcode) return Promise.resolve();
        if (scannerLoading) return scannerLoading;
        scannerLoading = new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = scannerLibUrl;
            s.onload = resolve;
            s.onerror = function () { scannerLoading = null; reject(new Error('Scanner library could not be loaded.')); };
            document.head.appendChild(s);
        });
        return scannerLoading;
    }

    function openCameraScanner(input) {
        var wrap = document.createElement('div');
        wrap.className = 'kiosk-camera';
        wrap.setAttribute('role', 'dialog');
        wrap.setAttribute('aria-modal', 'true');
        wrap.setAttribute('aria-label', 'Camera scanner');
        wrap.innerHTML =
            '<div class="kiosk-camera-card">' +
            '<h2 class="kiosk-h2 justify-content-center"><i class="bi bi-qr-code-scan" aria-hidden="true"></i> Hold the code up to the camera</h2>' +
            '<div id="kiosk-camera-view" class="kiosk-camera-view"></div>' +
            '<p class="kiosk-camera-status" role="status">Starting camera…</p>' +
            '<div class="kiosk-actions-row justify-content-center"><button type="button" class="kiosk-btn kiosk-btn-outline kiosk-btn-lg">Cancel</button></div>' +
            '</div>';
        document.body.appendChild(wrap);

        var status = wrap.querySelector('.kiosk-camera-status');
        var cancel = wrap.querySelector('button');
        var scanner = null;

        function close() {
            var done = function () { wrap.remove(); input.focus({ preventScroll: true }); };
            if (scanner) { scanner.stop().then(done, done); } else { done(); }
        }
        cancel.addEventListener('click', close);
        cancel.focus();

        if (!window.isSecureContext || !navigator.mediaDevices) {
            status.textContent = 'The camera only works on http://localhost or an HTTPS address. Please type the code instead.';
            return;
        }

        loadScannerLib().then(function () {
            scanner = new window.Html5Qrcode('kiosk-camera-view');
            return scanner.start(
                { facingMode: 'environment' },
                { fps: 10, qrbox: { width: 240, height: 240 } },
                function (text) {
                    var value = (text || '').trim().toUpperCase().slice(0, 30);
                    if (!value) return;
                    status.textContent = 'Got it: ' + value;
                    scanner.stop().then(function () {
                        scanner = null;
                        wrap.remove();
                        input.value = value;
                        if (input.form) input.form.submit();
                    });
                },
                function () { /* no code in this frame */ }
            ).then(function () { status.textContent = 'Looking for a code…'; });
        }).catch(function (err) {
            status.textContent = 'Camera not available (' + (err && err.message ? err.message : err) + '). Please type the code instead.';
        });
    }

    document.querySelectorAll('[data-camera-scan]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var input = document.getElementById(btn.dataset.cameraScan);
            if (input && !input.disabled) openCameraScanner(input);
        });
    });
})();
