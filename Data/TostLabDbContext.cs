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

        modelBuilder.Entity<SiparisSatiri>()
            .HasOne(satir => satir.Urun)
            .WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        // Sipariş silinirse satırları da silinsin
        modelBuilder.Entity<SiparisSatiri>()
            .HasOne(satir => satir.Siparis)
            .WithMany(siparis => siparis.Satirlar)
            .OnDelete(DeleteBehavior.Cascade);

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
