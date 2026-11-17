using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using TostLab.Models;

namespace TostLab.Services;

// Adisyon panelinin grafikleri: sunucuda SVG olarak çizilir (internet / kütüphane gerekmez).
// Renkler doğrulanmış kategorik paletten sabit sırayla verilir; yazılar seri rengini değil yazı rengini taşır.
// Her parçada <title> var: fareyle üstüne gelince değeri gösterir.
public static class Grafik
{
    // Kategorik palet (açık tema), sabit sıra: mavi, turuncu, aqua, sarı, pembe, yeşil, mor, kırmızı
    public static readonly string[] Renkler = { "#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948" };

    // Sıralı (yoğunluk) rampası: tek renk, açıktan koyuya
    private static readonly string[] Rampa = { "#cde2fb", "#9ec5f4", "#6da7ec", "#3987e5", "#256abf", "#184f95", "#0d366b" };

    private const string Cizgi = "#E7E5DF";      // ızgara
    private const string SoluKYazi = "#8C8A85";
    private const string Yazi = "#141414";

    private static readonly CultureInfo Turkce = new("tr-TR");
    private static readonly CultureInfo Nokta = CultureInfo.InvariantCulture;

    public static string Renk(int sira) => Renkler[sira % Renkler.Length];
    public static string Para(decimal tutar) => tutar.ToString("N0", Turkce) + " ₺";
    private static string S(double sayi) => sayi.ToString("0.##", Nokta);
    private static string E(string yazi) => HtmlEncoder.Default.Encode(yazi);

    // ---------- Halka (donut): ödeme yöntemleri, satış kanalları ----------
    public static IHtmlContent Halka(IReadOnlyList<(string Ad, decimal Deger)> dilimler, string ortaYazi, string ortaAlt)
    {
        const double merkez = 80, yaricap = 62, kalinlik = 22;
        decimal toplam = dilimler.Sum(dilim => dilim.Deger);
        var svg = new StringBuilder($"<svg class=\"grafik halka\" viewBox=\"0 0 160 160\" role=\"img\" aria-label=\"{E(ortaAlt)}\">");
        svg.Append($"<circle cx=\"{merkez}\" cy=\"{merkez}\" r=\"{yaricap}\" fill=\"none\" stroke=\"{Cizgi}\" stroke-width=\"{kalinlik}\"/>");
        if (toplam > 0)
        {
            double cevre = 2 * Math.PI * yaricap;
            double baslangic = 0;
            for (int sira = 0; sira < dilimler.Count; sira++)
            {
                double pay = (double)(dilimler[sira].Deger / toplam);
                if (pay <= 0) continue;
                // 2px zemin boşluğu: dilimler birbirine yapışmasın
                double uzunluk = Math.Max(pay * cevre - (dilimler.Count > 1 ? 2 : 0), 0.5);
                svg.Append($"<circle cx=\"{merkez}\" cy=\"{merkez}\" r=\"{yaricap}\" fill=\"none\" stroke=\"{Renk(sira)}\" stroke-width=\"{kalinlik}\" " +
                           $"stroke-dasharray=\"{S(uzunluk)} {S(cevre)}\" stroke-dashoffset=\"{S(-baslangic)}\" transform=\"rotate(-90 {merkez} {merkez})\">" +
                           $"<title>{E(dilimler[sira].Ad)}: {Para(dilimler[sira].Deger)} (%{(pay * 100).ToString("0", Turkce)})</title></circle>");
                baslangic += pay * cevre;
            }
        }
        svg.Append($"<text x=\"80\" y=\"78\" text-anchor=\"middle\" font-size=\"17\" font-weight=\"800\" fill=\"{Yazi}\">{E(ortaYazi)}</text>");
        svg.Append($"<text x=\"80\" y=\"96\" text-anchor=\"middle\" font-size=\"10\" fill=\"{SoluKYazi}\">{E(ortaAlt)}</text>");
        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    // ---------- Isı haritası: haftanın günü x saat ----------
    public static IHtmlContent IsiHaritasi(int[][] matris)
    {
        string[] gunAdlari = { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
        var doluSaatler = Enumerable.Range(0, 24).Where(saat => matris.Any(gun => gun[saat] > 0)).ToList();
        // Az veri olan günlerde de harita okunur genişlikte kalsın: en az 10:00–22:00 aralığı
        int ilk = doluSaatler.Count == 0 ? 10 : Math.Min(10, doluSaatler.Min());
        int son = doluSaatler.Count == 0 ? 22 : Math.Max(22, doluSaatler.Max());
        int enCok = Math.Max(1, matris.SelectMany(gun => gun).Max());
        const double hucre = 26, bosluk = 2, sol = 34, ust = 4;
        int saatSayisi = son - ilk + 1;
        double genislik = sol + saatSayisi * (hucre + bosluk), yukseklik = ust + 7 * (hucre + bosluk) + 16;

        var svg = new StringBuilder($"<svg class=\"grafik isi\" width=\"{S(genislik)}\" height=\"{S(yukseklik)}\" viewBox=\"0 0 {S(genislik)} {S(yukseklik)}\" role=\"img\" aria-label=\"Gün ve saat yoğunluğu\">");
        for (int gun = 0; gun < 7; gun++)
        {
            double y = ust + gun * (hucre + bosluk);
            svg.Append($"<text x=\"{sol - 6}\" y=\"{S(y + hucre / 2 + 3.5)}\" text-anchor=\"end\" font-size=\"10\" fill=\"{SoluKYazi}\">{gunAdlari[gun]}</text>");
            for (int saat = ilk; saat <= son; saat++)
            {
                int adet = matris[gun][saat];
                double x = sol + (saat - ilk) * (hucre + bosluk);
                string renk = adet == 0 ? "#F3F2EE" : Rampa[Math.Min(Rampa.Length - 1, (int)Math.Ceiling((double)adet / enCok * Rampa.Length) - 1)];
                svg.Append($"<rect x=\"{S(x)}\" y=\"{S(y)}\" width=\"{hucre}\" height=\"{hucre}\" rx=\"4\" fill=\"{renk}\">" +
                           $"<title>{gunAdlari[gun]} {saat:00}:00 · {adet} adisyon</title></rect>");
            }
        }
        for (int saat = ilk; saat <= son; saat++)
        {
            double x = sol + (saat - ilk) * (hucre + bosluk) + hucre / 2;
            svg.Append($"<text x=\"{S(x)}\" y=\"{S(yukseklik - 3)}\" text-anchor=\"middle\" font-size=\"10\" fill=\"{SoluKYazi}\">{saat}</text>");
        }
        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    // Isı haritası açıklaması için rampanın renkleri
    public static IReadOnlyList<string> RampaRenkleri => Rampa;

    // ---------- Yığılmış çubuk: gün gün kategori cirosu ----------
    public static IHtmlContent YiginCubuk(IReadOnlyList<KategoriDilimi> gunler, IReadOnlyList<string> kategoriler, bool haftaMi)
    {
        const double yukseklik = 200, sol = 46, ust = 10, alt = 22, sag = 8;
        double genislik = Math.Max(320, sol + sag + gunler.Count * 22);
        double cizimG = genislik - sol - sag, cizimY = yukseklik - ust - alt;
        decimal enBuyuk = Math.Max(1, gunler.Select(gun => gun.Kategoriler.Values.Sum()).DefaultIfEmpty(0).Max());
        decimal olcek = GuzelUstSinir(enBuyuk);
        double adim = cizimG / Math.Max(1, gunler.Count);
        double cubukG = Math.Min(28, adim * 0.62);

        var svg = new StringBuilder($"<svg class=\"grafik yigin\" viewBox=\"0 0 {S(genislik)} {yukseklik}\" role=\"img\" aria-label=\"Kategori bazlı satış\">");
        for (int cizgi = 0; cizgi <= 4; cizgi++)
        {
            double y = ust + cizimY - cizimY * cizgi / 4;
            svg.Append($"<line x1=\"{sol}\" x2=\"{S(genislik - sag)}\" y1=\"{S(y)}\" y2=\"{S(y)}\" stroke=\"{Cizgi}\"/>");
            svg.Append($"<text x=\"{sol - 6}\" y=\"{S(y + 3.5)}\" text-anchor=\"end\" font-size=\"10\" fill=\"{SoluKYazi}\">{KisaSayi(olcek * cizgi / 4)}</text>");
        }
        for (int sira = 0; sira < gunler.Count; sira++)
        {
            var gun = gunler[sira];
            double x = sol + adim * sira + (adim - cubukG) / 2;
            double tabani = ust + cizimY;
            for (int kategori = 0; kategori < kategoriler.Count; kategori++)
            {
                if (!gun.Kategoriler.TryGetValue(kategoriler[kategori], out decimal tutar) || tutar <= 0) continue;
                double boy = (double)(tutar / olcek) * cizimY;
                // Parçalar arasında 2px zemin boşluğu
                double cizilen = Math.Max(boy - 2, 0.8);
                svg.Append($"<rect x=\"{S(x)}\" y=\"{S(tabani - cizilen)}\" width=\"{S(cubukG)}\" height=\"{S(cizilen)}\" rx=\"2\" fill=\"{Renk(kategori)}\">" +
                           $"<title>{gun.Gun.ToString("d MMMM", Turkce)} · {E(kategoriler[kategori])}: {Para(tutar)}</title></rect>");
                tabani -= boy;
            }
            if (gunler.Count <= 16 || sira % 2 == 0)
            {
                string etiket = haftaMi ? gun.Gun.ToString("ddd", Turkce) : gun.Gun.Day.ToString();
                svg.Append($"<text x=\"{S(x + cubukG / 2)}\" y=\"{yukseklik - 7}\" text-anchor=\"middle\" font-size=\"10\" fill=\"{SoluKYazi}\">{E(etiket)}</text>");
            }
        }
        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    // ---------- İbre (gauge): aylık hedefin yüzde kaçı ----------
    public static IHtmlContent Ibre(decimal? oran, string altYazi)
    {
        const double merkezX = 110, merkezY = 104, yaricap = 84, kalinlik = 16;
        const double enFazla = 120;   // %120'ye kadar çizilir; %100 çentikle işaretli
        double doluluk = Math.Clamp((double)(oran ?? 0), 0, enFazla) / enFazla;

        string Yay(double baslangic, double bitis)
        {
            double aci1 = Math.PI * (1 - baslangic), aci2 = Math.PI * (1 - bitis);
            double x1 = merkezX + yaricap * Math.Cos(aci1), y1 = merkezY - yaricap * Math.Sin(aci1);
            double x2 = merkezX + yaricap * Math.Cos(aci2), y2 = merkezY - yaricap * Math.Sin(aci2);
            return $"M {S(x1)} {S(y1)} A {yaricap} {yaricap} 0 0 1 {S(x2)} {S(y2)}";
        }

        var svg = new StringBuilder("<svg class=\"grafik ibre\" viewBox=\"0 0 220 130\" role=\"img\" aria-label=\"Aylık hedef\">");
        svg.Append($"<path d=\"{Yay(0, 1)}\" fill=\"none\" stroke=\"{Cizgi}\" stroke-width=\"{kalinlik}\" stroke-linecap=\"round\"/>");
        if (doluluk > 0)
        {
            svg.Append($"<path d=\"{Yay(0, Math.Max(doluluk, 0.01))}\" fill=\"none\" stroke=\"{Renkler[0]}\" stroke-width=\"{kalinlik}\" stroke-linecap=\"round\"/>");
        }
        // %100 çentiği
        double yuzAci = Math.PI * (1 - 100 / enFazla);
        svg.Append($"<line x1=\"{S(merkezX + (yaricap - 13) * Math.Cos(yuzAci))}\" y1=\"{S(merkezY - (yaricap - 13) * Math.Sin(yuzAci))}\" " +
                   $"x2=\"{S(merkezX + (yaricap + 13) * Math.Cos(yuzAci))}\" y2=\"{S(merkezY - (yaricap + 13) * Math.Sin(yuzAci))}\" stroke=\"{Yazi}\" stroke-width=\"2\"/>");
        string yuzde = oran is decimal deger ? "%" + deger.ToString("0", Turkce) : "—";
        svg.Append($"<text x=\"{merkezX}\" y=\"{merkezY - 12}\" text-anchor=\"middle\" font-size=\"26\" font-weight=\"800\" fill=\"{Yazi}\">{yuzde}</text>");
        svg.Append($"<text x=\"{merkezX}\" y=\"{merkezY + 10}\" text-anchor=\"middle\" font-size=\"10\" fill=\"{SoluKYazi}\">{E(altYazi)}</text>");
        svg.Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    // ---------- Yardımcılar ----------

    // Eksen için yuvarlak üst sınır: 1, 2, 2.5, 5 x 10^n
    private static decimal GuzelUstSinir(decimal deger)
    {
        if (deger <= 0) return 1;
        double us = Math.Pow(10, Math.Floor(Math.Log10((double)deger)));
        foreach (double carpan in new[] { 1, 2, 2.5, 5, 10 })
        {
            if (carpan * us >= (double)deger) return (decimal)(carpan * us);
        }
        return deger;
    }

    // 12500 -> "12,5b", 800 -> "800"
    public static string KisaSayi(decimal deger) =>
        Math.Abs(deger) >= 1000 ? (deger / 1000).ToString("0.#", Turkce) + "b" : deger.ToString("0", Turkce);
}
