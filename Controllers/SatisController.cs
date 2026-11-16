using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;
using TostLab.Services;

namespace TostLab.Controllers;

// Satış ekranı (POS). Sayfa bir kez açılır; sonrası hep küçük JSON istekleri.
// Yönetici de çalışan da kullanır.
public class SatisController : Controller
{
    private readonly AdisyonServisi adisyonServisi;
    private readonly TostLabDbContext veritabani;

    public SatisController(AdisyonServisi adisyonServisi, TostLabDbContext veritabani)
    {
        this.adisyonServisi = adisyonServisi;
        this.veritabani = veritabani;
    }

    // GET /Satis : ekranın iskeleti + menü + ödeme türleri (bunlar gün içinde değişmez)
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var urunler = await veritabani.Urunler
            .Where(urun => urun.SatistaMi)
            .OrderBy(urun => urun.Id)
            .Select(urun => new { id = urun.Id, ad = urun.Ad, fiyat = urun.Fiyat, kategori = urun.Kategori })
            .ToListAsync();
        var odemeTurleri = await veritabani.OdemeTurleri
            .Where(odeme => odeme.AktifMi)
            .OrderBy(odeme => odeme.Id)
            .Select(odeme => new { id = odeme.Id, ad = odeme.Ad })
            .ToListAsync();

        ViewData["Urunler"] = urunler;
        ViewData["OdemeTurleri"] = odemeTurleri;
        ViewData["Ekran"] = await adisyonServisi.EkranDurumuAsync();
        return View();
    }

    // Masa planı (renkler): ekran 15 saniyede bir ve her işlemden sonra yeniler
    [HttpGet]
    public async Task<IActionResult> Durum() => Json(await adisyonServisi.EkranDurumuAsync());

    [HttpGet]
    public async Task<IActionResult> Adisyon(int id)
    {
        var adisyon = await adisyonServisi.AdisyonAsync(id);
        return adisyon == null ? NotFound(new { hata = "Bu adisyon kapanmış." }) : Json(adisyon);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> UrunEkle([FromBody] UrunEkleIstegi istek) =>
        Calistir(async () => await adisyonServisi.UrunEkleAsync(istek));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Adet([FromBody] AdetIstegi istek) =>
        Calistir(() => adisyonServisi.AdetDegistirAsync(istek));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Hesap([FromBody] HesapIstegi istek) =>
        Calistir(async () => await adisyonServisi.HesapIsteAsync(istek));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Ode([FromBody] OdemeIstegi istek) =>
        Calistir(async () => { await adisyonServisi.OdeAsync(istek); return null; });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Iptal([FromBody] IptalIstegi istek) =>
        Calistir(async () => { await adisyonServisi.IptalEtAsync(istek, KullaniciId()); return null; });

    // Her işlem aynı şekilde biter: güncel adisyon + güncel masa planı tek cevapta döner.
    // Kural hatası olursa 400 ve Türkçe mesaj döner.
    private async Task<IActionResult> Calistir(Func<Task<AdisyonBilgisi?>> islem)
    {
        try
        {
            var adisyon = await islem();
            return Json(new IslemSonucu(adisyon, await adisyonServisi.EkranDurumuAsync()));
        }
        catch (AdisyonHatasi hata)
        {
            return BadRequest(new { hata = hata.Message, ekran = await adisyonServisi.EkranDurumuAsync() });
        }
    }

    private int KullaniciId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
