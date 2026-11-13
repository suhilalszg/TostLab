// Menü sayfası: fiyat kaydetme, satışa aç/kapat, hızlı ekleme, arama.
// Sayfa yenilenmeden sunucuya küçük istekler (fetch) gönderir.

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
            throw new Error(veri.hata || 'Kaydedilemedi.');
        }
        return veri;
    }

    function isaretle(eleman, sinif) {
        eleman.classList.remove('kaydedildi', 'hatali');
        void eleman.offsetWidth;   // animasyonu yeniden başlat
        eleman.classList.add(sinif);
    }

    // Kategori başlığındaki "8 ürün · 3 fiyatsız" yazısını yenile
    function kategoriBilgisiniYaz(blok) {
        const kutular = [...blok.querySelectorAll('.fiyat-gir')];
        const fiyatsiz = kutular.filter(kutu => kutu.value.trim() === '').length;
        blok.querySelector('.bilgi').innerHTML =
            `${kutular.length} ürün` + (fiyatsiz > 0 ? ` <em>${fiyatsiz} fiyatsız</em>` : '');
    }

    // ---------- Fiyat: Enter'a basınca veya kutudan çıkınca kaydet ----------
    const fiyatKutulari = [...document.querySelectorAll('.fiyat-gir')];

    fiyatKutulari.forEach(function (kutu) {
        kutu.dataset.kayitli = kutu.value;

        async function kaydet() {
            if (kutu.value.trim() === kutu.dataset.kayitli) {
                return;
            }
            const satir = kutu.closest('.urun-satir');
            try {
                const veri = await gonder('/Urunler/FiyatKaydet', { id: satir.dataset.id, fiyat: kutu.value });
                kutu.value = veri.fiyat;
                kutu.dataset.kayitli = veri.fiyat;
                kutu.title = '';
                document.getElementById('sayac-fiyatsiz').textContent = veri.fiyatsizUrun;
                document.getElementById('fiyatsiz-suz').classList.toggle('uyari', veri.fiyatsizUrun > 0);
                isaretle(kutu.parentElement, 'kaydedildi');
                kategoriBilgisiniYaz(kutu.closest('.kategori-blok'));
            } catch (hata) {
                kutu.title = hata.message;
                isaretle(kutu.parentElement, 'hatali');
            }
        }

        kutu.addEventListener('keydown', function (olay) {
            if (olay.key === 'Enter' || olay.key === 'ArrowDown' || olay.key === 'ArrowUp') {
                olay.preventDefault();
                kaydet();
                // Görünen bir sonraki/önceki fiyat kutusuna geç
                const gorunenler = fiyatKutulari.filter(diger => diger.offsetParent !== null);
                const sira = gorunenler.indexOf(kutu) + (olay.key === 'ArrowUp' ? -1 : 1);
                if (gorunenler[sira]) {
                    gorunenler[sira].focus();
                    gorunenler[sira].select();
                }
            } else if (olay.key === 'Escape') {
                kutu.value = kutu.dataset.kayitli;
                kutu.blur();
            }
        });
        kutu.addEventListener('blur', kaydet);
        kutu.addEventListener('focus', () => kutu.select());
    });

    // ---------- Satışa aç / kapat ----------
    document.querySelectorAll('.anahtar').forEach(function (dugme) {
        dugme.addEventListener('click', async function () {
            const satir = dugme.closest('.urun-satir');
            try {
                const veri = await gonder('/Urunler/SatisDurumu', { id: satir.dataset.id });
                dugme.classList.toggle('acik', veri.satistaMi);
                satir.classList.toggle('kapali', !veri.satistaMi);
            } catch (hata) {
                alert(hata.message);
            }
        });
    });

    // ---------- Kategorinin altındaki hızlı ekleme ----------
    // "+ Ürün ekle" yazısına basınca kutular açılır
    document.querySelectorAll('.ekle-ac').forEach(function (dugme) {
        dugme.addEventListener('click', function () {
            const form = dugme.nextElementSibling;
            dugme.hidden = true;
            form.hidden = false;
            form.ad.focus();
        });
    });

    // Esc ile vazgeç
    document.querySelectorAll('.hizli-ekle').forEach(function (form) {
        form.addEventListener('keydown', function (olay) {
            if (olay.key === 'Escape') {
                form.reset();
                form.hidden = true;
                form.previousElementSibling.hidden = false;
            }
        });
    });

    document.querySelectorAll('.hizli-ekle').forEach(function (form) {
        form.addEventListener('submit', async function (olay) {
            olay.preventDefault();
            try {
                const veri = await gonder('/Urunler/HizliEkle', {
                    kategori: form.dataset.kategori,
                    ad: form.ad.value,
                    fiyat: form.fiyat.value
                });
                // Sayfayı yeniden aç, yeni ürüne kay ve onu parlat
                location.href = '/Urunler?yeni=' + veri.id;
            } catch (hata) {
                form.ad.title = hata.message;
                isaretle(form, 'hatali');
                form.ad.focus();
            }
        });
    });

    // ---------- Arama ve "sadece fiyatsız" süzgeci ----------
    const aramaKutusu = document.getElementById('menu-ara');
    const fiyatsizDugmesi = document.getElementById('fiyatsiz-suz');
    let sadeceFiyatsiz = false;
    const oncekiDurumlar = new Map();   // arama başlamadan önce hangi kategori açıktı

    function suz() {
        const aranan = aramaKutusu.value.trim().toLocaleLowerCase('tr-TR');
        let gorunenToplam = 0;

        document.querySelectorAll('.kategori-blok').forEach(function (blok) {
            let gorunenSatir = 0;
            blok.querySelectorAll('.urun-satir').forEach(function (satir) {
                const metinUyuyor = satir.dataset.ara.toLocaleLowerCase('tr-TR').includes(aranan);
                const fiyatUyuyor = !sadeceFiyatsiz || satir.querySelector('.fiyat-gir').value.trim() === '';
                const goster = metinUyuyor && fiyatUyuyor;
                satir.hidden = !goster;
                if (goster) gorunenSatir++;
            });
            const suzgecVar = aranan !== '' || sadeceFiyatsiz;
            blok.hidden = gorunenSatir === 0;
            // Arama yapılırken sonuç olan kategorileri aç; arama silinince eski haline dön
            if (suzgecVar) {
                if (!oncekiDurumlar.has(blok)) oncekiDurumlar.set(blok, blok.open);
                blok.open = gorunenSatir > 0;
            } else if (oncekiDurumlar.has(blok)) {
                blok.open = oncekiDurumlar.get(blok);
                oncekiDurumlar.delete(blok);
            }
            blok.querySelector('.ekle-alani').hidden = suzgecVar;
            gorunenToplam += gorunenSatir;
        });

        document.querySelector('.bos-sonuc').hidden = gorunenToplam > 0;
    }

    aramaKutusu.addEventListener('input', suz);
    fiyatsizDugmesi.addEventListener('click', function () {
        sadeceFiyatsiz = !sadeceFiyatsiz;
        fiyatsizDugmesi.classList.toggle('secili', sadeceFiyatsiz);
        suz();
    });

    // ---------- Az önce eklenen ürünü göster ----------
    const yeniKimlik = new URLSearchParams(location.search).get('yeni');
    const yeniSatir = yeniKimlik && document.querySelector(`.urun-satir[data-id="${CSS.escape(yeniKimlik)}"]`);
    if (yeniSatir) {
        yeniSatir.closest('.kategori-blok').open = true;
        yeniSatir.scrollIntoView({ block: 'center' });
        yeniSatir.classList.add('yeni');
        history.replaceState(null, '', '/Urunler');
    }
})();
