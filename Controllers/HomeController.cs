using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TostLab.Models;

namespace TostLab.Controllers;

public class HomeController : Controller
{
    // Ana sayfa artık Satış ekranı (Bugün sayfası kaldırıldı; özet Adisyon panelinde)
    public IActionResult Index() => RedirectToAction("Index", "Satis");

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
