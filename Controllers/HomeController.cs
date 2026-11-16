using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TostLab.Models;
using TostLab.Services;

namespace TostLab.Controllers;

public class HomeController : Controller
{
    private readonly RaporServisi raporServisi;

    public HomeController(RaporServisi raporServisi)
    {
        this.raporServisi = raporServisi;
    }

    // Bugün (pano): herkes açar. Para bilgisi sadece yöneticiye çizilir; bkz. Views/Home/Index
    // ?aralik=dun / ?aralik=hafta ile dün ya da bu hafta görülür.
    public async Task<IActionResult> Index(PanoAraligi aralik = PanoAraligi.Bugun) =>
        View(await raporServisi.PanoAsync(aralik));

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
