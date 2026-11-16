using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TostLab.Models;
using TostLab.Services;

namespace TostLab.Controllers;

// Adisyon paneli: gün / hafta / ay seç; kazançlar, grafikler, raporlar ve bütün adisyonlar tek sayfada.
// Birine tıkla, fişini gör. Sadece yönetici (para bilgisi var). Sorgular RaporServisi'nde.
[Authorize(Roles = Roller.Yonetici)]
public class AdisyonController : Controller
{
    private readonly RaporServisi raporServisi;

    public AdisyonController(RaporServisi raporServisi)
    {
        this.raporServisi = raporServisi;
    }

    // GET /Adisyon?donem=Hafta&tarih=2026-11-16&durum=Iptal
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PanelFiltresi filtre) =>
        View(await raporServisi.AdisyonPaneliAsync(filtre));

    // GET /Adisyon/Detay/12
    [HttpGet]
    public async Task<IActionResult> Detay(int id)
    {
        var detay = await raporServisi.AdisyonDetayiAsync(id);
        return detay == null ? NotFound() : View(detay);
    }
}

// Eski "Kayıtlar" adresleri (yer imleri, e-postalar) Adisyon paneline yönlenir.
[Authorize(Roles = Roller.Yonetici)]
public class KayitlarController : Controller
{
    // /Kayitlar?gun=2026-11-16 -> /Adisyon?tarih=2026-11-16
    [HttpGet]
    public IActionResult Index(DateTime? gun) =>
        RedirectToActionPermanent("Index", "Adisyon", new { tarih = gun?.ToString("yyyy-MM-dd") });

    [HttpGet]
    public IActionResult Detay(int id) => RedirectToActionPermanent("Detay", "Adisyon", new { id });
}
