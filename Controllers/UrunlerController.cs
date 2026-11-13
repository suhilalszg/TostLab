using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Controllers;

// Menü yönetimi: sadece yönetici
[Authorize(Roles = Roller.Yonetici)]
public class UrunlerController : Controller
{
    private readonly TostLabDbContext veritabani;

    public UrunlerController(TostLabDbContext veritabani)
    {
        this.veritabani = veritabani;
    }

    // GET /Urunler : tüm menü tek sayfada. Arama ve süzme tarayıcıda (menu.js) yapılır.
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tumUrunler = await veritabani.Urunler.OrderBy(urun => urun.Id).ToListAsync();

        var model = new MenuSayfasi
        {
            Urunler = tumUrunler   // view bunları kategorilere ayırır (ilk eklenme sırasıyla)
        };
        return View(model);
    }

    // Listede fiyat kutusuna yazıp Enter'a basınca buraya gelir (sayfa yenilenmez)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FiyatKaydet(int id, string? fiyat)
    {
        var urun = await veritabani.Urunler.FindAsync(id);
        if (urun == null)
        {
            return NotFound();
        }

        if (!FiyatiOku(fiyat, out decimal yeniFiyat))
        {
            return BadRequest(new { hata = "Fiyat sayı olmalı (örn. 90 veya 92,50)." });
        }

        urun.Fiyat = yeniFiyat;
        await veritabani.SaveChangesAsync();

        return Json(new { fiyat = MenuSayfasi.FiyatYazisi(urun.Fiyat) });
    }

    // Her kategorinin altındaki "hızlı ekle" satırı
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HizliEkle(string? kategori, string? ad, string? fiyat)
    {
        if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(kategori))
        {
            return BadRequest(new { hata = "Ürün adı boş olamaz." });
        }
        if (!FiyatiOku(fiyat, out decimal yeniFiyat))
        {
            return BadRequest(new { hata = "Fiyat sayı olmalı (örn. 90 veya 92,50)." });
        }

        var urun = new Urun { Ad = ad.Trim(), Kategori = kategori.Trim(), Fiyat = yeniFiyat };
        veritabani.Urunler.Add(urun);
        await veritabani.SaveChangesAsync();
        return Json(new { id = urun.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Ekle()
    {
        ViewData["Kategoriler"] = await KategorileriGetirAsync();
        return View("Form", new UrunFormu());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(UrunFormu form)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Kategoriler"] = await KategorileriGetirAsync();
            return View("Form", form);
        }

        var urun = new Urun();
        FormdanUrune(form, urun);
        veritabani.Urunler.Add(urun);
        await veritabani.SaveChangesAsync();

        TempData["Bildirim"] = $"{urun.Ad} menüye eklendi.";
        return RedirectToAction("Index", new { yeni = urun.Id });   // listede bu ürüne kay
    }

    [HttpGet]
    public async Task<IActionResult> Duzenle(int id)
    {
        var urun = await veritabani.Urunler.FindAsync(id);
        if (urun == null)
        {
            return NotFound();
        }

        ViewData["Kategoriler"] = await KategorileriGetirAsync();
        var form = new UrunFormu
        {
            Id = urun.Id,
            Ad = urun.Ad,
            Fiyat = urun.Fiyat,
            Kategori = urun.Kategori,
            Icindekiler = urun.Icindekiler,
            SatistaMi = urun.SatistaMi
        };
        return View("Form", form);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(int id, UrunFormu form)
    {
        var urun = await veritabani.Urunler.FindAsync(id);
        if (urun == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            form.Id = id;
            ViewData["Kategoriler"] = await KategorileriGetirAsync();
            return View("Form", form);
        }

        // Not: fiyat değişse de eski siparişler etkilenmez, çünkü satır kendi fiyatını saklıyor
        FormdanUrune(form, urun);
        await veritabani.SaveChangesAsync();

        TempData["Bildirim"] = $"{urun.Ad} güncellendi.";
        return RedirectToAction("Index", new { yeni = urun.Id });   // listede bu ürüne kay
    }

    // Listedeki anahtar: satışa aç / kapat (sayfa yenilenmez)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SatisDurumu(int id)
    {
        var urun = await veritabani.Urunler.FindAsync(id);
        if (urun == null)
        {
            return NotFound();
        }

        urun.SatistaMi = !urun.SatistaMi;
        await veritabani.SaveChangesAsync();

        return Json(new { satistaMi = urun.SatistaMi });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var urun = await veritabani.Urunler.FindAsync(id);
        if (urun == null)
        {
            return NotFound();
        }

        // Sipariş edilmiş ürün silinemez: geçmiş fişler bozulur. Satıştan kaldırılır.
        bool siparisiVar = await veritabani.SiparisSatirlari.AnyAsync(satir => satir.UrunId == id);
        if (siparisiVar)
        {
            urun.SatistaMi = false;
            await veritabani.SaveChangesAsync();
            TempData["Bildirim"] = $"{urun.Ad} daha önce sipariş edildiği için silinmedi, satıştan kaldırıldı.";
        }
        else
        {
            veritabani.Urunler.Remove(urun);
            await veritabani.SaveChangesAsync();
            TempData["Bildirim"] = $"{urun.Ad} menüden silindi.";
        }
        return RedirectToAction("Index");
    }

    private async Task<List<string>> KategorileriGetirAsync()
    {
        var kategoriler = await veritabani.Urunler
            .OrderBy(urun => urun.Id)
            .Select(urun => urun.Kategori)
            .ToListAsync();
        return kategoriler.Distinct().ToList();
    }

    // "92,50", "92.50", "92" hepsini kabul et; boş bırakılırsa 0 (fiyat girilmedi)
    private static bool FiyatiOku(string? yazi, out decimal fiyat)
    {
        fiyat = 0;
        if (string.IsNullOrWhiteSpace(yazi))
        {
            return true;
        }

        string temiz = yazi.Replace("₺", "").Trim().Replace('.', ',');
        return decimal.TryParse(temiz, System.Globalization.NumberStyles.Number,
                   new System.Globalization.CultureInfo("tr-TR"), out fiyat)
               && fiyat >= 0 && fiyat <= 100000;
    }

    private static void FormdanUrune(UrunFormu form, Urun urun)
    {
        urun.Ad = form.Ad.Trim();
        urun.Fiyat = form.Fiyat;
        urun.Kategori = form.Kategori.Trim();
        urun.Icindekiler = string.IsNullOrWhiteSpace(form.Icindekiler) ? null : form.Icindekiler.Trim();
        urun.SatistaMi = form.SatistaMi;
    }
}
