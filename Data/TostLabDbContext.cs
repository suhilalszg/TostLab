using Microsoft.EntityFrameworkCore;
using TostLab.Models;

namespace TostLab.Data;

public class TostLabDbContext : DbContext
{
    public TostLabDbContext(DbContextOptions<TostLabDbContext> options) : base(options)
    {
    }

    // Her DbSet veritabanında bir tablo olur
    public DbSet<Masa> Masalar => Set<Masa>();
    public DbSet<OdemeTuru> OdemeTurleri => Set<OdemeTuru>();
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<Urun> Urunler => Set<Urun>();
    public DbSet<Siparis> Siparisler => Set<Siparis>();
    public DbSet<SiparisSatiri> SiparisSatirlari => Set<SiparisSatiri>();
    public DbSet<SilinenSatir> SilinenSatirlar => Set<SilinenSatir>();
    public DbSet<PlatformKomisyonu> PlatformKomisyonlari => Set<PlatformKomisyonu>();
    public DbSet<GonderilenRapor> GonderilenRaporlar => Set<GonderilenRapor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Siparişi olan bir masa, ödeme türü veya ürün silinemesin (geçmiş kayıtlar bozulmasın)
        modelBuilder.Entity<Siparis>()
            .HasOne(siparis => siparis.Masa)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Siparis>()
            .HasOne(siparis => siparis.OdemeTuru)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Siparis>()
            .HasOne(siparis => siparis.IptalEdenKullanici)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Siparis>()
            .HasOne(siparis => siparis.IndirimYapanKullanici)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Siparis>()
            .HasOne(siparis => siparis.IadeEdenKullanici)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Siparis>()
            .HasOne(siparis => siparis.AcanKullanici)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SiparisSatiri>()
            .HasOne(satir => satir.IkramEdenKullanici)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SiparisSatiri>()
            .HasOne(satir => satir.Urun)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        // Sipariş silinirse satırları da silinsin
        modelBuilder.Entity<SiparisSatiri>()
            .HasOne(satir => satir.Siparis)
            .WithMany(siparis => siparis.Satirlar)
            .OnDelete(DeleteBehavior.Cascade);

        // Satış ekranı sık sık "açık adisyonları" sorar: bu sorgu hızlı olsun
        modelBuilder.Entity<Siparis>()
            .HasIndex(siparis => new { siparis.Durum, siparis.MasaId });

        // Kayıtlar sayfası günü tarihle süzer
        modelBuilder.Entity<Siparis>()
            .HasIndex(siparis => siparis.Tarih);

        // Silinen ürün izi: kullanıcı silinemez, iz kaybolmasın
        modelBuilder.Entity<SilinenSatir>()
            .HasOne(iz => iz.Kullanici)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SilinenSatir>().HasIndex(iz => iz.Saat);
        modelBuilder.Entity<SilinenSatir>().HasIndex(iz => iz.SiparisId);

        // Zamanlayıcı "bu ayın raporu gitti mi" diye sık sorar
        modelBuilder.Entity<GonderilenRapor>()
            .HasIndex(rapor => new { rapor.Yil, rapor.Ay });

        // Aynı kullanıcı adı iki kez kullanılamasın
        modelBuilder.Entity<Kullanici>()
            .HasIndex(kullanici => kullanici.KullaniciAdi)
            .IsUnique();

        // Başlangıç verisi: program ilk kurulduğunda hazır gelir
        modelBuilder.Entity<Masa>().HasData(
            new Masa { Id = 1, Ad = "İç 1" },
            new Masa { Id = 2, Ad = "İç 2" },
            new Masa { Id = 3, Ad = "İç 3" },
            new Masa { Id = 4, Ad = "İç 4" },
            new Masa { Id = 5, Ad = "Dış 1" });

        // Her platform bir kez; abinin verdiği oran: hepsi %38 (Ayarlar'dan değişebilir)
        modelBuilder.Entity<PlatformKomisyonu>()
            .HasIndex(komisyon => komisyon.Kaynak)
            .IsUnique();
        modelBuilder.Entity<PlatformKomisyonu>().HasData(
            new PlatformKomisyonu { Id = 1, Kaynak = "Yemeksepeti", Oran = 38 },
            new PlatformKomisyonu { Id = 2, Kaynak = "Getir", Oran = 38 },
            new PlatformKomisyonu { Id = 3, Kaynak = "Trendyol", Oran = 38 },
            new PlatformKomisyonu { Id = 4, Kaynak = "Migros", Oran = 38 });

        modelBuilder.Entity<OdemeTuru>().HasData(
            new OdemeTuru { Id = 1, Ad = "Nakit" },
            new OdemeTuru { Id = 2, Ad = "Kart" },
            new OdemeTuru { Id = 3, Ad = "Multinet" },
            new OdemeTuru { Id = 4, Ad = "Pluxee (Sodexo)" },
            new OdemeTuru { Id = 5, Ad = "Edenred (Ticket)" },
            new OdemeTuru { Id = 6, Ad = "Setcard" },
            new OdemeTuru { Id = 7, Ad = "Metropol" });
    }
}
