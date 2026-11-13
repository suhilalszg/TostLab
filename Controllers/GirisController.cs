using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;
using TostLab.Services;

namespace TostLab.Controllers;

[AllowAnonymous]
public class GirisController : Controller
{
    private readonly TostLabDbContext veritabani;
    private readonly IPasswordHasher<Kullanici> sifreleyici;

    public GirisController(TostLabDbContext veritabani, IPasswordHasher<Kullanici> sifreleyici)
    {
        this.veritabani = veritabani;
        this.sifreleyici = sifreleyici;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? donusAdresi)
    {
        // Hiç kullanıcı yoksa program yeni kurulmuştur: önce yönetici hesabı açılmalı
        if (!await veritabani.Kullanicilar.AnyAsync())
        {
            return RedirectToAction("Index", "Kurulum");
        }

        ViewData["DonusAdresi"] = donusAdresi;
        return View(new GirisFormu());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(GirisFormu form, string? donusAdresi)
    {
        ViewData["DonusAdresi"] = donusAdresi;

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var kullanici = await veritabani.Kullanicilar
            .FirstOrDefaultAsync(kayit => kayit.KullaniciAdi == form.KullaniciAdi);

        bool sifreDogru = kullanici != null &&
            sifreleyici.VerifyHashedPassword(kullanici, kullanici.SifreHash, form.Sifre) != PasswordVerificationResult.Failed;

        if (kullanici == null || !sifreDogru)
        {
            // Hangisinin yanlış olduğunu söylemiyoruz: güvenlik için
            ModelState.AddModelError("", "Kullanıcı adı veya şifre hatalı.");
            return View(form);
        }

        await OturumServisi.OturumAcAsync(HttpContext, kullanici);

        if (Url.IsLocalUrl(donusAdresi))
        {
            return LocalRedirect(donusAdresi);
        }
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cikis()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult Yetkisiz()
    {
        return View();
    }
}
