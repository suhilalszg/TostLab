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

    // Bugün: herkes açar. Para bilgisi (ciro, ödeme türleri) sadece yöneticiye çizilir; bkz. Views/Home/Index
    public async Task<IActionResult> Index() =>
        View(await raporServisi.GunRaporuAsync(DateTime.Today));

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
