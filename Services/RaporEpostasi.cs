using System.Text;
using TostLab.Models;

namespace TostLab.Services;

// Aylık rapor e-postasının konusu ve kısa gövdesi. Raporun kendisi ekteki PDF'te (RaporPdf).
// E-posta programları dış CSS dosyası okumaz: stiller etiketin içine (inline) yazılır.
public static class RaporEpostasi
{
    public static string Konu(AyRaporu rapor) =>
        $"TOST LAB · {new DateTime(rapor.Yil, rapor.Ay, 1):MMMM yyyy} aylık rapor";

    public static string Aylik(AyRaporu rapor)
    {
        var ozet = rapor.Toplam;
        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#1c1c1c;max-width:640px\">");
        html.Append($"<h2 style=\"margin:0 0 4px\">TOST LAB · {new DateTime(rapor.Yil, rapor.Ay, 1):MMMM yyyy}</h2>");
        html.Append($"<p style=\"margin:12px 0\">Ciro: <b>{Para(ozet.Ciro)}</b>");
        if (ozet.Komisyon > 0)
        {
            html.Append($" · Net kazanç: <b>{Para(ozet.NetKazanc)}</b>");
        }
        html.Append($" · {ozet.OdenenSayisi} adisyon</p>");
        html.Append("<p style=\"margin:12px 0\">Ayrıntılı rapor ekteki PDF dosyasında.</p>");
        html.Append($"<p style=\"color:#8a857b;font-size:12px;margin-top:20px\">TOST LAB panelinden gönderildi · {DateTime.Now:d MMMM yyyy HH:mm}</p>");
        html.Append("</div>");
        return html.ToString();
    }

    private static string Para(decimal tutar) => $"{tutar:N2} ₺";
}
