using System.Net;
using System.Text;
using TostLab.Models;

namespace TostLab.Services;

// Aylık raporu e-posta gövdesine (HTML) çevirir.
// E-posta programları dış CSS dosyası okumaz: stiller her etiketin içine (inline) yazılır.
// Ürün/ödeme adları WebUtility.HtmlEncode'dan geçer: adında < & olan bir şey HTML'i bozmasın.
public static class RaporEpostasi
{
    private const string Hucre = "padding:6px 10px;border-bottom:1px solid #eee;";
    private const string SagHucre = Hucre + "text-align:right;";

    public static string Konu(AyRaporu rapor) =>
        $"TOST LAB · {new DateTime(rapor.Yil, rapor.Ay, 1):MMMM yyyy} aylık rapor";

    public static string Aylik(AyRaporu rapor)
    {
        var ozet = rapor.Toplam;
        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#1c1c1c;max-width:640px\">");
        html.Append($"<h2 style=\"margin:0 0 4px\">TOST LAB · Aylık rapor</h2>");
        html.Append($"<div style=\"color:#8a857b;margin-bottom:16px\">{new DateTime(rapor.Yil, rapor.Ay, 1):MMMM yyyy}</div>");

        // Öne çıkan rakamlar
        html.Append("<table style=\"border-collapse:collapse;width:100%;margin-bottom:18px\">");
        Satir(html, "<b>Ciro</b>", $"<b>{Para(ozet.Ciro)}</b>");
        if (ozet.Komisyon > 0)
        {
            Satir(html, "Platform komisyonu", "−" + Para(ozet.Komisyon));
            Satir(html, "<b>Net kazanç</b>", $"<b>{Para(ozet.NetKazanc)}</b>");
        }
        if (ozet.IndirimToplami > 0)
        {
            Satir(html, "Yapılan indirim", Para(ozet.IndirimToplami));
        }
        if (ozet.IkramAdedi > 0)
        {
            Satir(html, "İkram", $"{ozet.IkramAdedi} ürün ({Para(ozet.IkramTutari)})");
        }
        if (ozet.IadeSayisi > 0)
        {
            Satir(html, "İade", $"{ozet.IadeSayisi} ({Para(ozet.IadeTutari)})");
        }
        Satir(html, "Açık gün", rapor.Gunler.Count.ToString());
        Satir(html, "Günlük ortalama ciro", Para(rapor.GunlukOrtalamaCiro));
        Satir(html, "Adisyon", ozet.OdenenSayisi.ToString());
        Satir(html, "İptal", $"{ozet.IptalSayisi} ({Para(ozet.IptalTutari)})");
        if (ozet.SilinenUrunAdedi > 0)
        {
            Satir(html, "Adisyondan silinen ürün", $"{ozet.SilinenUrunAdedi} ({Para(ozet.SilinenTutar)})");
        }
        html.Append("</table>");

        Kirilim(html, "Ödeme türüne göre", rapor.OdemeKirilimi);
        Kirilim(html, "Kaynağa göre", rapor.KaynakKirilimi);

        // Gün gün
        if (rapor.Gunler.Count > 0)
        {
            html.Append("<h3 style=\"margin:18px 0 6px\">Günler</h3>");
            html.Append("<table style=\"border-collapse:collapse;width:100%\">");
            html.Append($"<tr style=\"color:#8a857b\"><td style=\"{Hucre}\">Gün</td><td style=\"{SagHucre}\">Adisyon</td><td style=\"{SagHucre}\">İptal</td><td style=\"{SagHucre}\">Ciro</td></tr>");
            foreach (var gun in rapor.Gunler)
            {
                html.Append($"<tr><td style=\"{Hucre}\">{gun.Gun:d MMM ddd}</td><td style=\"{SagHucre}\">{gun.OdenenSayisi}</td>");
                html.Append($"<td style=\"{SagHucre}\">{gun.IptalSayisi}</td><td style=\"{SagHucre}\">{Para(gun.Ciro)}</td></tr>");
            }
            html.Append("</table>");
        }

        html.Append($"<p style=\"color:#8a857b;font-size:12px;margin-top:20px\">TOST LAB panelinden gönderildi · {DateTime.Now:d MMMM yyyy HH:mm}</p>");
        html.Append("</div>");
        return html.ToString();
    }

    private static void Satir(StringBuilder html, string ad, string deger) =>
        html.Append($"<tr><td style=\"{Hucre}\">{ad}</td><td style=\"{SagHucre}\">{deger}</td></tr>");

    private static void Kirilim(StringBuilder html, string baslik, List<KirilimSatiri> satirlar)
    {
        if (satirlar.Count == 0)
        {
            return;
        }
        html.Append($"<h3 style=\"margin:18px 0 6px\">{baslik}</h3>");
        html.Append("<table style=\"border-collapse:collapse;width:100%\">");
        foreach (var satir in satirlar)
        {
            string komisyon = satir.Komisyon > 0 ? $" <span style=\"color:#8a857b\">(kom. −{Para(satir.Komisyon)})</span>" : "";
            html.Append($"<tr><td style=\"{Hucre}\">{WebUtility.HtmlEncode(satir.Ad)} <span style=\"color:#8a857b\">· {satir.Adet}</span></td>");
            html.Append($"<td style=\"{SagHucre}\">{Para(satir.Tutar)}{komisyon}</td></tr>");
        }
        html.Append("</table>");
    }

    private static string Para(decimal tutar) => $"{tutar:N2} ₺";
}
