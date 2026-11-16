namespace TostLab.Services;

// Ürün fotoğraflarını diskte tutar. Klasör wwwroot dışında (varsayılan: proje yanında "urun-foto"),
// çünkü wwwroot'taki dosyalar derlemede paketlenir; dükkanda sonradan yüklenen dosyayı görmez.
// Program.cs bu klasörü /urun-foto adresinden sunar. Klasör git'e girmez.
public class UrunFotograflari
{
    public const string Adres = "/urun-foto";
    public const long EnBuyukBoyut = 3 * 1024 * 1024;   // 3 MB: telefon fotoğrafı için yeter

    public string Klasor { get; }

    public UrunFotograflari(IConfiguration ayarlar, IWebHostEnvironment ortam)
    {
        string yol = ayarlar["UrunFotoKlasoru"] ?? "urun-foto";
        Klasor = Path.GetFullPath(Path.Combine(ortam.ContentRootPath, yol));
        Directory.CreateDirectory(Klasor);
    }

    public static string? AdresiBul(string? dosyaAdi) => dosyaAdi == null ? null : $"{Adres}/{dosyaAdi}";

    // Dosyanın gerçekten resim olduğu ilk baytlarından anlaşılır (uzantıya güvenilmez).
    // Dönen: kaydedilen dosya adı ("12-a1b2c3.jpg")
    public async Task<string> KaydetAsync(int urunId, IFormFile dosya)
    {
        if (dosya.Length == 0)
        {
            throw new InvalidOperationException("Dosya boş.");
        }
        if (dosya.Length > EnBuyukBoyut)
        {
            throw new InvalidOperationException("Fotoğraf en fazla 3 MB olabilir.");
        }

        using var bellek = new MemoryStream();
        await dosya.CopyToAsync(bellek);
        byte[] baytlar = bellek.ToArray();

        string uzanti = UzantiyiBul(baytlar)
            ?? throw new InvalidOperationException("Sadece JPG, PNG ya da WEBP yüklenebilir.");

        // Adın sonuna rastgele parça: tarayıcı eski fotoğrafı önbellekten göstermesin
        string dosyaAdi = $"{urunId}-{Guid.NewGuid().ToString("N")[..8]}{uzanti}";
        await File.WriteAllBytesAsync(Path.Combine(Klasor, dosyaAdi), baytlar);
        return dosyaAdi;
    }

    public void Sil(string? dosyaAdi)
    {
        if (string.IsNullOrEmpty(dosyaAdi))
        {
            return;
        }
        // Sadece klasörün içindeki dosya silinir ("../" gibi bir ad gelse bile dışarı çıkmaz)
        string yol = Path.Combine(Klasor, Path.GetFileName(dosyaAdi));
        if (File.Exists(yol))
        {
            File.Delete(yol);
        }
    }

    private static string? UzantiyiBul(byte[] baytlar)
    {
        if (baytlar.Length < 12)
        {
            return null;
        }
        if (baytlar[0] == 0xFF && baytlar[1] == 0xD8 && baytlar[2] == 0xFF)
        {
            return ".jpg";
        }
        if (baytlar[0] == 0x89 && baytlar[1] == 0x50 && baytlar[2] == 0x4E && baytlar[3] == 0x47)
        {
            return ".png";
        }
        // RIFF....WEBP
        if (baytlar[0] == 'R' && baytlar[1] == 'I' && baytlar[2] == 'F' && baytlar[3] == 'F'
            && baytlar[8] == 'W' && baytlar[9] == 'E' && baytlar[10] == 'B' && baytlar[11] == 'P')
        {
            return ".webp";
        }
        return null;
    }
}
