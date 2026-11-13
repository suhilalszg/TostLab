namespace TostLab.Models;

public class Kullanici
{
    public int Id { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string SifreHash { get; set; } = "";
    public string Rol { get; set; } = "Calisan";
}
