namespace TostLab.Models;

// Satış ekranı ile sunucu arasında gidip gelen JSON şekilleri.
// "record": sadece veri taşıyan, kısa yazılan sınıf.

// ---------- Sunucudan ekrana ----------

public record SatirBilgisi(int Id, int UrunId, string Ad, int Adet, decimal BirimFiyat, decimal Tutar);

public record AdisyonBilgisi(
    int Id,
    string Kaynak,
    int? MasaId,
    string Baslik,          // "İç 2" ya da "Yemeksepeti 14:05"
    DateTime Acilis,
    decimal Toplam,         // indirimden sonra, ödenecek
    int? KisiSayisi,
    List<SatirBilgisi> Satirlar,
    IndirimBilgisi? Indirim);   // indirim yoksa null

// Ekranın indirimi aynı kuralla kendisi de hesaplayabilsin diye değer + tür birlikte gelir
public record IndirimBilgisi(bool YuzdeMi, decimal Deger, decimal Tutar, string? Neden);

public record MasaBilgisi(
    int Id,
    string Ad,
    bool DoluMu,            // false: yeşil (boş), true: amber (dolu)
    int? AdisyonId,
    decimal Toplam,
    DateTime? Acilis,
    int? KisiSayisi);

public record PaketBilgisi(int AdisyonId, string Baslik, string Kaynak, decimal Toplam, DateTime Acilis);

// Masa planının tamamı: her değişiklikten sonra ekran bunu yeniden çizer
public record EkranDurumu(List<MasaBilgisi> Masalar, List<PaketBilgisi> Paketler);

// Her işlemden sonra dönen cevap: güncel adisyon (kapandıysa null) + güncel masa planı
public record IslemSonucu(AdisyonBilgisi? Adisyon, EkranDurumu Ekran);

// ---------- Ekrandan sunucuya ----------

// KisiSayisi: masa açılırken önceden girildiyse ilk ürünle birlikte gelir
public record UrunEkleIstegi(int? AdisyonId, string Kaynak, int? MasaId, int UrunId, int Adet, int? KisiSayisi);
public record AdetIstegi(int AdisyonId, int SatirId, int Adet);
public record OdemeIstegi(int AdisyonId, int? OdemeTuruId);   // platform siparişinde boş
public record IptalIstegi(int AdisyonId, string? Neden);
public record KisiIstegi(int AdisyonId, int Kisi);
// Deger 0 = indirimi kaldır
public record IndirimIstegi(int AdisyonId, bool YuzdeMi, decimal Deger, string? Neden);

// Kurallara uymayan istek: mesajı kullanıcıya aynen gösterilir
public class AdisyonHatasi(string mesaj) : Exception(mesaj);
