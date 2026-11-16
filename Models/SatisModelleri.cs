namespace TostLab.Models;

// Satış ekranı ile sunucu arasında gidip gelen JSON şekilleri.
// "record": sadece veri taşıyan, kısa yazılan sınıf.

// ---------- Sunucudan ekrana ----------

public record SatirBilgisi(int Id, int UrunId, string Ad, int Adet, decimal BirimFiyat, decimal Tutar);

public record AdisyonBilgisi(
    int Id,
    string Kaynak,
    int? MasaId,
    string Baslik,          // "İç 2" ya da "Yemeksepeti #14"
    string Durum,           // "acik" | "hesap"
    DateTime Acilis,
    decimal Toplam,
    int? KisiSayisi,
    List<SatirBilgisi> Satirlar);

public record MasaBilgisi(
    int Id,
    string Ad,
    string Durum,           // "bos" (yeşil) | "dolu" (kırmızı) | "hesap" (sarı)
    int? AdisyonId,
    decimal Toplam,
    DateTime? Acilis,
    int? KisiSayisi);

public record PaketBilgisi(int AdisyonId, string Baslik, string Kaynak, string Durum, decimal Toplam, DateTime Acilis);

// Masa planının tamamı: her değişiklikten sonra ekran bunu yeniden çizer
public record EkranDurumu(List<MasaBilgisi> Masalar, List<PaketBilgisi> Paketler);

// Her işlemden sonra dönen cevap: güncel adisyon (kapandıysa null) + güncel masa planı
public record IslemSonucu(AdisyonBilgisi? Adisyon, EkranDurumu Ekran);

// ---------- Ekrandan sunucuya ----------

public record UrunEkleIstegi(int? AdisyonId, string Kaynak, int? MasaId, int UrunId, int Adet);
public record AdetIstegi(int AdisyonId, int SatirId, int Adet);
public record HesapIstegi(int AdisyonId, bool Istendi);
public record OdemeIstegi(int AdisyonId, int OdemeTuruId);
public record IptalIstegi(int AdisyonId, string? Neden);
public record KisiIstegi(int AdisyonId, int Kisi);

// Kurallara uymayan istek: mesajı kullanıcıya aynen gösterilir
public class AdisyonHatasi(string mesaj) : Exception(mesaj);
