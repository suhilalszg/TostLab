namespace TostLab.Models;

// Açık adisyondan çıkarılan ürünün izi (adet azaltma ya da satırı silme).
// Neden var: ürün hazırlandıktan sonra adisyondan silinirse yönetici bunu Kayıtlar'da görsün.
// SiparisId'ye bilerek ilişki (foreign key) koymadık: adisyondaki son ürün silinince
// adisyonun kendisi de silinir, ama bu iz kalmalı.
public class SilinenSatir
{
    public int Id { get; set; }

    public int SiparisId { get; set; }

    // Adisyon silinmiş olsa da okunabilsin diye yazı olarak saklanır: "İç 2", "Getir 14:05"
    public string AdisyonBasligi { get; set; } = "";

    public int UrunId { get; set; }
    public string UrunAdi { get; set; } = "";

    // Kaç tane çıkarıldı (3'ten 1'e indiyse 2)
    public int Adet { get; set; }
    public decimal BirimFiyat { get; set; }

    public DateTime Saat { get; set; }

    public int KullaniciId { get; set; }
    public Kullanici Kullanici { get; set; } = null!;

    public decimal Tutar => Adet * BirimFiyat;
}
