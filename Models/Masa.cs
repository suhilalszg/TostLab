namespace TostLab.Models;

public class Masa : IAdliKayit
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public bool AktifMi { get; set; } = true;
}