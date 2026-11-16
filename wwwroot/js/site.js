// Bütün sayfalarda çalışan küçük kodlar.

// ---------- Koyu / açık tema düğmesi ----------
(function () {
    const dugme = document.getElementById('tema-dugme');
    if (!dugme) return;

    function koyuMu() {
        const secim = document.documentElement.dataset.tema;
        if (secim) return secim === 'koyu';
        return window.matchMedia('(prefers-color-scheme: dark)').matches;   // seçim yoksa bilgisayara uy
    }

    function simgeyiYaz() {
        dugme.textContent = koyuMu() ? '☀' : '☾';
    }

    dugme.addEventListener('click', function () {
        const yeni = koyuMu() ? 'acik' : 'koyu';
        document.documentElement.dataset.tema = yeni;
        try { localStorage.setItem('tema', yeni); } catch (e) { }
        simgeyiYaz();
    });

    simgeyiYaz();
})();
