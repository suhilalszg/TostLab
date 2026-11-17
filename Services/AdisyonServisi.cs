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
            .Select(siparis => new
            {
                siparis.Id, siparis.MasaId, siparis.Kaynak, siparis.Toplam, siparis.Tarih, siparis.KisiSayisi,
                Garson = siparis.AcanKullanici != null ? siparis.AcanKullanici.KullaniciAdi : null
            })
            .ToListAsync();

        var masalar = await veritabani.Masalar.Where(masa => masa.AktifMi).OrderBy(masa => masa.Id).ToListAsync();

        var masaBilgileri = masalar.Select(masa =>
        {
            var adisyon = acikAdisyonlar.FirstOrDefault(acik => acik.MasaId == masa.Id);
            return adisyon == null
                ? new MasaBilgisi(masa.Id, masa.Ad, false, null, 0, null, null, null)
                : new MasaBilgisi(masa.Id, masa.Ad, true, adisyon.Id, adisyon.Toplam, adisyon.Tarih, adisyon.KisiSayisi, adisyon.Garson);
        }).ToList();

        var paketler = acikAdisyonlar
            .Where(adisyon => adisyon.MasaId == null)
            .OrderBy(adisyon => adisyon.Tarih)
            .Select(adisyon => new PaketBilgisi(adisyon.Id, PaketBasligi(adisyon.Kaynak, adisyon.Tarih), adisyon.Kaynak,
                adisyon.Toplam, adisyon.Tarih, adisyon.Garson))
            .ToList();

        return new EkranDurumu(masaBilgileri, paketler);
    }

    public async Task<AdisyonBilgisi?> AdisyonAsync(int adisyonId)
    {
        var adisyon = await AcikAdisyonuYukleAsync(adisyonId, zorunlu: false);
        return adisyon == null ? null : Bilgiye(adisyon);
    }

    // İade penceresi: bugün ödenmiş adisyonlar, en yenisi üstte
    public async Task<List<OdenmisAdisyon>> BugunOdenenlerAsync()
    {
        var bugun = DateTime.Today;
        var liste = await veritabani.Siparisler
            .Where(siparis => siparis.Durum == AdisyonDurumu.Odendi && siparis.KapanisSaati >= bugun)
            .OrderByDescending(siparis => siparis.KapanisSaati)
            .Take(60)
            .Select(siparis => new
            {
                siparis.Id, siparis.Kaynak, siparis.Tarih, siparis.Toplam, siparis.KapanisSaati,
                MasaAdi = siparis.Masa != null ? siparis.Masa.Ad : null,
                OdemeTuru = siparis.OdemeTuru != null ? siparis.OdemeTuru.Ad : null
            })
            .ToListAsync();

        return liste.Select(siparis => new OdenmisAdisyon(siparis.Id,
                siparis.MasaAdi ?? PaketBasligi(siparis.Kaynak, siparis.Tarih),
                siparis.Kaynak, siparis.Toplam, siparis.KapanisSaati!.Value,
                siparis.OdemeTuru ?? (Kaynaklar.PlatformMu(siparis.Kaynak) ? "Online" : null)))
            .ToList();
    }

    // ===================== DEĞİŞTİRME =====================

    // Ürüne dokununca: adisyon yoksa açar (açan kişi garson olarak yazılır), ürün varsa adedini arttırır
    public async Task<AdisyonBilgisi> UrunEkleAsync(UrunEkleIstegi istek, int kullaniciId)
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
            : await YeniAdisyonAsync(istek.Kaynak, istek.MasaId, kullaniciId);

        // Masa açılırken kişi sayısı önceden girildiyse ilk ürünle birlikte gelir
        if (istek.KisiSayisi is int kisi && adisyon.KisiSayisi == null && adisyon.MasaId != null)
        {
            adisyon.KisiSayisi = KisiyiDenetle(kisi);
        }

        // Aynı ürün aynı fiyatla zaten varsa yeni satır açma, adedi arttır (ikram satırına eklenmez)
        var satir = adisyon.Satirlar.FirstOrDefault(oge => oge.UrunId == urun.Id && oge.BirimFiyat == urun.Fiyat && !oge.IkramMi);
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
    // Adet azalırsa (ya da satır silinirse) kim, ne zaman, neyi çıkardı: SilinenSatirlar tablosuna yazılır.
    public async Task<AdisyonBilgisi?> AdetDegistirAsync(AdetIstegi istek, int kullaniciId)
    {
        var adisyon = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);
        var satir = SatiriBul(adisyon!, istek.SatirId);

        int yeniAdet = Math.Clamp(istek.Adet, 0, 99);
        if (yeniAdet < satir.Adet)
        {
            veritabani.SilinenSatirlar.Add(new SilinenSatir
            {
                SiparisId = adisyon!.Id,
                AdisyonBasligi = Baslik(adisyon),
                UrunId = satir.UrunId,
                UrunAdi = satir.Urun.Ad + (satir.IkramMi ? " (ikram)" : ""),
                Adet = satir.Adet - yeniAdet,
                BirimFiyat = satir.IkramMi ? 0 : satir.BirimFiyat,
                Saat = DateTime.Now,
                KullaniciId = kullaniciId
            });
        }

        if (yeniAdet == 0)
        {
            SatiriKaldir(adisyon!, satir);
        }
        else
        {
            satir.Adet = yeniAdet;
        }

        if (adisyon!.Satirlar.Count == 0)
        {
            // Hiç ürün kalmadı: yanlışlıkla açılmış adisyon, kaldır (silinen ürün izi kalır)
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

    // İndirim: yüzde (1–100) ya da tutar (₺). Değer 0 indirimi kaldırır.
    // Platform siparişinde yok: fiyatı ve kampanyayı uygulama belirliyor.
    // Kim yaptıysa kaydedilir (yönetici Adisyon panelinde görür).
    public async Task<AdisyonBilgisi> IndirimUygulaAsync(IndirimIstegi istek, int kullaniciId)
    {
        var adisyon = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);
        PlatformDegilse(adisyon!, "indirim yapılmaz");

        if (istek.Deger == 0)
        {
            adisyon!.IndirimDegeri = 0;
            adisyon.IndirimNedeni = null;
            adisyon.IndirimYapanKullaniciId = null;
        }
        else
        {
            if (istek.Deger < 0 || (istek.YuzdeMi && istek.Deger > 100))
            {
                throw new AdisyonHatasi(istek.YuzdeMi ? "Yüzde 1 ile 100 arasında olmalı." : "İndirim tutarı geçerli değil.");
            }
            decimal araToplam = adisyon!.Satirlar.Sum(satir => satir.Tutar);
            if (!istek.YuzdeMi && istek.Deger > araToplam)
            {
                throw new AdisyonHatasi("İndirim adisyon tutarından büyük olamaz.");
            }
            adisyon.IndirimYuzdeMi = istek.YuzdeMi;
            adisyon.IndirimDegeri = Math.Round(istek.Deger, 2);
            adisyon.IndirimNedeni = string.IsNullOrWhiteSpace(istek.Neden) ? null : istek.Neden.Trim();
            adisyon.IndirimYapanKullaniciId = kullaniciId;
        }

        ToplamiHesapla(adisyon!);
        await veritabani.SaveChangesAsync();
        return Bilgiye(adisyon!);
    }

    // İkram: satır ücretsiz olur (ya da ikram geri alınır). Kim yaptı saklanır.
    // Adet verilirse satırın sadece o kadarı ikram olur: "3 tost" → "2 tost" + "1 tost (ikram)".
    public async Task<AdisyonBilgisi> IkramAsync(IkramIstegi istek, int kullaniciId)
    {
        var adisyon = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);
        PlatformDegilse(adisyon!, "ikram yapılmaz");
        var satir = SatiriBul(adisyon!, istek.SatirId);

        if (satir.IkramMi)
        {
            // İkramı geri al: aynı ürünün ücretli satırı varsa ona katılır
            var ucretli = adisyon!.Satirlar.FirstOrDefault(oge => oge != satir && !oge.IkramMi
                && oge.UrunId == satir.UrunId && oge.BirimFiyat == satir.BirimFiyat);
            if (ucretli != null)
            {
                ucretli.Adet = Math.Min(99, ucretli.Adet + satir.Adet);
                SatiriKaldir(adisyon, satir);
            }
            else
            {
                satir.IkramMi = false;
                satir.IkramEdenKullaniciId = null;
            }
        }
        else
        {
            int adet = istek.Adet ?? satir.Adet;
            if (adet < 1 || adet > satir.Adet)
            {
                throw new AdisyonHatasi($"İkram adedi 1 ile {satir.Adet} arasında olmalı.");
            }

            // Aynı ürünün ikram satırı varsa ona eklenir, yoksa yeni satır
            var ikramSatiri = adisyon!.Satirlar.FirstOrDefault(oge => oge.IkramMi
                && oge.UrunId == satir.UrunId && oge.BirimFiyat == satir.BirimFiyat);
            if (adet == satir.Adet && ikramSatiri == null)
            {
                satir.IkramMi = true;
                satir.IkramEdenKullaniciId = kullaniciId;
            }
            else
            {
                if (ikramSatiri == null)
                {
                    ikramSatiri = new SiparisSatiri
                    {
                        UrunId = satir.UrunId, Urun = satir.Urun, BirimFiyat = satir.BirimFiyat,
                        Adet = 0, IkramMi = true, IkramEdenKullaniciId = kullaniciId
                    };
                    adisyon.Satirlar.Add(ikramSatiri);
                }
                ikramSatiri.Adet = Math.Min(99, ikramSatiri.Adet + adet);
                satir.Adet -= adet;
                if (satir.Adet == 0)
                {
                    SatiriKaldir(adisyon, satir);
                }
            }
        }

        ToplamiHesapla(adisyon!);
        await veritabani.SaveChangesAsync();
        return Bilgiye(adisyon!);
    }

    // Taşı: adisyonu başka masaya geçir.
    // Hedef masa boşsa adisyon oraya taşınır; doluysa iki adisyon birleşir (bu adisyon kapanır).
    // Telefonla gelen paket de masaya alınabilir; platform siparişi alınamaz.
    public async Task<AdisyonBilgisi> TasiAsync(TasiIstegi istek)
    {
        var kaynak = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);
        PlatformDegilse(kaynak!, "masaya taşınmaz");
        if (kaynak!.MasaId == istek.HedefMasaId)
        {
            throw new AdisyonHatasi("Adisyon zaten bu masada.");
        }

        var hedefMasa = await veritabani.Masalar.FirstOrDefaultAsync(masa => masa.Id == istek.HedefMasaId && masa.AktifMi)
            ?? throw new AdisyonHatasi("Masa bulunamadı.");

        var hedef = await AdisyonSorgusu()
            .FirstOrDefaultAsync(siparis => siparis.MasaId == hedefMasa.Id && siparis.Durum == AdisyonDurumu.Acik);

        if (hedef == null)
        {
            kaynak.MasaId = hedefMasa.Id;
            kaynak.Masa = hedefMasa;
            kaynak.Kaynak = Kaynaklar.Masa;
            await veritabani.SaveChangesAsync();
            return Bilgiye(kaynak);
        }

        // Birleştirme: iki indirimi karıştırmamak için taşınan adisyonda indirim olmasın
        if (kaynak.IndirimDegeri > 0)
        {
            throw new AdisyonHatasi($"{hedefMasa.Ad} dolu. Birleştirmek için önce bu adisyondaki indirimi kaldır.");
        }

        foreach (var satir in kaynak.Satirlar.ToList())
        {
            var ayni = hedef.Satirlar.FirstOrDefault(oge => oge.UrunId == satir.UrunId
                && oge.BirimFiyat == satir.BirimFiyat && oge.IkramMi == satir.IkramMi);
            if (ayni != null)
            {
                ayni.Adet = Math.Min(99, ayni.Adet + satir.Adet);
            }
            else
            {
                // Satırı hedef adisyona geçir (silinmesin diye kaynağın listesinden önce ayrılır)
                satir.Siparis = hedef;
                satir.SiparisId = hedef.Id;
            }
        }
        // Geçen satırlar kaynağın listesinden çıksın; yoksa kaynak silinirken onlar da silinir
        veritabani.ChangeTracker.DetectChanges();
        if (kaynak.KisiSayisi is int kaynakKisi)
        {
            hedef.KisiSayisi = Math.Min(50, (hedef.KisiSayisi ?? 0) + kaynakKisi);
        }

        // Taşınan adisyondan silinmiş ürünlerin izi hedef adisyona bağlansın
        var izler = await veritabani.SilinenSatirlar.Where(iz => iz.SiparisId == kaynak.Id).ToListAsync();
        foreach (var iz in izler)
        {
            iz.SiparisId = hedef.Id;
        }

        veritabani.Siparisler.Remove(kaynak);   // kalan (hedefle birleşen) satırlar da silinir
        ToplamiHesapla(hedef);
        await veritabani.SaveChangesAsync();
        return Bilgiye(hedef);
    }

    // Böl: seçilen ürünler ayrı bir adisyon olarak hemen ödenir, kalanı masada kalır.
    // ("Ben kendi tostumu ödeyeyim" durumu.) Yüzde indirim varsa ödenen kısma da uygulanır.
    public async Task<AdisyonBilgisi?> BolOdeAsync(BolIstegi istek)
    {
        var kaynak = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);
        PlatformDegilse(kaynak!, "bölünmez");

        var secimler = (istek.Secimler ?? new()).Where(secim => secim.Adet > 0).ToList();
        if (secimler.Count == 0)
        {
            throw new AdisyonHatasi("Ödenecek ürünleri seç.");
        }
        if (secimler.Select(secim => secim.SatirId).Distinct().Count() != secimler.Count)
        {
            throw new AdisyonHatasi("Aynı ürün iki kez seçilmiş.");
        }

        var odemeTuruId = await OdemeTurunuDenetleAsync(istek.OdemeTuruId);

        // Hepsi seçildiyse bölmeye gerek yok: adisyon normal kapanır
        bool hepsi = kaynak!.Satirlar.All(satir => secimler.Any(secim => secim.SatirId == satir.Id && secim.Adet >= satir.Adet));
        if (hepsi)
        {
            await OdeAsync(new OdemeIstegi(kaynak.Id, odemeTuruId));
            return null;
        }

        var odenen = new Siparis
        {
            Tarih = kaynak.Tarih,                 // aynı güne, aynı açılış saatine yazılsın
            Kaynak = kaynak.Kaynak,
            MasaId = kaynak.MasaId,
            AcanKullaniciId = kaynak.AcanKullaniciId,
            Durum = AdisyonDurumu.Odendi,
            OdemeTuruId = odemeTuruId,
            KapanisSaati = DateTime.Now,
            KomisyonOrani = 0
        };
        if (kaynak.IndirimYuzdeMi && kaynak.IndirimDegeri > 0)
        {
            odenen.IndirimYuzdeMi = true;
            odenen.IndirimDegeri = kaynak.IndirimDegeri;
            odenen.IndirimNedeni = kaynak.IndirimNedeni;
            odenen.IndirimYapanKullaniciId = kaynak.IndirimYapanKullaniciId;
        }

        foreach (var secim in secimler)
        {
            var satir = SatiriBul(kaynak, secim.SatirId);
            if (secim.Adet > satir.Adet)
            {
                throw new AdisyonHatasi($"{satir.Urun.Ad} için en fazla {satir.Adet} seçilebilir.");
            }
            odenen.Satirlar.Add(new SiparisSatiri
            {
                UrunId = satir.UrunId, Urun = satir.Urun, BirimFiyat = satir.BirimFiyat, Adet = secim.Adet,
                IkramMi = satir.IkramMi, IkramEdenKullaniciId = satir.IkramEdenKullaniciId
            });
            // Bu ürün silinmedi, ödendi: silinen ürün izine yazılmaz
            satir.Adet -= secim.Adet;
            if (satir.Adet == 0)
            {
                SatiriKaldir(kaynak, satir);
            }
        }

        ToplamiHesapla(odenen);
        ToplamiHesapla(kaynak);
        veritabani.Siparisler.Add(odenen);
        await FisEkleAsync(odenen, kaynak.Masa, kaynak.AcanKullanici);
        await veritabani.SaveChangesAsync();
        return Bilgiye(kaynak);
    }

    // Masa ve telefon paketinde ödeme türü seçilir.
    // Yemeksepeti/Getir/Trendyol/Migros'ta para uygulamada ödenmiştir: ödeme türü sorulmaz, sadece kapanır.
    public async Task OdeAsync(OdemeIstegi istek)
    {
        var adisyon = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);

        if (Kaynaklar.PlatformMu(adisyon!.Kaynak))
        {
            adisyon.OdemeTuruId = null;
            // O anki oranı siparişe kopyala: oran sonradan değişse de bu siparişin komisyonu değişmez
            adisyon.KomisyonOrani = await veritabani.PlatformKomisyonlari
                .Where(komisyon => komisyon.Kaynak == adisyon.Kaynak)
                .Select(komisyon => komisyon.Oran)
                .FirstOrDefaultAsync();
        }
        else
        {
            adisyon.KomisyonOrani = 0;
            adisyon.OdemeTuruId = await OdemeTurunuDenetleAsync(istek.OdemeTuruId);
        }

        ToplamiHesapla(adisyon);
        adisyon.Durum = AdisyonDurumu.Odendi;
        adisyon.KapanisSaati = DateTime.Now;
        await FisEkleAsync(adisyon, adisyon.Masa, adisyon.AcanKullanici);
        await veritabani.SaveChangesAsync();
    }

    // Ödenen adisyonun fişi aynı kayıtla (aynı SaveChanges) yazılır: ödeme var da fiş yok durumu olmaz
    private async Task FisEkleAsync(Siparis odenen, Masa? masa, Kullanici? acan)
    {
        string? odemeTuruAdi = odenen.OdemeTuruId is int odemeTuruId
            ? await veritabani.OdemeTurleri.Where(tur => tur.Id == odemeTuruId).Select(tur => tur.Ad).FirstOrDefaultAsync()
            : null;
        veritabani.FisKayitlari.Add(FisServisi.Olustur(odenen,
            masa?.Ad ?? PaketBasligi(odenen.Kaynak, odenen.Tarih),
            odemeTuruAdi, acan?.KullaniciAdi, odenen.KapanisSaati ?? DateTime.Now));
    }

    public async Task IptalEtAsync(IptalIstegi istek, int kullaniciId)
    {
        string neden = NedenZorunlu(istek.Neden, "İptal nedenini yaz.");
        var adisyon = await AcikAdisyonuYukleAsync(istek.AdisyonId, zorunlu: true);
        adisyon!.Durum = AdisyonDurumu.Iptal;
        adisyon.IptalNedeni = neden;
        adisyon.IptalSaati = DateTime.Now;
        adisyon.IptalEdenKullaniciId = kullaniciId;
        adisyon.KapanisSaati = adisyon.IptalSaati;
        await veritabani.SaveChangesAsync();
    }

    // İade: ödenmiş adisyonun parası geri verildi. Ciro ve ödeme dağılımından düşer, Adisyon panelinde "İade" görünür.
    public async Task IadeEtAsync(IadeIstegi istek, int kullaniciId)
    {
        string neden = NedenZorunlu(istek.Neden, "İade nedenini yaz.");
        var adisyon = await veritabani.Siparisler.FirstOrDefaultAsync(siparis => siparis.Id == istek.AdisyonId);
        if (adisyon == null || adisyon.Durum != AdisyonDurumu.Odendi)
        {
            throw new AdisyonHatasi("Sadece ödenmiş adisyon iade edilir.");
        }
        adisyon.Durum = AdisyonDurumu.Iade;
        adisyon.IadeNedeni = neden;
        adisyon.IadeSaati = DateTime.Now;
        adisyon.IadeEdenKullaniciId = kullaniciId;
        await veritabani.SaveChangesAsync();
    }

    // ===================== YARDIMCILAR =====================

    private async Task<Siparis> YeniAdisyonAsync(string kaynak, int? masaId, int kullaniciId)
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
            var acikOlan = await AdisyonSorgusu()
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
            Durum = AdisyonDurumu.Acik,
            AcanKullaniciId = kullaniciId,
            AcanKullanici = await veritabani.Kullanicilar.FindAsync(kullaniciId)
        };
        if (masaId != null)
        {
            yeni.Masa = await veritabani.Masalar.FindAsync(masaId);
        }
        veritabani.Siparisler.Add(yeni);
        return yeni;
    }

    private IQueryable<Siparis> AdisyonSorgusu() => veritabani.Siparisler
        .Include(siparis => siparis.Satirlar).ThenInclude(satir => satir.Urun)
        .Include(siparis => siparis.Masa)
        .Include(siparis => siparis.AcanKullanici);

    private async Task<Siparis?> AcikAdisyonuYukleAsync(int adisyonId, bool zorunlu)
    {
        var adisyon = await AdisyonSorgusu().FirstOrDefaultAsync(siparis => siparis.Id == adisyonId);

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

    private async Task<int> OdemeTurunuDenetleAsync(int? odemeTuruId)
    {
        bool odemeVar = await veritabani.OdemeTurleri.AnyAsync(odeme => odeme.Id == odemeTuruId && odeme.AktifMi);
        if (!odemeVar)
        {
            throw new AdisyonHatasi("Ödeme türünü seç.");
        }
        return odemeTuruId!.Value;
    }

    private static SiparisSatiri SatiriBul(Siparis adisyon, int satirId) =>
        adisyon.Satirlar.FirstOrDefault(oge => oge.Id == satirId)
            ?? throw new AdisyonHatasi("Satır bulunamadı. Sayfayı yenile.");

    private void SatiriKaldir(Siparis adisyon, SiparisSatiri satir)
    {
        adisyon.Satirlar.Remove(satir);
        if (satir.Id != 0)
        {
            veritabani.SiparisSatirlari.Remove(satir);
        }
    }

    private static void PlatformDegilse(Siparis adisyon, string neYapilmaz)
    {
        if (Kaynaklar.PlatformMu(adisyon.Kaynak))
        {
            throw new AdisyonHatasi($"Platform siparişinde {neYapilmaz}.");
        }
    }

    private static string NedenZorunlu(string? neden, string mesaj) =>
        string.IsNullOrWhiteSpace(neden) ? throw new AdisyonHatasi(mesaj) : neden.Trim();

    private static int KisiyiDenetle(int kisi)
    {
        if (kisi < 1 || kisi > 50)
        {
            throw new AdisyonHatasi("Kişi sayısı 1 ile 50 arasında olmalı.");
        }
        return kisi;
    }

    // Tek hesap yeri: satır toplamı (ikramlar 0) - indirim. Ekrandaki satis.js de aynı kuralı uygular.
    private static void ToplamiHesapla(Siparis adisyon)
    {
        decimal araToplam = adisyon.Satirlar.Sum(satir => satir.Tutar);
        adisyon.IndirimTutari = IndirimHesapla(araToplam, adisyon.IndirimYuzdeMi, adisyon.IndirimDegeri);
        adisyon.Toplam = araToplam - adisyon.IndirimTutari;
    }

    public static decimal IndirimHesapla(decimal araToplam, bool yuzdeMi, decimal deger)
    {
        if (deger <= 0)
        {
            return 0;
        }
        decimal indirim = yuzdeMi ? Math.Round(araToplam * deger / 100, 2) : deger;
        return Math.Min(indirim, araToplam);   // ürün silinip tutar küçülse de eksiye düşmesin
    }

    // "Getir 14:05": kasada hangi sipariş olduğu saatle daha kolay anlaşılır
    public static string PaketBasligi(string kaynak, DateTime acilis) => $"{kaynak} {acilis:HH:mm}";

    private static string Baslik(Siparis adisyon) => adisyon.Masa?.Ad ?? PaketBasligi(adisyon.Kaynak, adisyon.Tarih);

    private static AdisyonBilgisi Bilgiye(Siparis adisyon)
    {
        var satirlar = adisyon.Satirlar
            .OrderBy(satir => satir.Id == 0 ? int.MaxValue : satir.Id)   // yeni eklenen en altta
            .Select(satir => new SatirBilgisi(satir.Id, satir.UrunId, satir.Urun.Ad, satir.Adet, satir.BirimFiyat, satir.Tutar, satir.IkramMi))
            .ToList();
        var indirim = adisyon.IndirimDegeri > 0
            ? new IndirimBilgisi(adisyon.IndirimYuzdeMi, adisyon.IndirimDegeri, adisyon.IndirimTutari, adisyon.IndirimNedeni)
            : null;
        return new AdisyonBilgisi(adisyon.Id, adisyon.Kaynak, adisyon.MasaId, Baslik(adisyon), adisyon.Tarih, adisyon.Toplam,
            adisyon.KisiSayisi, satirlar, indirim, adisyon.AcanKullanici?.KullaniciAdi);
    }
}
