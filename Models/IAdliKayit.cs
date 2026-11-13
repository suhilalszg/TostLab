namespace TostLab.Models;

// Masa ve OdemeTuru aynı üç alana sahip: Id, Ad, AktifMi.
// Bu ortak "sözleşme" (interface) sayesinde Ayarlar sayfası ikisini aynı kodla yönetir.
public interface IAdliKayit
{
    int Id { get; }
    string Ad { get; set; }
    bool AktifMi { get; set; }
}
