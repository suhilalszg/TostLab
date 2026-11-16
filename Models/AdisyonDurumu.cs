namespace TostLab.Models;

// Bir adisyonun (siparişin) yaşam döngüsü.
// Masa ekranındaki renkler buradan gelir:
//   masada Acik adisyon varsa       -> kırmızı (dolu)
//   masada HesapIstendi varsa       -> sarı
//   masada açık adisyon yoksa       -> yeşil (boş)
public enum AdisyonDurumu
{
    Acik = 0,          // ürün ekleniyor
    HesapIstendi = 1,  // müşteri hesabı istedi, ödeme bekleniyor
    Odendi = 2,        // kapandı, rapora girer
    Iptal = 3          // iptal edildi (nedeni yazılı), rapora ciro olarak girmez
}
