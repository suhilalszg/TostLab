using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

namespace TostLab.Services;

// Adisyonun bütün iş kuralları burada. Controller sadece isteği alır ve buraya verir.
// Böylece kurallar tek yerde durur; ileride telefon/tablet ekranı eklense de aynı servis kullanılır.
public class AdisyonServisi
{
    private readonly TostLabDbContext veritabani;

    public AdisyonServisi(TostLabDbContext veritabani)
    {
        this.veritabani = veritabani;
    }

    // ===================== OKUMA =====================

    public async Task<EkranDurumu> EkranDurumuAsync()
    {
        // Açık adisyonları tek sorguda al (Durum + MasaId indeksi bunu hızlandırır)
        var acikAdisyonlar = await veritabani.Siparisler
            .Where(siparis => siparis.Durum == AdisyonDurumu.Acik)
            .Select(siparis => new { siparis.Id, siparis.MasaId, siparis.Kaynak, siparis.Toplam, siparis.Tarih, siparis.KisiSayisi })
            .ToListAsync();

        var masalar = await veritabani.Masalar.Where(masa => masa.AktifMi).OrderBy(masa => masa.Id).ToListAsync();

        var masaBilgileri = masalar.Select(masa =>
        {
            var adisyon = acikAdisyonlar.FirstOrDefault(acik => acik.MasaId == masa.Id);
            return adisyon == null
                ? new MasaBilgisi(masa.Id, masa.Ad, false, null, 0, null, null)
                : new MasaBilgisi(masa.Id, masa.Ad, true, adisyon.Id, adisyon.Toplam, adisyon.Tarih, adisyon.KisiSayisi);
        }).ToList();

        var paketler = acikAdisyonlar
            .Where(adisyon => adisyon.MasaId == null)
            .OrderBy(adisyon => adisyon.Tarih)
            .Select(adisyon => new PaketBilgisi(adisyon.Id, PaketBasligi(adisyon.Kaynak, adisyon.Tarih), adisyon.Kaynak, adisyon.Toplam, adisyon.Tarih))
            .ToList();

        return new EkranDurumu(masaBilgileri, paketler);
    }

    public async Task<AdisyonBilgisi?> AdisyonAsync(int adisyonId)
    {
        var adisyon = await AcikAdisyonuYukleAsync(adisyonId, zorunlu: false);
        return adisyon == null ? null : Bilgiye(adisyon);
    }

    // ===================== DEĞİŞTİRME =====================

    // Ürüne dokununca: adisyon yoksa açar, ürün varsa adedini arttırır
    public async Task<AdisyonBilgisi> UrunEkleAsync(UrunEkleIstegi istek)
    {
        if (istek.Adet < 1 || istek.Adet > 99)
        {
            throw new AdisyonHatasi("Adet 1 ile 99 arasında olmalı.");
        }

        var urun = await veritabani.Urunler.FirstOrDefaultAsync(oge => oge.Id == istek.UrunId && oge.SatistaMi)
            ?? throw new AdisyonHatasi("Bu ürün satışta değil. Sayfayı yenile.");
        if (urun.Fiyat <= 0)
        {
            throw new AdisyonHatasi($"{urun.Ad} ürününün fiyatı girilmemiş.");
        }

        Siparis adisyon = istek.AdisyonId is int adisyonId
            ? await AcikAdisyonuYukleAsync(adisyonId, zorunlu: true) ?? throw new AdisyonHatasi("Adisyon bulunamadı.")
            : await YeniAdisyonAsync(istek.Kaynak, istek.MasaId);

        // Masa açılırken kişi sayısı önceden girildiyse ilk ürünle birlikte gelir
        if (istek.KisiSayisi is int kisi && adisyon.KisiSayisi == null && adisyon.MasaId != null)
        {
            adisyon.KisiSayisi = KisiyiDenetle(kisi);
        }

        // Aynı ürün aynı fiyatla zaten varsa yeni satır açma, adedi arttır
        var satir = adisyon.Satirlar.FirstOrDefault(oge => oge.UrunId == urun.Id && oge.BirimFiyat == urun.Fiyat);
        if (satir == null)
        {
            satir = new SiparisSatiri { UrunId = urun.Id, Urun = urun, BirimFiyat = urun.Fiyat, Adet = 0 };
            adisyon.Satirlar.Add(satir);
        }
        satir.Adet = Math.Min(99, satir.Adet + istek.Adet);

        ToplamiHesapla(adisyon);
        await veritabani.SaveChangesAsync();
        return Bilgiye(adisyon);
    }

    // − / + / sil: adet 0 olursa satır silinir; adisyonda satır kalmazsa adisyon da silinir (masa boşalır)
    public async Task<AdisyonBilgisi?> AdetDegistirAsync(AdetIstegi istek)
    {
        var adisyon = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);
        var satir = adisyon!.Satirlar.FirstOrDefault(oge => oge.Id == istek.SatirId)
            ?? throw new AdisyonHatasi("Satır bulunamadı. Sayfayı yenile.");

        if (istek.Adet <= 0)
        {
            adisyon.Satirlar.Remove(satir);
            veritabani.SiparisSatirlari.Remove(satir);
        }
        else
        {
            satir.Adet = Math.Min(99, istek.Adet);
        }

        if (adisyon.Satirlar.Count == 0)
        {
            // Hiç ürün kalmadı: yanlışlıkla açılmış adisyon, iz bırakmadan kaldır
            veritabani.Siparisler.Remove(adisyon);
            await veritabani.SaveChangesAsync();
            return null;
        }

        ToplamiHesapla(adisyon);
        await veritabani.SaveChangesAsync();
        return Bilgiye(adisyon);
    }

    // Masadaki kişi sayısı: 1–50 arası
    public async Task<AdisyonBilgisi> KisiDegistirAsync(KisiIstegi istek)
    {
        var adisyon = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);
        if (adisyon!.MasaId == null)
        {
            throw new AdisyonHatasi("Kişi sayısı sadece masa için girilir.");
        }
        adisyon.KisiSayisi = KisiyiDenetle(istek.Kisi);
        await veritabani.SaveChangesAsync();
        return Bilgiye(adisyon);
    }

    // Masa ve telefon paketinde ödeme türü seçilir.
    // Yemeksepeti/Getir/Trendyol/Migros'ta para uygulamada ödenmiştir: ödeme türü sorulmaz, sadece kapanır.
    public async Task OdeAsync(OdemeIstegi istek)
    {
        var adisyon = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);

        if (Kaynaklar.PlatformMu(adisyon!.Kaynak))
        {
            adisyon.OdemeTuruId = null;
        }
        else
        {
            bool odemeVar = await veritabani.OdemeTurleri.AnyAsync(odeme => odeme.Id == istek.OdemeTuruId && odeme.AktifMi);
            if (!odemeVar)
            {
                throw new AdisyonHatasi("Ödeme türünü seç.");
            }
            adisyon.OdemeTuruId = istek.OdemeTuruId;
        }

        ToplamiHesapla(adisyon);
        adisyon.Durum = AdisyonDurumu.Odendi;
        adisyon.KapanisSaati = DateTime.Now;
        await veritabani.SaveChangesAsync();
    }

    public async Task IptalEtAsync(IptalIstegi istek, int kullaniciId)
    {
        if (string.IsNullOrWhiteSpace(istek.Neden))
        {
            throw new AdisyonHatasi("İptal nedenini yaz.");
        }

        var adisyon = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);
        adisyon!.Durum = AdisyonDurumu.Iptal;
        adisyon.IptalNedeni = istek.Neden.Trim();
        adisyon.IptalSaati = DateTime.Now;
        adisyon.IptalEdenKullaniciId = kullaniciId;
        adisyon.KapanisSaati = adisyon.IptalSaati;
        await veritabani.SaveChangesAsync();
    }

    // ===================== YARDIMCILAR =====================

    private async Task<Siparis> YeniAdisyonAsync(string kaynak, int? masaId)
    {
        if (!Kaynaklar.Hepsi.Contains(kaynak))
        {
            throw new AdisyonHatasi("Siparişin nereden geldiğini seç.");
        }

        if (kaynak == Kaynaklar.Masa)
        {
            bool masaVar = await veritabani.Masalar.AnyAsync(masa => masa.Id == masaId && masa.AktifMi);
            if (!masaVar)
            {
                throw new AdisyonHatasi("Masa bulunamadı.");
            }

            // Bir masada aynı anda tek açık adisyon olur: varsa onu kullan
            var acikOlan = await veritabani.Siparisler
                .Include(siparis => siparis.Satirlar).ThenInclude(satir => satir.Urun)
                .Include(siparis => siparis.Masa)
                .FirstOrDefaultAsync(siparis => siparis.MasaId == masaId && siparis.Durum == AdisyonDurumu.Acik);
            if (acikOlan != null)
            {
                return acikOlan;
            }
        }
        else
        {
            masaId = null;
        }

        var yeni = new Siparis
        {
            Tarih = DateTime.Now,
            Kaynak = kaynak,
            MasaId = masaId,
            Durum = AdisyonDurumu.Acik
        };
        if (masaId != null)
        {
            yeni.Masa = await veritabani.Masalar.FindAsync(masaId);
        }
        veritabani.Siparisler.Add(yeni);
        return yeni;
    }

    private async Task<Siparis?> AcikAdisyonuYukleAsync(int adisyonId, bool zorunlu)
    {
        var adisyon = await veritabani.Siparisler
            .Include(siparis => siparis.Satirlar).ThenInclude(satir => satir.Urun)
            .Include(siparis => siparis.Masa)
            .FirstOrDefaultAsync(siparis => siparis.Id == adisyonId);

        if (adisyon == null || !adisyon.AcikMi)
        {
            if (zorunlu)
            {
                throw new AdisyonHatasi("Bu adisyon kapanmış. Ekran yenileniyor.");
            }
            return null;
        }
        return adisyon;
    }

    private static int KisiyiDenetle(int kisi)
    {
        if (kisi < 1 || kisi > 50)
        {
            throw new AdisyonHatasi("Kişi sayısı 1 ile 50 arasında olmalı.");
        }
        return kisi;
    }

    private static void ToplamiHesapla(Siparis adisyon)
    {
        adisyon.Toplam = adisyon.Satirlar.Sum(satir => satir.Tutar);
    }

    // "Getir 14:05": kasada hangi sipariş olduğu saatle daha kolay anlaşılır
    private static string PaketBasligi(string kaynak, DateTime acilis) => $"{kaynak} {acilis:HH:mm}";

    private static AdisyonBilgisi Bilgiye(Siparis adisyon)
    {
        string baslik = adisyon.Masa?.Ad ?? PaketBasligi(adisyon.Kaynak, adisyon.Tarih);
        var satirlar = adisyon.Satirlar
            .OrderBy(satir => satir.Id == 0 ? int.MaxValue : satir.Id)   // yeni eklenen en altta
            .Select(satir => new SatirBilgisi(satir.Id, satir.UrunId, satir.Urun.Ad, satir.Adet, satir.BirimFiyat, satir.Tutar))
            .ToList();
        return new AdisyonBilgisi(adisyon.Id, adisyon.Kaynak, adisyon.MasaId, baslik, adisyon.Tarih, adisyon.Toplam, adisyon.KisiSayisi, satirlar);
    }
}
