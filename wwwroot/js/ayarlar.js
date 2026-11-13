// Ayarlar sayfası: masa ve ödeme türü adını değiştir, aç/kapat, yeni ekle.
// Menü sayfasıyla aynı yöntem: sayfa yenilenmeden küçük istekler (fetch).

(function () {
    const anahtarDegeri = document.querySelector('input[name="__RequestVerificationToken"]').value;

    async function gonder(adres, bilgiler) {
        const cevap = await fetch(adres, {
            method: 'POST',
            headers: { 'RequestVerificationToken': anahtarDegeri },
            body: new URLSearchParams(bilgiler)
        });
        const veri = await cevap.json().catch(() => ({}));
        if (!cevap.ok) {
            const hata = new Error(veri.hata || 'Kaydedilemedi.');
            hata.veri = veri;
            throw hata;
        }
        return veri;
    }

    function isaretle(eleman, sinif) {
        eleman.classList.remove('kaydedildi', 'hatali');
        void eleman.offsetWidth;   // animasyonu yeniden başlat
        eleman.classList.add(sinif);
    }

    function acikSayisiniYaz(blok) {
        blok.querySelector('.acik-sayisi').textContent = blok.querySelectorAll('.anahtar.acik').length;
    }

    // ---------- Ad: yazıp Enter'a basınca veya kutudan çıkınca kaydet ----------
    function adKutusunuHazirla(kutu) {
        kutu.dataset.kayitli = kutu.value;

        async function kaydet() {
            if (kutu.value.trim() === kutu.dataset.kayitli) {
                kutu.value = kutu.dataset.kayitli;
                return;
            }
            const satir = kutu.closest('.ayar-satir');
            const blok = kutu.closest('.ayar-blok');
            try {
                const veri = await gonder('/Ayarlar/AdDegistir',
                    { liste: blok.dataset.liste, id: satir.dataset.id, ad: kutu.value });
                kutu.value = veri.ad;
                kutu.dataset.kayitli = veri.ad;
                kutu.title = '';
                isaretle(kutu, 'kaydedildi');
            } catch (hata) {
                kutu.value = kutu.dataset.kayitli;   // eski ada dön
                isaretle(kutu, 'hatali');
                alert(hata.message);
                setTimeout(() => kutu.classList.remove('hatali'), 1500);
            }
        }

        kutu.addEventListener('keydown', function (olay) {
            if (olay.key === 'Enter') {
                olay.preventDefault();
                kutu.blur();   // blur kaydeder
            } else if (olay.key === 'Escape') {
                kutu.value = kutu.dataset.kayitli;
                kutu.blur();
            }
        });
        kutu.addEventListener('blur', kaydet);
    }

    // ---------- Aç / kapat ----------
    function anahtariHazirla(dugme) {
        dugme.addEventListener('click', async function () {
            const satir = dugme.closest('.ayar-satir');
            const blok = dugme.closest('.ayar-blok');
            try {
                const veri = await gonder('/Ayarlar/Durum', { liste: blok.dataset.liste, id: satir.dataset.id });
                dugme.classList.toggle('acik', veri.aktifMi);
                satir.classList.toggle('kapali', !veri.aktifMi);
                acikSayisiniYaz(blok);
            } catch (hata) {
                alert(hata.message);
            }
        });
    }

    document.querySelectorAll('.ad-gir').forEach(adKutusunuHazirla);
    document.querySelectorAll('.ayar-satir .anahtar').forEach(anahtariHazirla);

    // ---------- Yeni ekle ----------
    document.querySelectorAll('.ayar-blok').forEach(function (blok) {
        const dugme = blok.querySelector('.ekle-ac');
        const form = blok.querySelector('.hizli-ekle');

        function formuKapat() {
            form.reset();
            form.hidden = true;
            dugme.hidden = false;
        }

        dugme.addEventListener('click', function () {
            dugme.hidden = true;
            form.hidden = false;
            form.ad.focus();
        });

        form.addEventListener('keydown', function (olay) {
            if (olay.key === 'Escape') formuKapat();
        });

        form.addEventListener('submit', async function (olay) {
            olay.preventDefault();
            try {
                const veri = await gonder('/Ayarlar/Ekle', { liste: blok.dataset.liste, ad: form.ad.value });

                // Yeni satırı sayfayı yenilemeden listeye ekle
                const satir = document.createElement('div');
                satir.className = 'ayar-satir yeni';
                satir.dataset.id = veri.id;
                satir.innerHTML = '<input type="text" class="ad-gir" maxlength="40" />' +
                                  '<button type="button" class="anahtar acik" title="Aç / kapat"></button>';
                satir.querySelector('.ad-gir').value = veri.ad;   // value ile: ad içindeki < > güvenli kalır
                blok.querySelector('.ekle-alani').before(satir);
                adKutusunuHazirla(satir.querySelector('.ad-gir'));
                anahtariHazirla(satir.querySelector('.anahtar'));
                acikSayisiniYaz(blok);

                form.reset();
                form.ad.focus();   // art arda ekleyebilsin
            } catch (hata) {
                form.ad.title = hata.message;
                isaretle(form, 'hatali');
                alert(hata.message);
            }
        });
    });
})();
