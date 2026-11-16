using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TostLab.Models;
using TostLab.Services;

namespace TostLab.Controllers;

// Geçmiş adisyonlar: gün seç, süz, birine tıkla fişini gör. Sadece yönetici (para bilgisi var).
// Controller ince: bütün sorgular RaporServisi'nde.
[Authorize(Roles = Roller.Yonetici)]
public class KayitlarController : Controller
{
    private readonly RaporServisi raporServisi;

    public KayitlarController(RaporServisi raporServisi)
    {
        this.raporServisi = raporServisi;
    }

    // GET /Kayitlar?gun=2026-11-16&durum=Iptal
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] KayitFiltresi filtre) =>
        View(await raporServisi.KayitlarAsync(filtre));

    // GET /Kayitlar/Detay/12
    [HttpGet]
    public async Task<IActionResult> Detay(int id)
    {
        var detay = await raporServisi.AdisyonDetayiAsync(id);
        return detay == null ? NotFound() : View(detay);
    }
}
