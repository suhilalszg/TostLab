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
    private readonly RaporGondermeServisi raporGondermeServisi;

    public GunSonuController(RaporServisi raporServisi, RaporGondermeServisi raporGondermeServisi)
    {
        this.raporServisi = raporServisi;
        this.raporGondermeServisi = raporGondermeServisi;
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
        ViewData["EpostaHazirMi"] = raporGondermeServisi.EpostaHazirMi;
        ViewData["SonGonderim"] = await raporGondermeServisi.SonGonderimAsync(secilenYil, secilenAy);
        ViewData["Mesaj"] = TempData["Mesaj"];
        return View(rapor);
    }

    // POST /GunSonu/AyGonder : aylık raporu şimdi e-postayla gönder
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AyGonder(int yil, int ay)
    {
        if (ay < 1 || ay > 12 || yil < 2000 || yil > 2100)
        {
            return BadRequest();
        }
        try
        {
            await raporGondermeServisi.ElleGonderAsync(yil, ay);
            TempData["Mesaj"] = "Rapor e-postayla gönderildi.";
        }
        catch (EpostaHatasi hata)
        {
            TempData["Mesaj"] = hata.Message;
        }
        return RedirectToAction(nameof(Ay), new { yil, ay });
    }
}
