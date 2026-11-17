namespace TostLab.Models;

// Kasadan çıkan para: tedarikçi, fatura, avans... Adisyon panelinde girilir, raporda gider olarak düşer.
public class KasaHareketi
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public decimal Tutar { get; set; }

    // "Tedarikçi", "Fatura", "Avans", "Diğer" (bkz. GiderTurleri)
    public string Tur { get; set; } = "";
    public string? Aciklama { get; set; }

    public int KullaniciId { get; set; }
    public Kullanici Kullanici { get; set; } = null!;
}

public static class GiderTurleri
{
    public static readonly string[] Hepsi = { "Tedarikçi", "Fatura", "Avans", "Diğer" };
}

// Gün sonu kasa sayımı: sabah kasadaki para + akşam sayılan para.
// Sistemdeki nakit = açılış + nakit satış - kasa çıkışları; fark = sayılan - sistem (eksi: açık, artı: fazla)
public class KasaSayimi
{
    public int Id { get; set; }
    public DateTime Gun { get; set; }              // sadece tarih (günde bir sayım)
    public decimal AcilisNakdi { get; set; }
    public decimal? SayilanNakit { get; set; }    // sabah sadece açılış girilebilir
    public DateTime Saat { get; set; }             // son kaydedildiği an

    public int KullaniciId { get; set; }
    public Kullanici Kullanici { get; set; } = null!;
}

// Ayın ciro hedefi (ibre grafiği bununla oranlanır)
public class AylikHedef
{
    public int Id { get; set; }
    public int Yil { get; set; }
    public int Ay { get; set; }
    public decimal Tutar { get; set; }
}
