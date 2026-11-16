namespace TostLab.Models;

// Hangi ayın raporu ne zaman e-postayla gitti.
// Zamanlayıcı buna bakar: geçen ayın otomatik raporu gittiyse bir daha göndermez.
public class GonderilenRapor
{
    public int Id { get; set; }

    public int Yil { get; set; }
    public int Ay { get; set; }

    public DateTime GonderimSaati { get; set; }

    // true: yönetici Aylık sayfasındaki düğmeyle gönderdi (ay bitmeden de olabilir)
    // false: ay bitince zamanlayıcı kendisi gönderdi
    public bool ElleMi { get; set; }

    public string Alici { get; set; } = "";
}
