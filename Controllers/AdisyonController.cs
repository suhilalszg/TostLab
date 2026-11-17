using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;
using TostLab.Services;

namespace TostLab.Controllers;

// Adisyon paneli: gün / hafta / ay seç; kasa, fire, operasyon, kârlılık ve bütün adisyonlar tek sayfada.
// Gün sonu ekranı da buraya taşındı (kasa sayımı, giderler, aylık e-posta). Sadece yönetici.
[Authorize(Roles = Roller.Yonetici)]
public class AdisyonController : Controller
{
    private readonly RaporServisi raporServisi;
    private readonly RaporGondermeServisi raporGondermeServisi;
    private readonly TostLabDbContext veritabani;

    public AdisyonController(RaporServisi raporServisi, RaporGondermeServisi raporGondermeServisi, TostLabDbContext veritabani)
    {
        this.raporServisi = raporServisi;
        this.raporGondermeServisi = raporGondermeServisi;
        this.veritabani = veritabani;
    }

    // GET /Adisyon?donem=Hafta&tarih=2026-11-16&durum=Iptal
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PanelFiltresi filtre)
    {
        var panel = await raporServisi.AdisyonPaneliAsync(filtre);
        if (panel.Filtre.Donem == Donem.Ay)
        {
            ViewData["EpostaHazirMi"] = raporGondermeServisi.EpostaHazirMi;
            ViewData["SonGonderim"] = await raporGondermeServisi.SonGonderimAsync(panel.Filtre.Baslangic.Year, panel.Filtre.Baslangic.Month);
        }
        return View(panel);
    }

    // GET /Adisyon/Detay/12
    [HttpGet]
    public async Task<IActionResult> Detay(int id)
    {
        var detay = await raporServisi.AdisyonDetayiAsync(id);
        return detay == null ? NotFound() : View(detay);
    }

    // POST /Adisyon/KasaSayimi : günün açılış nakdi ve akşam sayılan nakit (günde bir kayıt, tekrar kaydedilirse güncellenir)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KasaSayimi(DateTime gun, string? acilisNakdi, string? sayilanNakit)
    {
        gun = gun.Date;
        decimal? acilis = TutarCoz(acilisNakdi);
        decimal? sayilan = TutarCoz(sayilanNakit);
        if (gun > DateTime.Today || acilis is < 0 || sayilan is < 0)
        {
            TempData["Mesaj"] = "Kasa sayımı kaydedilemedi: tutarı ve günü kontrol et.";
            return PanoyaDon(Donem.Gun, gun);
        }

        var sayim = await veritabani.KasaSayimlari.FirstOrDefaultAsync(kayit => kayit.Gun == gun);
        // Açılış boşsa sabah girilen korunur; hiç girilmediyse 0 sayılmaz (sahte "kasa fazlası" çıkardı): sorulur
        decimal? acilisNakdiDegeri = acilis ?? sayim?.AcilisNakdi;
        if (acilisNakdiDegeri == null)
        {
            TempData["Mesaj"] = "Önce açılış nakdini gir (sabah kasada ne vardı; boşsa 0 yaz).";
            return PanoyaDon(Donem.Gun, gun);
        }
        if (sayim == null)
        {
            sayim = new KasaSayimi { Gun = gun };
            veritabani.KasaSayimlari.Add(sayim);
        }
        sayim.AcilisNakdi = acilisNakdiDegeri.Value;
        sayim.SayilanNakit = sayilan;
        sayim.Saat = DateTime.Now;
        sayim.KullaniciId = KullaniciId();
        await veritabani.SaveChangesAsync();

        TempData["Mesaj"] = "Kasa sayımı kaydedildi.";
        return PanoyaDon(Donem.Gun, gun);
    }

    // POST /Adisyon/GiderEkle : kasadan çıkan para (tedarikçi, fatura, avans...)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GiderEkle(DateTime gun, string tur, string? tutar, string? aciklama, Donem donem, DateTime tarih)
    {
        decimal? giderTutari = TutarCoz(tutar);
        if (giderTutari is not > 0 || giderTutari > 10_000_000 || !GiderTurleri.Hepsi.Contains(tur) || gun.Date > DateTime.Today)
        {
            TempData["Mesaj"] = "Gider eklenemedi: tutar, tür ve günü kontrol et (gelecek gün olmaz).";
            return PanoyaDon(donem, tarih);
        }

        // Bugünse şu anki saat, geçmiş günse o günün akşamı (listede sıralı dursun)
        DateTime giderZamani = gun.Date == DateTime.Today ? DateTime.Now : gun.Date.AddHours(23);
        veritabani.KasaHareketleri.Add(new KasaHareketi
        {
            Tarih = giderZamani,
            Tutar = giderTutari.Value,
            Tur = tur,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()[..Math.Min(120, aciklama.Trim().Length)],
            KullaniciId = KullaniciId()
        });
        await veritabani.SaveChangesAsync();

        TempData["Mesaj"] = "Gider eklendi.";
        return PanoyaDon(donem, tarih);
    }

    // POST /Adisyon/GiderSil
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GiderSil(int id, Donem donem, DateTime tarih)
    {
        var gider = await veritabani.KasaHareketleri.FindAsync(id);
        if (gider != null)
        {
            veritabani.KasaHareketleri.Remove(gider);
            await veritabani.SaveChangesAsync();
            TempData["Mesaj"] = "Gider silindi.";
        }
        return PanoyaDon(donem, tarih);
    }

    // POST /Adisyon/Hedef : ayın ciro hedefi (boş bırakılırsa hedef kalkar)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hedef(int yil, int ay, string? tutar, Donem donem, DateTime tarih)
    {
        decimal? hedefTutari = TutarCoz(tutar);
        if (ay < 1 || ay > 12 || yil < 2000 || yil > 2100)
        {
            return BadRequest();   // gizli alanlar: elle bozulmadıkça olmaz
        }
        if (hedefTutari is < 0 or > 100_000_000)
        {
            TempData["Mesaj"] = "Hedef kaydedilemedi: tutarı rakamla yaz (örn. 300.000).";
            return PanoyaDon(donem, tarih);
        }

        var hedef = await veritabani.AylikHedefler.FirstOrDefaultAsync(kayit => kayit.Yil == yil && kayit.Ay == ay);
        if (hedefTutari is not > 0)
        {
            if (hedef != null) veritabani.AylikHedefler.Remove(hedef);
        }
        else if (hedef == null)
        {
            veritabani.AylikHedefler.Add(new AylikHedef { Yil = yil, Ay = ay, Tutar = hedefTutari.Value });
        }
        else
        {
            hedef.Tutar = hedefTutari.Value;
        }
        await veritabani.SaveChangesAsync();

        TempData["Mesaj"] = hedefTutari is > 0 ? "Aylık hedef kaydedildi." : "Aylık hedef kaldırıldı.";
        return PanoyaDon(donem, tarih);
    }

    // POST /Adisyon/AyGonder : aylık raporu şimdi e-postayla gönder (eski Gün sonu > Aylık'tan taşındı)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AyGonder(int yil, int ay)
    {
        if (ay < 1 || ay > 12 || yil < 2000 || yil > 2100)
        {
            return BadRequest();
        }
        try
        {
            await raporGondermeServisi.ElleGonderAsync(yil, ay);
            TempData["Mesaj"] = "Rapor e-postayla gönderildi.";
        }
        catch (EpostaHatasi hata)
        {
            TempData["Mesaj"] = hata.Message;
        }
        return PanoyaDon(Donem.Ay, new DateTime(yil, ay, 1));
    }

    private RedirectToActionResult PanoyaDon(Donem donem, DateTime tarih) =>
        RedirectToAction(nameof(Index), new { donem, tarih = tarih.ToString("yyyy-MM-dd") });

    private int KullaniciId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // "1.250,50" / "1250,5" / "1.250" / "300.000" / "1250.5" -> sayı ; boş -> null ; okunamazsa -1 (geçersiz sayılır)
    // Türkçe yazımda nokta binlik ayracıdır: virgül yoksa ve noktalar 3'erli grupları ayırıyorsa binlik sayılır.
    internal static decimal? TutarCoz(string? yazi)
    {
        if (string.IsNullOrWhiteSpace(yazi)) return null;
        string temiz = yazi.Trim().Replace("₺", "").Replace(" ", "");
        if (temiz.Contains(','))
        {
            temiz = temiz.Replace(".", "").Replace(',', '.');
        }
        else if (BinlikNoktali.IsMatch(temiz))
        {
            temiz = temiz.Replace(".", "");
        }
        return decimal.TryParse(temiz, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var tutar)
            ? Math.Round(tutar, 2) : -1;
    }

    private static readonly System.Text.RegularExpressions.Regex BinlikNoktali = new(@"^-?\d{1,3}(\.\d{3})+$");
}

// Eski "Kayıtlar" adresleri (yer imleri, e-postalar) Adisyon paneline yönlenir.
[Authorize(Roles = Roller.Yonetici)]
public class KayitlarController : Controller
{
    // /Kayitlar?gun=2026-11-16&durum=Iptal -> /Adisyon?tarih=2026-11-16&durum=Iptal (süzgeçler de taşınır)
    [HttpGet]
    public IActionResult Index(DateTime? gun, AdisyonDurumu? durum, string? kaynak, int? odemeTuruId) =>
        RedirectToActionPermanent("Index", "Adisyon",
            new { tarih = gun?.ToString("yyyy-MM-dd"), durum, kaynak, odemeTuruId });

    [HttpGet]
    public IActionResult Detay(int id) => RedirectToActionPermanent("Detay", "Adisyon", new { id });
}

// Gün sonu ekranı Adisyon paneline katıldı: eski adresler (yer imleri, e-postadaki bağlantılar) oraya yönlenir.
[Authorize(Roles = Roller.Yonetici)]
public class GunSonuController : Controller
{
    // /GunSonu?gun=2026-11-16 -> /Adisyon?donem=Gun&tarih=2026-11-16
    [HttpGet]
    public IActionResult Index(DateTime? gun) =>
        RedirectToActionPermanent("Index", "Adisyon", new { donem = Donem.Gun, tarih = (gun ?? DateTime.Today).ToString("yyyy-MM-dd") });

    // /GunSonu/Ay?yil=2026&ay=11 -> /Adisyon?donem=Ay&tarih=2026-11-01
    [HttpGet]
    public IActionResult Ay(int? yil, int? ay)
    {
        int secilenYil = yil is >= 2000 and <= 2100 ? yil.Value : DateTime.Today.Year;
        int secilenAy = ay is >= 1 and <= 12 ? ay.Value : DateTime.Today.Month;
        return RedirectToActionPermanent("Index", "Adisyon", new { donem = Donem.Ay, tarih = $"{secilenYil:0000}-{secilenAy:00}-01" });
    }
}
