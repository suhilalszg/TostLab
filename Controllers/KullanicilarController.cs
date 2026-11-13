using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Controllers;

// Bu sayfaları sadece yönetici açabilir
[Authorize(Roles = Roller.Yonetici)]
public class KullanicilarController : Controller
{
    private readonly TostLabDbContext veritabani;
    private readonly IPasswordHasher<Kullanici> sifreleyici;

    public KullanicilarController(TostLabDbContext veritabani, IPasswordHasher<Kullanici> sifreleyici)
    {
        this.veritabani = veritabani;
        this.sifreleyici = sifreleyici;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var kullanicilar = await veritabani.Kullanicilar
            .OrderBy(kullanici => kullanici.KullaniciAdi)
            .ToListAsync();
        return View(kullanicilar);
    }

    [HttpGet]
    public IActionResult Ekle()
    {
        return View(new KullaniciFormu());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(KullaniciFormu form)
    {
        if (form.Rol != Roller.Yonetici && form.Rol != Roller.Calisan)
        {
            ModelState.AddModelError(nameof(form.Rol), "Geçersiz rol.");
        }

        string kullaniciAdi = form.KullaniciAdi.Trim();
        if (await veritabani.Kullanicilar.AnyAsync(kayit => kayit.KullaniciAdi == kullaniciAdi))
        {
            ModelState.AddModelError(nameof(form.KullaniciAdi), "Bu kullanıcı adı zaten var.");
        }

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var yeniKullanici = new Kullanici { KullaniciAdi = kullaniciAdi, Rol = form.Rol };
        yeniKullanici.SifreHash = sifreleyici.HashPassword(yeniKullanici, form.Sifre);

        veritabani.Kullanicilar.Add(yeniKullanici);
        await veritabani.SaveChangesAsync();

        TempData["Bildirim"] = $"{yeniKullanici.KullaniciAdi} eklendi.";
        return RedirectToAction("Index");
    }
}
