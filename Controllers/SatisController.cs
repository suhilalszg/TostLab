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
            .Select(urun => new
            {
                id = urun.Id, ad = urun.Ad, fiyat = urun.Fiyat, kategori = urun.Kategori,
                foto = urun.FotografAdi == null ? null : UrunFotograflari.Adres + "/" + urun.FotografAdi
            })
            .ToListAsync();
        var odemeTurleri = await veritabani.OdemeTurleri
            .Where(odeme => odeme.AktifMi)
            .OrderBy(odeme => odeme.Id)
            .Select(odeme => new { id = odeme.Id, ad = odeme.Ad })
            .ToListAsync();

        ViewData["Urunler"] = urunler;
        ViewData["OdemeTurleri"] = odemeTurleri;
        ViewData["Ekran"] = await adisyonServisi.EkranDurumuAsync();
        ViewData["Platformlar"] = Kaynaklar.Platformlar;
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
        Calistir(async () => await adisyonServisi.UrunEkleAsync(istek, KullaniciId()));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Ikram([FromBody] IkramIstegi istek) =>
        Calistir(async () => await adisyonServisi.IkramAsync(istek, KullaniciId()));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Tasi([FromBody] TasiIstegi istek) =>
        Calistir(async () => await adisyonServisi.TasiAsync(istek));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Bol([FromBody] BolIstegi istek) =>
        Calistir(() => adisyonServisi.BolOdeAsync(istek));

    // İade penceresi açılınca: bugün ödenenler
    [HttpGet]
    public async Task<IActionResult> Odenenler() => Json(await adisyonServisi.BugunOdenenlerAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Iade([FromBody] IadeIstegi istek) =>
        Calistir(async () => { await adisyonServisi.IadeEtAsync(istek, KullaniciId()); return null; });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Adet([FromBody] AdetIstegi istek) =>
        Calistir(() => adisyonServisi.AdetDegistirAsync(istek, KullaniciId()));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Kisi([FromBody] KisiIstegi istek) =>
        Calistir(async () => await adisyonServisi.KisiDegistirAsync(istek));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Indirim([FromBody] IndirimIstegi istek) =>
        Calistir(async () => await adisyonServisi.IndirimUygulaAsync(istek, KullaniciId()));

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
