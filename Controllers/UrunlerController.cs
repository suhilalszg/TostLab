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

    // GET /Urunler?kategori=Klasikler&arama=kızıl
    [HttpGet]
    public async Task<IActionResult> Index(string? kategori, string? arama)
    {
        var tumUrunler = await veritabani.Urunler.OrderBy(urun => urun.Id).ToListAsync();

        // Kategorileri menüdeki sırasıyla (ilk eklenme sırası) çıkar
        var kategoriler = tumUrunler.Select(urun => urun.Kategori).Distinct().ToList();

        var gosterilecekler = tumUrunler.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(kategori))
        {
            gosterilecekler = gosterilecekler.Where(urun => urun.Kategori == kategori);
        }
        if (!string.IsNullOrWhiteSpace(arama))
        {
            string aranan = arama.Trim();
            gosterilecekler = gosterilecekler.Where(urun =>
                urun.Ad.Contains(aranan, StringComparison.CurrentCultureIgnoreCase) ||
                (urun.Icindekiler ?? "").Contains(aranan, StringComparison.CurrentCultureIgnoreCase));
        }

        var model = new MenuSayfasi
        {
            Kategoriler = kategoriler,
            SeciliKategori = kategori,
            Arama = arama,
            Urunler = gosterilecekler.ToList(),
            ToplamUrun = tumUrunler.Count,
            SatistakiUrun = tumUrunler.Count(urun => urun.SatistaMi),
            FiyatsizUrun = tumUrunler.Count(urun => urun.Fiyat == 0)
        };
        return View(model);
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
        return RedirectToAction("Index", new { kategori = urun.Kategori });
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
        return RedirectToAction("Index", new { kategori = urun.Kategori });
    }

    // Listedeki anahtar: satışa aç / kapat
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SatisDurumu(int id, string? kategori, string? arama)
    {
        var urun = await veritabani.Urunler.FindAsync(id);
        if (urun != null)
        {
            urun.SatistaMi = !urun.SatistaMi;
            await veritabani.SaveChangesAsync();
        }
        return RedirectToAction("Index", new { kategori, arama });
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
        return RedirectToAction("Index", new { kategori = urun.Kategori });
    }

    private async Task<List<string>> KategorileriGetirAsync()
    {
        var kategoriler = await veritabani.Urunler
            .OrderBy(urun => urun.Id)
            .Select(urun => urun.Kategori)
            .ToListAsync();
        return kategoriler.Distinct().ToList();
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
