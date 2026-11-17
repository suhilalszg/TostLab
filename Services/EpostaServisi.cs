using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace TostLab.Services;

// eposta.json içindeki "Eposta" bölümü buraya dolar (Options kalıbı).
// eposta.json git'e girmez: içinde Gmail uygulama şifresi var. Örnek: eposta.ornek.json
public class EpostaAyarlari
{
    public string SmtpSunucu { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;

    // Gönderen hesap ve onun uygulama şifresi (Gmail > Güvenlik > Uygulama şifreleri)
    public string KullaniciAdi { get; set; } = "";
    public string Sifre { get; set; } = "";

    // Raporun gideceği adres (abi)
    public string Alici { get; set; } = "";

    // Doluysa e-posta gönderilmez, .eml dosyası olarak bu klasöre yazılır (deneme için)
    public string? DenemeKlasoru { get; set; }

    public bool DenemeMi => !string.IsNullOrWhiteSpace(DenemeKlasoru);

    public bool HazirMi => DenemeMi
        || (KullaniciAdi != "" && Sifre != "" && Alici != "");
}

public class EpostaHatasi(string mesaj) : Exception(mesaj);

public class EpostaServisi
{
    private readonly EpostaAyarlari ayarlar;

    public EpostaServisi(IOptions<EpostaAyarlari> secenekler)
    {
        ayarlar = secenekler.Value;
    }

    public bool HazirMi => ayarlar.HazirMi;
    public string Alici => ayarlar.DenemeMi ? "deneme klasörü" : ayarlar.Alici;

    // ekAdi + ekIcerigi verilirse dosya (ör. PDF rapor) e-postaya eklenir
    public async Task GonderAsync(string konu, string htmlGovde, string? ekAdi = null, byte[]? ekIcerigi = null)
    {
        if (!ayarlar.HazirMi)
        {
            throw new EpostaHatasi("E-posta ayarları yapılmamış. eposta.ornek.json dosyasını eposta.json olarak kopyalayıp doldur.");
        }

        string gonderen = ayarlar.KullaniciAdi != "" ? ayarlar.KullaniciAdi : "tostlab@localhost";
        string alici = ayarlar.Alici != "" ? ayarlar.Alici : "deneme@localhost";

        using var mesaj = new MailMessage(new MailAddress(gonderen, "TOST LAB"), new MailAddress(alici))
        {
            Subject = konu,
            Body = htmlGovde,
            IsBodyHtml = true,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        if (ekAdi != null && ekIcerigi != null)
        {
            // MailMessage kapanınca ek ve akışı da kapanır
            mesaj.Attachments.Add(new Attachment(new MemoryStream(ekIcerigi), ekAdi, "application/pdf"));
        }

        using var istemci = new SmtpClient();
        if (ayarlar.DenemeMi)
        {
            string klasor = Path.GetFullPath(ayarlar.DenemeKlasoru!);
            Directory.CreateDirectory(klasor);
            istemci.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            istemci.PickupDirectoryLocation = klasor;
        }
        else
        {
            istemci.Host = ayarlar.SmtpSunucu;
            istemci.Port = ayarlar.Port;
            istemci.EnableSsl = true;   // Gmail 587: STARTTLS
            istemci.Credentials = new NetworkCredential(ayarlar.KullaniciAdi, ayarlar.Sifre);
        }

        try
        {
            await istemci.SendMailAsync(mesaj);
        }
        catch (SmtpException hata)
        {
            throw new EpostaHatasi("E-posta gönderilemedi: " + hata.Message);
        }
    }
}
