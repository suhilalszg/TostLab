using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Controllers;

// Sipariş girişi: yönetici de çalışan da kullanır (sadece giriş yapmış olmak yeter)
public class SiparisController : Controller
{
    private readonly TostLabDbContext veritabani;

    public SiparisController(TostLabDbContext veritabani)
    {
        this.veritabani = veritabani;
    }

    // GET /Siparis : sipariş ekranı
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new SiparisSayfasi
        {
            Urunler = await veritabani.Urunler.Where(urun => urun.SatistaMi).OrderBy(urun => urun.Id).ToListAsync(),
            Masalar = await veritabani.Masalar.Where(masa => masa.AktifMi).OrderBy(masa => masa.Id).ToListAsync(),
            OdemeTurleri = await veritabani.OdemeTurleri.Where(odeme => odeme.AktifMi).OrderBy(odeme => odeme.Id).ToListAsync()
        };
        return View(model);
    }

    // POST /Siparis/Kaydet : tarayıcı fişi JSON olarak gönderir
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Kaydet([FromBody] SiparisIstegi istek)
    {
        // ---- 1) Gelen bilgiyi kontrol et ----
        if (!Kaynaklar.Hepsi.Contains(istek.Kaynak))
        {
            return Hata("Siparişin nereden geldiğini seç.");
        }

        if (istek.Kaynak == Kaynaklar.Masa)
        {
            bool masaVar = await veritabani.Masalar.AnyAsync(masa => masa.Id == istek.MasaId && masa.AktifMi);
            if (!masaVar)
            {
                return Hata("Masa seç.");
            }
        }
        else
        {
            istek.MasaId = null;   // masa dışı siparişte masa olmaz
        }

        bool odemeVar = await veritabani.OdemeTurleri.AnyAsync(odeme => odeme.Id == istek.OdemeTuruId && odeme.AktifMi);
        if (!odemeVar)
        {
            return Hata("Ödeme türünü seç.");
        }

        // Aynı ürün iki satırda geldiyse birleştir, adeti 0 olanları at
        var adetler = istek.Satirlar
            .Where(satir => satir.Adet > 0)
            .GroupBy(satir => satir.UrunId)
            .ToDictionary(grup => grup.Key, grup => grup.Sum(satir => satir.Adet));

        if (adetler.Count == 0)
        {
            return Hata("Fişe en az bir ürün ekle.");
        }
        if (adetler.Values.Any(adet => adet > 99))
        {
            return Hata("Bir üründen en fazla 99 adet girilebilir.");
        }

        // ---- 2) Fiyatları tarayıcıdan değil veritabanından al ----
        var urunKimlikleri = adetler.Keys.ToList();
        var urunler = await veritabani.Urunler
            .Where(urun => urunKimlikleri.Contains(urun.Id) && urun.SatistaMi)
            .ToListAsync();

        if (urunler.Count != adetler.Count)
        {
            return Hata("Fişteki bir ürün satıştan kaldırılmış. Sayfayı yenile.");
        }
        var fiyatsizUrun = urunler.FirstOrDefault(urun => urun.Fiyat <= 0);
        if (fiyatsizUrun != null)
        {
            return Hata($"{fiyatsizUrun.Ad} ürününün fiyatı girilmemiş.");
        }

        // ---- 3) Siparişi oluştur ve kaydet ----
        var siparis = new Siparis
        {
            Tarih = DateTime.Now,
            Kaynak = istek.Kaynak,
            MasaId = istek.MasaId,
            OdemeTuruId = istek.OdemeTuruId,
            Satirlar = urunler.Select(urun => new SiparisSatiri
            {
                UrunId = urun.Id,
                Adet = adetler[urun.Id],
                BirimFiyat = urun.Fiyat   // o anki fiyat sabitlenir
            }).ToList()
        };
        siparis.Toplam = siparis.Satirlar.Sum(satir => satir.Tutar);

        veritabani.Siparisler.Add(siparis);
        await veritabani.SaveChangesAsync();

        return Json(new { id = siparis.Id, toplam = siparis.Toplam.ToString("N2") });
    }

    private BadRequestObjectResult Hata(string mesaj) => BadRequest(new { hata = mesaj });
}
