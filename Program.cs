using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using TostLab.Data;
using TostLab.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Giriş yapmamış kimse hiçbir sayfayı açamasın (açık sayfalarda [AllowAnonymous] var)
    var girisZorunlu = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(girisZorunlu));
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

var app = builder.Build();

// Program açılırken veritabanı yoksa oluştur, yeni migration varsa uygula
using (var kapsam = app.Services.CreateScope())
{
    kapsam.ServiceProvider.GetRequiredService<TostLabDbContext>().Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();   // 1) Sen kimsin? (çerezi oku)
app.UseAuthorization();    // 2) Buraya girebilir misin? (rolü kontrol et)

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
