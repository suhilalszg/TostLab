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
    public List<string> Kategoriler { get; set; } = new();
    public string? SeciliKategori { get; set; }
    public string? Arama { get; set; }
    public List<Urun> Urunler { get; set; } = new();
    public int ToplamUrun { get; set; }
    public int SatistakiUrun { get; set; }
    public int FiyatsizUrun { get; set; }
}
