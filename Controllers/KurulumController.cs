using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;
using TostLab.Services;

namespace TostLab.Controllers;

// İlk kurulum: veritabanında hiç kullanıcı yokken yönetici hesabını açar.
// Bir kullanıcı oluştuktan sonra bu sayfa kapanır.
[AllowAnonymous]
public class KurulumController : Controller
{
    private readonly TostLabDbContext veritabani;
    private readonly IPasswordHasher<Kullanici> sifreleyici;

    public KurulumController(TostLabDbContext veritabani, IPasswordHasher<Kullanici> sifreleyici)
    {
        this.veritabani = veritabani;
        this.sifreleyici = sifreleyici;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (await veritabani.Kullanicilar.AnyAsync())
        {
            return RedirectToAction("Index", "Giris");
        }
        return View(new KullaniciFormu { Rol = Roller.Yonetici });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(KullaniciFormu form)
    {
        if (await veritabani.Kullanicilar.AnyAsync())
        {
            return RedirectToAction("Index", "Giris");
        }

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var yonetici = new Kullanici
        {
            KullaniciAdi = form.KullaniciAdi.Trim(),
            Rol = Roller.Yonetici   // formdan ne gelirse gelsin ilk hesap yönetici olur
        };
        yonetici.SifreHash = sifreleyici.HashPassword(yonetici, form.Sifre);

        veritabani.Kullanicilar.Add(yonetici);
        await veritabani.SaveChangesAsync();

        await OturumServisi.OturumAcAsync(HttpContext, yonetici);
        return RedirectToAction("Index", "Home");
    }
}
