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
