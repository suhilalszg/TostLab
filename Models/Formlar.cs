using System.ComponentModel.DataAnnotations;

namespace TostLab.Models;

// Formlar: ekrandan gelen bilgiyi taşır. Veritabanına doğrudan yazılmaz.

public class GirisFormu
{
    [Required(ErrorMessage = "Kullanıcı adı boş olamaz.")]
    [Display(Name = "Kullanıcı adı")]
    public string KullaniciAdi { get; set; } = "";

    [Required(ErrorMessage = "Şifre boş olamaz.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Sifre { get; set; } = "";
}

public class KullaniciFormu
{
    [Required(ErrorMessage = "Kullanıcı adı boş olamaz.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3-30 karakter olmalı.")]
    [Display(Name = "Kullanıcı adı")]
    public string KullaniciAdi { get; set; } = "";

    [Required(ErrorMessage = "Şifre boş olamaz.")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalı.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Sifre { get; set; } = "";

    [Required(ErrorMessage = "Şifre tekrarı boş olamaz.")]
    [Compare(nameof(Sifre), ErrorMessage = "Şifreler aynı değil.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre (tekrar)")]
    public string SifreTekrar { get; set; } = "";

    [Display(Name = "Rol")]
    public string Rol { get; set; } = Roller.Calisan;
}

public class UrunFormu
{
    public int Id { get; set; }   // 0 ise yeni ürün

    [Required(ErrorMessage = "Ürün adı boş olamaz.")]
    [StringLength(60, ErrorMessage = "Ürün adı en fazla 60 karakter olabilir.")]
    [Display(Name = "Ürün adı")]
    public string Ad { get; set; } = "";

    [Range(0, 100000, ErrorMessage = "Fiyat 0 ile 100.000 ₺ arasında olmalı.")]
    [Display(Name = "Fiyat (₺)")]
    public decimal Fiyat { get; set; }

    [Required(ErrorMessage = "Kategori boş olamaz.")]
    [StringLength(40, ErrorMessage = "Kategori en fazla 40 karakter olabilir.")]
    [Display(Name = "Kategori")]
    public string Kategori { get; set; } = "";

    [StringLength(300, ErrorMessage = "İçindekiler en fazla 300 karakter olabilir.")]
    [Display(Name = "İçindekiler")]
    public string? Icindekiler { get; set; }

    [Display(Name = "Satışta")]
    public bool SatistaMi { get; set; } = true;
}

// Menü sayfasının ekrana gönderdiği her şey tek pakette
public class MenuSayfasi
{
    public List<Urun> Urunler { get; set; } = new();

    // Fiyat kutusunda görünen yazı: 0 -> boş, 90 -> "90", 100,5 -> "100,50"
    public static string FiyatYazisi(decimal fiyat)
    {
        if (fiyat == 0) return "";
        return fiyat % 1 == 0 ? fiyat.ToString("0") : fiyat.ToString("0.00");
    }
}

// Ayarlar sayfasının ekrana gönderdiği iki liste
public class AyarlarSayfasi
{
    public List<Masa> Masalar { get; set; } = new();
    public List<OdemeTuru> OdemeTurleri { get; set; } = new();
}
