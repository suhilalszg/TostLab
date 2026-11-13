using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TostLab.Models;

namespace TostLab.Services;

public static class OturumServisi
{
    // Kullanıcının kimlik kartını (claim) hazırlar ve tarayıcıya çerez olarak verir
    public static async Task OturumAcAsync(HttpContext istek, Kullanici kullanici)
    {
        var bilgiler = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, kullanici.Id.ToString()),
            new Claim(ClaimTypes.Name, kullanici.KullaniciAdi),
            new Claim(ClaimTypes.Role, kullanici.Rol)
        };

        var kimlik = new ClaimsIdentity(bilgiler, CookieAuthenticationDefaults.AuthenticationScheme);
        await istek.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(kimlik));
    }
}
