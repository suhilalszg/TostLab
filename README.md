# TOST LAB · Adisyon ve Gün Sonu Paneli

Küçük bir tost & burger dükkanı (TOST LAB / ZIP BURGER) için yazılmış **gerçek bir müşteri projesi**.
Dükkan siparişleri kağıt adisyonla tutuyordu; gün sonunda kaç sipariş geldiği, ne kadar iptal olduğu,
paranın nakit/kart/yemek kartı/platform olarak nasıl dağıldığı elle hesaplanıyordu.
Bu uygulama dükkanın bilgisayarında çalışır: siparişi masaya ya da pakete girer, ödemeyi alır,
gün sonunda raporu hazırlar, ay sonunda özetini e-postayla gönderir.

## Özellikler

| Ekran | Ne yapar | Kim görür |
|---|---|---|
| **Satış** | Mor-pembe dokunmatik POS ekranı. Masa planı (boş / dolu, garson adı, tutar, geçen süre), solda açık adisyonlar ve toplamı, sağda süzgeçler ve yeni paket / platform siparişi. Satışta fotoğraflı ürün kartları ve dikey kategori çubuğu. İşlemler: **İkram** (ücretsiz, adet adet), **İade** (bugün ödenmiş adisyon, nedenli), **Böl** (seçilen ürünleri ayrı öde), **İskonto** (% ya da ₺), **Taşı** (boş masaya taşı ya da dolu masayla birleştir), **İptal** (nedenli). Üst çubukta internet / sunucu durumu ve kullanıcı. | Herkes |
| **Fişler** | Ödenen her adisyonun fişi ödeme anında olduğu gibi saklanır. Günlük ya da aylık listelenir, ödeme türüne göre süzülür; iade edilen fişte İADE damgası. Bir fiş kayıtlardan silinebilir (satış ve raporlar etkilenmez). | Yönetici |
| **Adisyon** | Gün sonu ve aylık rapor tek panelde (gün / hafta / ay). Sırasıyla: **1) Finans ve kasa**: brüt → net ciro, ödeme yöntemi (nakit, kart, her yemek kartı, platformlar, açık hesap), kasa sayımı (açık / fazla). **2) Fire**: iptaller ve silinen kalemler, ikram (tutar + maliyet), indirimler (oran, neden, kim), açık kalan adisyonlar. **3) Operasyon**: adisyon sayısı, ortalama sepet, masa devri, saatlik yoğunluk, en çok / en az satan 5, ilk 10 ciro ve adet. **4) Kârlılık**: önceki dönem ve geçen yıl kıyası, platform komisyonu, giderler (kasa çıkışı), gün × saat ısı haritası, satış kanalları (paket ve platformlar), kategori yığını, aylık hedef ibresi. Sonda bütün adisyonlar (süzgeçli) ve fiş görünümünde detay; ay görünümünden aylık e-posta. | Yönetici |
| **Menü** | Ürün ekle / düzenle / satışa aç-kapat, listede hızlı fiyat girişi, ürün fotoğrafı yükleme (JPG / PNG / WEBP, en fazla 3 MB). | Yönetici |
| **Ayarlar** | Masalar, ödeme türleri (Nakit, Kart, Multinet, Pluxee, Edenred, Setcard, Metropol…), platform komisyon oranları. | Yönetici |
| **Kullanıcılar** | Yönetici ve çalışan hesapları. | Yönetici |

Açık ve koyu tema vardır; dış kaynağa ihtiyaç duymaz (yazı tipi ve kütüphaneler projenin içinde), internet olmadan çalışır.

## Teknoloji

- **ASP.NET Core MVC (.NET 10)**: sayfalar sunucuda çizilir (Razor)
- **Entity Framework Core 10 + SQLite**: tek dosyalık veritabanı (`tostlab.db`), kurulum gerektirmez
- **Cookie tabanlı giriş + roller** (Yönetici / Çalışan), şifreler `PasswordHasher` ile hash'lenir
- Satış ekranında **Alpine.js** ve **Tailwind** (yerel kopya), diğer sayfalarda Bootstrap + kendi tema dosyası

## Mimari

```
Controllers/   İnce: isteği alır, servise verir, sonucu sayfaya / JSON'a çevirir
Services/      İş kuralları burada
  AdisyonServisi   adisyon açma, ürün ekleme, adet, ödeme, iptal (yazan tek yer)
  RaporServisi     Adisyon paneli ve aylık e-posta hesapları (sadece okur)
  FisServisi       fiş kayıtları: ödemede fişi saklar, listeler, siler
  Grafik           panel grafiklerini sunucuda SVG çizer (halka, ısı haritası, yığın, ibre)
  RaporEpostasi    aylık raporu HTML e-postaya çevirir
  RaporGondermeServisi + AySonuZamanlayici
                   ay bitince geçen ayın raporunu e-postayla gönderir (arka plan servisi)
Models/        Veritabanı sınıfları + ekrana giden küçük kayıtlar (DTO / record)
Data/          DbContext, ilişkiler, indeksler, başlangıç verisi
Migrations/    Veritabanı şemasının adım adım geçmişi
```

Önemli kararlar:

- **Fiyat sunucuda hesaplanır.** Satış ekranı sadece "hangi ürün, kaç tane" gönderir; tutarı sunucu ürün tablosundan bulur.
- **Geçmiş bozulmaz.** Satırda sipariş anındaki birim fiyat, siparişte ödeme anındaki komisyon oranı saklanır.
  Fiyat ya da oran sonradan değişse de eski raporlar aynı kalır. Siparişi olan masa / ödeme türü / ürün silinemez, sadece kapatılır.
- **İz bırakılır.** İptal / iade edilen adisyonun nedeni, saati ve kimin ettiği; ikramı kimin yaptığı; adisyonu kimin açtığı (garson); açık adisyondan silinen her ürün (kim, ne zaman, kaç tane) kaydedilir.
- **Fotoğraflar diskte.** Menü'den yüklenen fotoğraflar `urun-foto/` klasörüne yazılır (git'e girmez), `/urun-foto/...` adresinden sunulur. Dosyanın gerçekten resim olduğu ilk baytlarından kontrol edilir.
- **Çalışan para görmez.** Adisyon paneli ve Fişler sadece yöneticiye açıktır; çalışan Satış ekranını kullanır.
- **Gün = adisyonun açıldığı takvim günü** (dükkan gece 12'de kapanıyor).

## Çalıştırma

Gerekenler: [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/suhilalszg/TostLab.git
cd TostLab
dotnet run
```

Tarayıcıda `http://localhost:5050` açılır. İlk açılışta veritabanı kendiliğinden oluşur, menü yüklenir ve
**Kurulum** sayfası ilk yönetici hesabını ister. Sonra Menü'den fiyatları girmek yeterli.

Visual Studio ile: `TostLab.slnx` dosyasını açıp **Ctrl+F5**.

### Aylık rapor e-postası

Her ayın 1'inde (bilgisayar o an kapalıysa açıldığı ilk yarım saatte) geçen ayın raporu e-postayla gider.
Aynı ay iki kez gönderilmez. Açmak için `eposta.ornek.json` dosyasını `eposta.json` adıyla kopyalayıp doldur:

- `KullaniciAdi`: raporu gönderecek Gmail adresi
- `Sifre`: o hesabın **uygulama şifresi** (Google Hesabı → Güvenlik → 2 adımlı doğrulama → Uygulama şifreleri), normal şifre değil
- `Alici`: raporun gideceği adres

`eposta.json` git'e girmez. Dosya yoksa program normal çalışır, sadece e-posta gitmez.
Aylık özet sayfasındaki **Şimdi e-postayla gönder** düğmesiyle ayarı denemek mümkün.

Veritabanında değişiklik yaparken (geliştirici için):

```bash
dotnet tool restore
dotnet ef migrations add DegisiklikAdi
```

Yeni migration uygulama açılırken kendiliğinden uygulanır.

## Yol haritası

- [x] Veri modeli, veritabanı, giriş ve roller
- [x] Menü, masalar, ödeme türleri
- [x] Satış ekranı (masa planı, paket ve platform siparişleri, iptal)
- [x] Adisyon paneli (gün sonu + aylık), platform komisyonu
- [x] Fiş kayıtları (günlük / aylık, silinebilir)
- [x] Kasa sayımı, giderler, ürün maliyeti, aylık hedef
- [x] Ay sonunda aylık raporun kendiliğinden e-postayla gönderilmesi
- [x] İndirim (yüzde ya da tutar, kim yaptı kaydedilir; platform siparişinde yok)
- [x] Yeni tasarım (mor-pembe POS), ürün fotoğrafları, ikram / iade / böl / taşı, garson adı
- [ ] Dükkanda gerçek kullanım ve geri bildirimle düzeltmeler
