namespace TostLab.Models;

// Bir adisyonun (siparişin) yaşam döngüsü.
// Masa ekranındaki renkler buradan gelir:
//   masada Acik adisyon varsa  -> pembe (dolu)
//   masada açık adisyon yoksa  -> mor (boş)
// Not: 1 numarası eskiden "HesapIstendi" idi; kaldırıldı, numaralar kaymasın diye boş bırakıldı.
public enum AdisyonDurumu
{
    Acik = 0,          // ürün ekleniyor
    Odendi = 2,        // kapandı, rapora girer
    Iptal = 3,         // iptal edildi (nedeni yazılı), rapora ciro olarak girmez
    Iade = 4           // ödendikten sonra para geri verildi (nedeni yazılı), ciroya girmez
}
