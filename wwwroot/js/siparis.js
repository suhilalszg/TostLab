// Sipariş ekranı: ürüne tıkla -> fişe eklenir, kaynak ve ödeme seç, kaydet.
// Fiş tarayıcıda tutulur; "Siparişi kaydet" deyince sunucuya tek seferde gider.
// Fiyatı sunucu kendisi hesaplar, buradaki toplam sadece göstermek için.

(function () {
    const anahtarDegeri = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const paraBicimi = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    // Fiş: urunId -> { ad, fiyat, adet }  (Map eklenme sırasını korur)
    const fis = new Map();
    let kaynak = null;
    let masaId = null;
    let odemeTuruId = null;

    const satirAlani = document.getElementById('fis-satirlar');
    const toplamYazisi = document.getElementById('fis-toplam');
    const kaydetDugmesi = document.getElementById('kaydet');
    const uyariYazisi = document.getElementById('fis-uyari');
    const masaSecimi = document.getElementById('masa-secimi');

    // ---------- Seçenek düğmeleri (kaynak, masa, ödeme) ----------
    function secimHazirla(kutu, secilince) {
        kutu.addEventListener('click', function (olay) {
            const dugme = olay.target.closest('.secenek');
            if (!dugme) return;
            kutu.querySelectorAll('.secenek').forEach(diger => diger.classList.toggle('secili', diger === dugme));
            secilince(dugme.dataset.deger);
            durumuYaz();
        });
    }

    function secimiTemizle(kutu) {
        kutu.querySelectorAll('.secenek').forEach(dugme => dugme.classList.remove('secili'));
    }

    secimHazirla(document.getElementById('kaynak-secimi'), function (deger) {
        kaynak = deger;
        masaSecimi.hidden = kaynak !== 'Masa';
        if (kaynak !== 'Masa') {
            masaId = null;
            secimiTemizle(masaSecimi);
        }
    });
    secimHazirla(masaSecimi, deger => masaId = Number(deger));
    secimHazirla(document.getElementById('odeme-secimi'), deger => odemeTuruId = Number(deger));

    // ---------- Ürün ekle ----------
    document.querySelectorAll('.urun-kart').forEach(function (kart) {
        kart.addEventListener('click', function () {
            const id = Number(kart.dataset.id);
            const satir = fis.get(id);
            if (satir) {
                satir.adet++;
            } else {
                fis.set(id, { ad: kart.dataset.ad, fiyat: Number(kart.dataset.fiyat), adet: 1 });
            }
            fisiCiz(id);
        });
    });

    // ---------- Fişi ekrana çiz ----------
    function fisiCiz(parlayacakId) {
        satirAlani.innerHTML = '';
        if (fis.size === 0) {
            satirAlani.innerHTML = '<p class="fis-bos">Ürüne tıkla, fişe eklensin.</p>';
        }

        fis.forEach(function (satir, id) {
            const eleman = document.createElement('div');
            eleman.className = 'fis-satir' + (id === parlayacakId ? ' yeni' : '');
            eleman.innerHTML =
                '<span class="fis-ad"></span>' +
                '<span class="adet-kutu">' +
                '  <button type="button" class="adet-azalt" aria-label="Azalt">−</button>' +
                '  <b class="adet"></b>' +
                '  <button type="button" class="adet-arttir" aria-label="Arttır">+</button>' +
                '</span>' +
                '<span class="fis-tutar"></span>';
            // textContent: ürün adı ne olursa olsun HTML olarak çalışmaz
            eleman.querySelector('.fis-ad').textContent = satir.ad;
            eleman.querySelector('.adet').textContent = satir.adet;
            eleman.querySelector('.fis-tutar').textContent = paraBicimi.format(satir.adet * satir.fiyat) + ' ₺';

            eleman.querySelector('.adet-arttir').addEventListener('click', function () {
                if (satir.adet < 99) satir.adet++;
                fisiCiz();
            });
            eleman.querySelector('.adet-azalt').addEventListener('click', function () {
                satir.adet--;
                if (satir.adet <= 0) fis.delete(id);
                fisiCiz();
            });
            satirAlani.appendChild(eleman);
        });

        let toplam = 0;
        fis.forEach(satir => toplam += satir.adet * satir.fiyat);
        toplamYazisi.textContent = paraBicimi.format(toplam) + ' ₺';
        durumuYaz();
    }

    // ---------- Neyin eksik olduğunu göster ----------
    function eksikNe() {
        if (fis.size === 0) return 'Fişe ürün ekle.';
        if (!kaynak) return 'Nereden geldiğini seç.';
        if (kaynak === 'Masa' && !masaId) return 'Masa seç.';
        if (!odemeTuruId) return 'Ödeme türünü seç.';
        return '';
    }

    function durumuYaz() {
        const eksik = eksikNe();
        uyariYazisi.textContent = eksik;
        kaydetDugmesi.disabled = eksik !== '';
    }

    // ---------- Kaydet ----------
    kaydetDugmesi.addEventListener('click', async function () {
        if (eksikNe()) return;
        kaydetDugmesi.disabled = true;
        kaydetDugmesi.textContent = 'Kaydediliyor…';

        const istek = {
            kaynak: kaynak,
            masaId: masaId,
            odemeTuruId: odemeTuruId,
            satirlar: [...fis].map(([urunId, satir]) => ({ urunId: urunId, adet: satir.adet }))
        };

        try {
            const cevap = await fetch('/Siparis/Kaydet', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': anahtarDegeri },
                body: JSON.stringify(istek)
            });
            const veri = await cevap.json().catch(() => ({}));
            if (!cevap.ok) throw new Error(veri.hata || 'Kaydedilemedi.');

            bildir(`Sipariş #${veri.id} kaydedildi · ${veri.toplam} ₺`);
            fisiSifirla();
        } catch (hata) {
            uyariYazisi.textContent = hata.message;
            kaydetDugmesi.disabled = false;
        } finally {
            kaydetDugmesi.textContent = 'Siparişi kaydet';
        }
    });

    function fisiSifirla() {
        fis.clear();
        kaynak = masaId = odemeTuruId = null;
        document.querySelectorAll('.fis-panel .secenekler').forEach(secimiTemizle);
        masaSecimi.hidden = true;
        fisiCiz();
    }

    document.getElementById('temizle').addEventListener('click', function () {
        if (fis.size === 0 || confirm('Fiş temizlensin mi?')) fisiSifirla();
    });

    function bildir(mesaj) {
        const kutu = document.getElementById('bildirim');
        kutu.textContent = mesaj;
        kutu.hidden = false;
        clearTimeout(bildir.zamanlayici);
        bildir.zamanlayici = setTimeout(() => kutu.hidden = true, 3500);
    }

    // ---------- Ürün ara ----------
    const aramaKutusu = document.getElementById('urun-ara');
    const oncekiDurumlar = new Map();

    aramaKutusu.addEventListener('input', function () {
        const aranan = aramaKutusu.value.trim().toLocaleLowerCase('tr-TR');
        let gorunenToplam = 0;
        document.querySelectorAll('.siparis-urunler .kategori-blok').forEach(function (blok) {
            let gorunen = 0;
            blok.querySelectorAll('.urun-kart').forEach(function (kart) {
                const goster = kart.dataset.ad.toLocaleLowerCase('tr-TR').includes(aranan);
                kart.hidden = !goster;
                if (goster) gorunen++;
            });
            blok.hidden = gorunen === 0;
            if (aranan) {
                if (!oncekiDurumlar.has(blok)) oncekiDurumlar.set(blok, blok.open);
                blok.open = gorunen > 0;
            } else if (oncekiDurumlar.has(blok)) {
                blok.open = oncekiDurumlar.get(blok);
                oncekiDurumlar.delete(blok);
            }
            gorunenToplam += gorunen;
        });
        document.querySelector('.bos-sonuc').hidden = gorunenToplam > 0;
    });

    durumuYaz();
})();
