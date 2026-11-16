# TOST LAB · Adisyon ve Gün Sonu Paneli

Küçük bir tost & burger dükkanı (TOST LAB / ZIP BURGER) için yazılmış **gerçek bir müşteri projesi**.
Dükkan siparişleri kağıt adisyonla tutuyordu; gün sonunda kaç sipariş geldiği, ne kadar iptal olduğu,
paranın nakit/kart/yemek kartı/platform olarak nasıl dağıldığı elle hesaplanıyordu.
Bu uygulama dükkanın bilgisayarında çalışır: siparişi masaya ya da pakete girer, ödemeyi alır,
gün sonunda raporu hazırlar ve WhatsApp'a gönderilecek metni çıkarır.

## Özellikler

| Ekran | Ne yapar | Kim görür |
|---|---|---|
| **Satış** | Masa planı (boş / dolu), kategori → ürün → adisyon tek ekranda. Masa, telefonla paket ve Yemeksepeti / Getir / Trendyol / Migros siparişleri. Kişi sayısı, adet değiştirme, indirim (%5–20 tek dokunuş ya da yazılan % / ₺), iptal (nedeni yazılır), ödeme türü seçerek kapatma. | Herkes |
| **Bugün** | Günün özeti: ciro, ortalama adisyon, ödeme türüne ve kaynağa göre dağılım, en çok satan ürünler, saatlere göre yoğunluk. | Yönetici her şeyi, çalışan sadece adetleri |
| **Kayıtlar** | Geçmiş adisyonlar: gün / durum / kaynak / ödeme türüne göre süzme, fiş görünümünde detay (ÖDENDİ / İPTAL damgası), adisyondan silinen ürünlerin izi. | Yönetici |
| **Gün sonu** | Seçilen günün raporu ve aylık özet; platform komisyonu düşülmüş net kazanç; WhatsApp'ta açılacak hazır metin, yazdırma. | Yönetici |
| **Menü** | Ürün ekle / düzenle / satışa aç-kapat, listede hızlı fiyat girişi. | Yönetici |
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
  RaporServisi     Kayıtlar, Bugün, Gün sonu, Aylık hesapları (sadece okur)
  RaporMetni       raporu WhatsApp düz metnine çevirir
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
- **İz bırakılır.** İptal edilen adisyonun nedeni, saati ve kimin ettiği; açık adisyondan silinen her ürün (kim, ne zaman, kaç tane) kaydedilir.
- **Çalışan para görmez.** Bugün sayfasında ciro ve tutarlar sunucuda hiç çizilmez; Kayıtlar ve Gün sonu sadece yöneticiye açıktır.
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
- [x] Kayıtlar, Bugün, Gün sonu ve aylık özet, platform komisyonu
- [x] Ay sonunda aylık raporun kendiliğinden e-postayla gönderilmesi
- [x] İndirim (yüzde ya da tutar, kim yaptı kaydedilir; platform siparişinde yok)
- [ ] Dükkanda gerçek kullanım ve geri bildirimle düzeltmeler
