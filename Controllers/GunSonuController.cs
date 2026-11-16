using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TostLab.Models;
using TostLab.Services;

namespace TostLab.Controllers;

// Gün sonu ve aylık özet. Sadece yönetici.
// Hesaplar RaporServisi'nde (Bugün sayfasıyla aynı GunRaporuAsync), metin RaporMetni'nde.
[Authorize(Roles = Roller.Yonetici)]
public class GunSonuController : Controller
{
    private readonly RaporServisi raporServisi;

    public GunSonuController(RaporServisi raporServisi)
    {
        this.raporServisi = raporServisi;
    }

    // GET /GunSonu?gun=2026-11-16  (gün yoksa bugün)
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? gun)
    {
        var rapor = await raporServisi.GunRaporuAsync((gun ?? DateTime.Today).Date);
        ViewData["WhatsappMetni"] = RaporMetni.GunSonu(rapor);
        return View(rapor);
    }

    // GET /GunSonu/Ay?yil=2026&ay=11  (yoksa bu ay)
    [HttpGet]
    public async Task<IActionResult> Ay(int? yil, int? ay)
    {
        int secilenYil = yil ?? DateTime.Today.Year;
        int secilenAy = ay ?? DateTime.Today.Month;
        if (secilenAy < 1 || secilenAy > 12 || secilenYil < 2000 || secilenYil > 2100)
        {
            return BadRequest();
        }

        var rapor = await raporServisi.AyRaporuAsync(secilenYil, secilenAy);
        ViewData["WhatsappMetni"] = RaporMetni.Aylik(rapor);
        return View(rapor);
    }
}
