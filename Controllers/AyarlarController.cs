using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Controllers;

// Ayarlar: masalar ve ödeme türleri. Sadece yönetici.
// Kayıtlar silinmez (eski siparişler onlara bağlı), sadece kapatılır.
[Authorize(Roles = Roller.Yonetici)]
public class AyarlarController : Controller
{
    private const string MasaListesi = "masa";
    private const string OdemeListesi = "odeme";

    private readonly TostLabDbContext veritabani;

    public AyarlarController(TostLabDbContext veritabani)
    {
        this.veritabani = veritabani;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new AyarlarSayfasi
        {
            Masalar = await veritabani.Masalar.OrderBy(masa => masa.Id).ToListAsync(),
            OdemeTurleri = await veritabani.OdemeTurleri.OrderBy(odeme => odeme.Id).ToListAsync()
        };
        return View(model);
    }

    // Yeni masa ya da ödeme türü: /Ayarlar/Ekle  (liste=masa | odeme)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(string liste, string? ad)
    {
        var kayitlar = await ListeyiGetirAsync(liste);
        if (kayitlar == null)
        {
            return NotFound();
        }

        string? hata = AdHatasi(ad, kayitlar, haricId: 0);
        if (hata != null)
        {
            return BadRequest(new { hata });
        }

        IAdliKayit yeniKayit = liste == MasaListesi
            ? veritabani.Masalar.Add(new Masa { Ad = ad!.Trim() }).Entity
            : veritabani.OdemeTurleri.Add(new OdemeTuru { Ad = ad!.Trim() }).Entity;
        await veritabani.SaveChangesAsync();

        return Json(new { id = yeniKayit.Id, ad = yeniKayit.Ad });
    }

    // Adı değiştir (yazıp Enter'a basınca)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdDegistir(string liste, int id, string? ad)
    {
        var kayitlar = await ListeyiGetirAsync(liste);
        var kayit = kayitlar?.FirstOrDefault(oge => oge.Id == id);
        if (kayit == null)
        {
            return NotFound();
        }

        string? hata = AdHatasi(ad, kayitlar!, haricId: id);
        if (hata != null)
        {
            return BadRequest(new { hata, ad = kayit.Ad });
        }

        kayit.Ad = ad!.Trim();
        await veritabani.SaveChangesAsync();
        return Json(new { ad = kayit.Ad });
    }

    // Aç / kapat anahtarı
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Durum(string liste, int id)
    {
        var kayitlar = await ListeyiGetirAsync(liste);
        var kayit = kayitlar?.FirstOrDefault(oge => oge.Id == id);
        if (kayit == null)
        {
            return NotFound();
        }

        // En az bir ödeme türü açık kalmalı, yoksa sipariş kapatılamaz
        if (kayit.AktifMi && liste == OdemeListesi && kayitlar!.Count(oge => oge.AktifMi) == 1)
        {
            return BadRequest(new { hata = "En az bir ödeme türü açık kalmalı." });
        }

        kayit.AktifMi = !kayit.AktifMi;
        await veritabani.SaveChangesAsync();
        return Json(new { aktifMi = kayit.AktifMi });
    }

    // İki tablo küçük (birkaç satır), o yüzden hepsini getirip bellekte çalışıyoruz
    private async Task<List<IAdliKayit>?> ListeyiGetirAsync(string liste)
    {
        if (liste == MasaListesi)
        {
            return (await veritabani.Masalar.ToListAsync()).Cast<IAdliKayit>().ToList();
        }
        if (liste == OdemeListesi)
        {
            return (await veritabani.OdemeTurleri.ToListAsync()).Cast<IAdliKayit>().ToList();
        }
        return null;
    }

    private static string? AdHatasi(string? ad, List<IAdliKayit> kayitlar, int haricId)
    {
        if (string.IsNullOrWhiteSpace(ad))
        {
            return "Ad boş olamaz.";
        }
        if (ad.Trim().Length > 40)
        {
            return "Ad en fazla 40 karakter olabilir.";
        }
        bool ayniAdVar = kayitlar.Any(oge => oge.Id != haricId &&
            string.Equals(oge.Ad, ad.Trim(), StringComparison.CurrentCultureIgnoreCase));
        return ayniAdVar ? "Bu ad zaten var." : null;
    }
}
