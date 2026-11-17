using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TostLab.Models;
using TostLab.Services;

namespace TostLab.Controllers;

// Fişler: ödenen adisyonların fişleri günlük / aylık. Sadece yönetici (tutar var).
// Fiş silmek satışı silmez, sadece fiş kayıtlarından kaldırır.
[Authorize(Roles = Roller.Yonetici)]
public class FislerController : Controller
{
    private readonly FisServisi fisServisi;

    public FislerController(FisServisi fisServisi)
    {
        this.fisServisi = fisServisi;
    }

    // GET /Fisler?donem=Ay&tarih=2026-11-01&odeme=Nakit
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FisFiltresi filtre) =>
        View(await fisServisi.ListeAsync(filtre));

    // POST /Fisler/Sil
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id, FisDonemi donem, DateTime? tarih, string? odeme, int sayfa = 1)
    {
        int kullaniciId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        TempData["Mesaj"] = await fisServisi.SilAsync(id, kullaniciId)
            ? $"Fiş #{id} kayıtlardan silindi. Satış ve raporlar etkilenmedi."
            : "Fiş bulunamadı (daha önce silinmiş olabilir).";
        return RedirectToAction(nameof(Index), new { donem, tarih = tarih?.ToString("yyyy-MM-dd"), odeme, sayfa = sayfa > 1 ? sayfa : (int?)null });
    }
}
