using System.Text;
using TostLab.Models;

namespace TostLab.Services;

// Raporu WhatsApp'a yapıştırılacak düz yazıya çevirir.
// *yıldız* arası WhatsApp'ta kalın görünür.
// Şimdilik "WhatsApp'ta aç" bağlantısıyla elle gönderiliyor; gece 12'de otomatik göndermek
// WhatsApp Business API ister, o yüzden sonraya bıraktık. Metin aynı kalacak, sadece gönderen değişecek.
public static class RaporMetni
{
    public static string GunSonu(GunRaporu rapor)
    {
        var ozet = rapor.Ozet;
        var yazi = new StringBuilder();
        yazi.AppendLine($"*TOST LAB · Gün sonu*");
        yazi.AppendLine(rapor.Gun.ToString("d MMMM yyyy, dddd"));
        yazi.AppendLine();
        yazi.AppendLine($"*Ciro: {Para(ozet.Ciro)}*");
        yazi.AppendLine($"Adisyon: {ozet.OdenenSayisi} · Ortalama: {Para(rapor.OrtalamaAdisyon)}");
        yazi.AppendLine($"İptal: {ozet.IptalSayisi} ({Para(ozet.IptalTutari)})");
        if (ozet.SilinenUrunAdedi > 0)
        {
            yazi.AppendLine($"Adisyondan silinen: {ozet.SilinenUrunAdedi} ürün ({Para(ozet.SilinenTutar)})");
        }
        if (ozet.AcikSayisi > 0)
        {
            yazi.AppendLine($"⚠ Kapanmamış adisyon: {ozet.AcikSayisi}");
        }

        Kirilim(yazi, "Ödeme", rapor.OdemeKirilimi);
        Kirilim(yazi, "Kaynak", rapor.KaynakKirilimi);

        if (rapor.EnCokSatanlar.Count > 0)
        {
            yazi.AppendLine();
            yazi.AppendLine("*En çok satan*");
            foreach (var urun in rapor.EnCokSatanlar)
            {
                yazi.AppendLine($"{urun.Ad}: {urun.Adet} adet");
            }
        }
        return yazi.ToString().TrimEnd();
    }

    public static string Aylik(AyRaporu rapor)
    {
        var ozet = rapor.Toplam;
        var yazi = new StringBuilder();
        yazi.AppendLine($"*TOST LAB · Aylık özet*");
        yazi.AppendLine(new DateTime(rapor.Yil, rapor.Ay, 1).ToString("MMMM yyyy"));
        yazi.AppendLine();
        yazi.AppendLine($"*Ciro: {Para(ozet.Ciro)}*");
        yazi.AppendLine($"Açık gün: {rapor.Gunler.Count} · Günlük ortalama: {Para(rapor.GunlukOrtalamaCiro)}");
        yazi.AppendLine($"Adisyon: {ozet.OdenenSayisi} · İptal: {ozet.IptalSayisi} ({Para(ozet.IptalTutari)})");

        if (rapor.Gunler.Count > 0)
        {
            var enIyi = rapor.Gunler.MaxBy(gun => gun.Ciro)!;
            yazi.AppendLine($"En iyi gün: {enIyi.Gun:d MMMM} ({Para(enIyi.Ciro)})");
        }

        Kirilim(yazi, "Ödeme", rapor.OdemeKirilimi);
        Kirilim(yazi, "Kaynak", rapor.KaynakKirilimi);
        return yazi.ToString().TrimEnd();
    }

    private static void Kirilim(StringBuilder yazi, string baslik, List<KirilimSatiri> satirlar)
    {
        if (satirlar.Count == 0)
        {
            return;
        }
        yazi.AppendLine();
        yazi.AppendLine($"*{baslik}*");
        foreach (var satir in satirlar)
        {
            yazi.AppendLine($"{satir.Ad}: {Para(satir.Tutar)} ({satir.Adet})");
        }
    }

    private static string Para(decimal tutar) => $"{tutar:N2} ₺";
}
