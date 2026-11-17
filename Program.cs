using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;
using TostLab.Services;

var builder = WebApplication.CreateBuilder(args);

// E-posta ayarları (şifre içerir): git'e girmeyen eposta.json'dan. Dosya yoksa e-posta kapalı kalır.
// Komut satırı sonra tekrar eklenir ki --Eposta:... ile verilen değer dosyadakini ezsin.
builder.Configuration.AddJsonFile("eposta.json", optional: true, reloadOnChange: true);
builder.Configuration.AddCommandLine(args);
builder.Services.Configure<EpostaAyarlari>(builder.Configuration.GetSection("Eposta"));

builder.Services.AddControllersWithViews(options =>
{
    // Giriş yapmamış kimse hiçbir sayfayı açamasın (açık sayfalarda [AllowAnonymous] var)
    var girisZorunlu = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(girisZorunlu));

    // Hazır İngilizce hata mesajlarını Türkçe yap
    var mesajlar = options.ModelBindingMessageProvider;
    mesajlar.SetAttemptedValueIsInvalidAccessor((deger, alan) => $"'{deger}' geçerli değil. {alan} için sayı yaz.");
    mesajlar.SetValueIsInvalidAccessor(deger => $"'{deger}' geçerli değil.");
    mesajlar.SetValueMustNotBeNullAccessor(alan => $"{alan} boş olamaz.");
    mesajlar.SetMissingBindRequiredValueAccessor(alan => $"{alan} boş olamaz.");
});

// Veritabanı: SQLite, bağlantı bilgisi appsettings.json içinde
builder.Services.AddDbContext<TostLabDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TostLab")));

// Giriş: çerez (cookie) ile oturum
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Giris";
        options.LogoutPath = "/Giris/Cikis";
        options.AccessDeniedPath = "/Giris/Yetkisiz";
        options.ReturnUrlParameter = "donusAdresi";
        options.ExpireTimeSpan = TimeSpan.FromHours(14);
        options.SlidingExpiration = true;
    });

// Şifreleri hash'leyen hazır araç (ASP.NET Core Identity'den)
builder.Services.AddSingleton<IPasswordHasher<Kullanici>, PasswordHasher<Kullanici>>();

// Adisyon kuralları: her istekte veritabanıyla birlikte yeni bir tane oluşur (scoped)
builder.Services.AddScoped<AdisyonServisi>();
// Raporlar (Adisyon paneli, aylık e-posta): sadece okur
builder.Services.AddScoped<RaporServisi>();
// Fiş kayıtları: ödenen adisyonların fişleri (Fişler ekranı)
builder.Services.AddScoped<FisServisi>();
// Aylık rapor e-postası: hazırla + gönder + kaydet; ay bitince arka planda zamanlayıcı tetikler
builder.Services.AddScoped<EpostaServisi>();
builder.Services.AddScoped<RaporGondermeServisi>();
builder.Services.AddHostedService<AySonuZamanlayici>();
// Ürün fotoğrafları: Menü'den yüklenir, diskte durur
builder.Services.AddSingleton<UrunFotograflari>();

// PDF rapor kütüphanesi: yıllık cirosu 1 milyon doları geçmeyen işletmeye ücretsiz (Community lisansı)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

// Program açılırken veritabanı yoksa oluştur, yeni migration varsa uygula
using (var kapsam = app.Services.CreateScope())
{
    var veritabani = kapsam.ServiceProvider.GetRequiredService<TostLabDbContext>();
    veritabani.Database.Migrate();
    BaslangicMenusu.BossaYukle(veritabani);   // ürün yoksa abinin menüsünü yükle
    FisServisi.EksikFisleriOlustur(veritabani);  // fiş kaydından önce ödenmiş adisyonların fişleri (bir kez)
}

// Türkçe biçim: fiyat "92,50" diye yazılır, tarih "13 Kasım" diye görünür
var turkce = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkce;
CultureInfo.DefaultThreadCurrentUICulture = turkce;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkce),
    SupportedCultures = new[] { turkce },
    SupportedUICultures = new[] { turkce }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Yüklenen ürün fotoğrafları: /urun-foto/12-ab12cd34.jpg
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        app.Services.GetRequiredService<UrunFotograflari>().Klasor),
    RequestPath = UrunFotograflari.Adres
});

app.UseRouting();

app.UseAuthentication();   // 1) Sen kimsin? (çerezi oku)
app.UseAuthorization();    // 2) Buraya girebilir misin? (rolü kontrol et)

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
