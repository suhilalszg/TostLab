using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Services;

// Aylık raporu hazırlar, e-postayla gönderir ve gönderildiğini kaydeder.
// Hem zamanlayıcı (ay bitince) hem Aylık sayfasındaki düğme (elle) bunu kullanır.
public class RaporGondermeServisi
{
    private readonly TostLabDbContext veritabani;
    private readonly RaporServisi raporServisi;
    private readonly EpostaServisi epostaServisi;

    public RaporGondermeServisi(TostLabDbContext veritabani, RaporServisi raporServisi, EpostaServisi epostaServisi)
    {
        this.veritabani = veritabani;
        this.raporServisi = raporServisi;
        this.epostaServisi = epostaServisi;
    }

    public bool EpostaHazirMi => epostaServisi.HazirMi;

    // Zamanlayıcı çağırır: geçen ayın raporu otomatik gitmediyse gönderir.
    // Gönderdiyse true döner. E-posta ayarı yoksa ya da o ay hiç satış yoksa sessizce geçer.
    public async Task<bool> GecenAyiGonderAsync(DateTime bugun)
    {
        var gecenAy = bugun.AddMonths(-1);
        if (!epostaServisi.HazirMi)
        {
            return false;
        }

        // Elle gönderilen sayılmaz: yönetici ay bitmeden göndermiş olabilir, o rapor eksiktir
        bool gittiMi = await veritabani.GonderilenRaporlar
            .AnyAsync(rapor => rapor.Yil == gecenAy.Year && rapor.Ay == gecenAy.Month && !rapor.ElleMi);
        if (gittiMi)
        {
            return false;
        }

        var aylik = await raporServisi.AyRaporuAsync(gecenAy.Year, gecenAy.Month);
        if (aylik.Gunler.Count == 0)
        {
            return false;   // program o ay hiç kullanılmamış (ilk kurulum gibi): boş rapor gönderme
        }

        await GonderVeKaydetAsync(aylik, elleMi: false);
        return true;
    }

    // Aylık sayfasındaki düğme
    public async Task ElleGonderAsync(int yil, int ay)
    {
        var aylik = await raporServisi.AyRaporuAsync(yil, ay);
        await GonderVeKaydetAsync(aylik, elleMi: true);
    }

    public Task<GonderilenRapor?> SonGonderimAsync(int yil, int ay) =>
        veritabani.GonderilenRaporlar.AsNoTracking()
            .Where(rapor => rapor.Yil == yil && rapor.Ay == ay)
            .OrderByDescending(rapor => rapor.Id)
            .FirstOrDefaultAsync();

    private async Task GonderVeKaydetAsync(AyRaporu aylik, bool elleMi)
    {
        // Önce gönder, sonra kaydet: gönderilemezse kayıt da olmaz, zamanlayıcı yarım saat sonra tekrar dener
        await epostaServisi.GonderAsync(RaporEpostasi.Konu(aylik), RaporEpostasi.Aylik(aylik));

        veritabani.GonderilenRaporlar.Add(new GonderilenRapor
        {
            Yil = aylik.Yil,
            Ay = aylik.Ay,
            GonderimSaati = DateTime.Now,
            ElleMi = elleMi,
            Alici = epostaServisi.Alici
        });
        await veritabani.SaveChangesAsync();
    }
}
