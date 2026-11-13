namespace TostLab.Models;

public class Urun
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public decimal Fiyat { get; set; }
    public string Kategori { get; set; } = "";
    public string? Icindekiler { get; set; }
    public bool SatistaMi { get; set; } = true;
}
