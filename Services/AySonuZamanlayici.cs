namespace TostLab.Services;

// Arka planda, program açık olduğu sürece çalışır (BackgroundService).
// Yarım saatte bir "yeni ay başladı mı, geçen ayın raporu gitti mi?" diye bakar.
// Ayın 1'i gece 00:00'da bilgisayar kapalıysa sorun yok: açıldığı ilk kontrolde gönderir.
//
// Neden scope açıyoruz: bu sınıf program boyunca tek (singleton) yaşar,
// ama DbContext her iş için yeni olmalı (scoped). O yüzden her kontrolde yeni bir kapsam açılır.
public class AySonuZamanlayici : BackgroundService
{
    private static readonly TimeSpan KontrolAraligi = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory kapsamFabrikasi;
    private readonly ILogger<AySonuZamanlayici> gunluk;

    public AySonuZamanlayici(IServiceScopeFactory kapsamFabrikasi, ILogger<AySonuZamanlayici> gunluk)
    {
        this.kapsamFabrikasi = kapsamFabrikasi;
        this.gunluk = gunluk;
    }

    protected override async Task ExecuteAsync(CancellationToken durdurma)
    {
        // Program açılışı (migration vb.) bitsin
        await Task.Delay(TimeSpan.FromSeconds(20), durdurma);

        using var zamanlayici = new PeriodicTimer(KontrolAraligi);
        do
        {
            await KontrolEtAsync();
        }
        while (await zamanlayici.WaitForNextTickAsync(durdurma));
    }

    private async Task KontrolEtAsync()
    {
        try
        {
            using var kapsam = kapsamFabrikasi.CreateScope();
            var gonderme = kapsam.ServiceProvider.GetRequiredService<RaporGondermeServisi>();
            if (await gonderme.GecenAyiGonderAsync(DateTime.Today))
            {
                gunluk.LogInformation("Geçen ayın raporu e-postayla gönderildi.");
            }
        }
        catch (Exception hata)
        {
            // İnternet yoksa vb.: programı düşürme, yarım saat sonra tekrar dene
            gunluk.LogWarning(hata, "Aylık rapor gönderilemedi, sonra tekrar denenecek.");
        }
    }
}
