using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TostLab.Models;

namespace TostLab.Services;

// Aylık raporun PDF hâli: e-postaya ek olarak gider. İçerik e-posta gövdesiyle aynı sırada:
// özet, ödeme kırılımı, kaynağa göre, gün gün.
public static class RaporPdf
{
    private static readonly CultureInfo Turkce = new("tr-TR");
    private const string Koyu = "#1c1c1c";
    private const string Soluk = "#8a857b";
    private const string Cizgi = "#e6e2da";
    private const string Vurgu = "#f5f2ec";

    public static string DosyaAdi(AyRaporu rapor) => $"TostLab-Aylik-Rapor-{rapor.Yil:0000}-{rapor.Ay:00}.pdf";

    public static byte[] Olustur(AyRaporu rapor)
    {
        var ozet = rapor.Toplam;
        string donem = new DateTime(rapor.Yil, rapor.Ay, 1).ToString("MMMM yyyy", Turkce);

        return Document.Create(belge =>
        {
            belge.Page(sayfa =>
            {
                sayfa.Size(PageSizes.A4);
                sayfa.Margin(36);
                sayfa.DefaultTextStyle(yazi => yazi.FontSize(10).FontColor(Koyu));

                sayfa.Header().Column(baslik =>
                {
                    baslik.Item().Text("TOST LAB · Aylık rapor").FontSize(18).Bold();
                    baslik.Item().Text(donem).FontColor(Soluk);
                    baslik.Item().PaddingTop(8).LineHorizontal(1).LineColor(Cizgi);
                });

                sayfa.Content().PaddingTop(12).Column(icerik =>
                {
                    icerik.Spacing(16);

                    // Öne çıkan rakamlar: e-postadaki gibi, olmayan kalem hiç yazılmaz
                    var satirlar = new List<(string Ad, string Deger, bool KalinMi)>
                    {
                        ("Ciro", Para(ozet.Ciro), true)
                    };
                    if (ozet.Komisyon > 0)
                    {
                        satirlar.Add(("Platform komisyonu", "−" + Para(ozet.Komisyon), false));
                        satirlar.Add(("Net kazanç", Para(ozet.NetKazanc), true));
                    }
                    if (ozet.IndirimToplami > 0) satirlar.Add(("Yapılan indirim", Para(ozet.IndirimToplami), false));
                    if (ozet.IkramAdedi > 0) satirlar.Add(("İkram", $"{ozet.IkramAdedi} ürün ({Para(ozet.IkramTutari)})", false));
                    if (ozet.IadeSayisi > 0) satirlar.Add(("İade", $"{ozet.IadeSayisi} ({Para(ozet.IadeTutari)})", false));
                    satirlar.Add(("Açık gün", rapor.Gunler.Count.ToString(), false));
                    satirlar.Add(("Günlük ortalama ciro", Para(rapor.GunlukOrtalamaCiro), false));
                    satirlar.Add(("Adisyon", ozet.OdenenSayisi.ToString(), false));
                    satirlar.Add(("İptal", $"{ozet.IptalSayisi} ({Para(ozet.IptalTutari)})", false));
                    if (ozet.SilinenUrunAdedi > 0) satirlar.Add(("Adisyondan silinen ürün", $"{ozet.SilinenUrunAdedi} ({Para(ozet.SilinenTutar)})", false));

                    icerik.Item().Table(tablo =>
                    {
                        tablo.ColumnsDefinition(sutun => { sutun.RelativeColumn(); sutun.ConstantColumn(170); });
                        foreach (var (ad, deger, kalinMi) in satirlar)
                        {
                            var adHucresi = tablo.Cell().Element(Hucre).Text(ad);
                            var degerHucresi = tablo.Cell().Element(Hucre).AlignRight().Text(deger);
                            if (kalinMi) { adHucresi.Bold(); degerHucresi.Bold(); }
                        }
                    });

                    Kirilim(icerik, "Ödeme", rapor.OdemeKirilimi);
                    Kirilim(icerik, "Kaynağa göre", rapor.KaynakKirilimi);

                    if (rapor.Gunler.Count > 0)
                    {
                        icerik.Item().Column(bolum =>
                        {
                            bolum.Item().PaddingBottom(4).Text("Günler").FontSize(12).Bold();
                            bolum.Item().Table(tablo =>
                            {
                                tablo.ColumnsDefinition(sutun =>
                                {
                                    sutun.RelativeColumn();
                                    sutun.ConstantColumn(70);
                                    sutun.ConstantColumn(60);
                                    sutun.ConstantColumn(110);
                                });
                                tablo.Header(baslik =>
                                {
                                    baslik.Cell().Element(BaslikHucresi).Text("Gün");
                                    baslik.Cell().Element(BaslikHucresi).AlignRight().Text("Adisyon");
                                    baslik.Cell().Element(BaslikHucresi).AlignRight().Text("İptal");
                                    baslik.Cell().Element(BaslikHucresi).AlignRight().Text("Ciro");
                                });
                                foreach (var gun in rapor.Gunler)
                                {
                                    tablo.Cell().Element(Hucre).Text(gun.Gun.ToString("d MMM ddd", Turkce));
                                    tablo.Cell().Element(Hucre).AlignRight().Text(gun.OdenenSayisi.ToString());
                                    tablo.Cell().Element(Hucre).AlignRight().Text(gun.IptalSayisi.ToString());
                                    tablo.Cell().Element(Hucre).AlignRight().Text(Para(gun.Ciro));
                                }
                            });
                        });
                    }
                });

                sayfa.Footer().Row(alt =>
                {
                    alt.RelativeItem().Text($"TOST LAB panelinden oluşturuldu · {DateTime.Now.ToString("d MMMM yyyy HH:mm", Turkce)}")
                        .FontSize(8).FontColor(Soluk);
                    alt.ConstantItem(60).AlignRight().Text(yazi =>
                    {
                        yazi.DefaultTextStyle(stil => stil.FontSize(8).FontColor(Soluk));
                        yazi.CurrentPageNumber();
                        yazi.Span(" / ");
                        yazi.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Kirilim(ColumnDescriptor icerik, string baslik, List<KirilimSatiri> satirlar)
    {
        if (satirlar.Count == 0)
        {
            return;
        }
        icerik.Item().Column(bolum =>
        {
            bolum.Item().PaddingBottom(4).Text(baslik).FontSize(12).Bold();
            bolum.Item().Table(tablo =>
            {
                tablo.ColumnsDefinition(sutun => { sutun.RelativeColumn(); sutun.ConstantColumn(60); sutun.ConstantColumn(170); });
                foreach (var satir in satirlar)
                {
                    tablo.Cell().Element(Hucre).Text(satir.Ad);
                    tablo.Cell().Element(Hucre).AlignRight().Text($"{satir.Adet} adet").FontColor(Soluk);
                    tablo.Cell().Element(Hucre).AlignRight().Text(yazi =>
                    {
                        yazi.Span(Para(satir.Tutar));
                        if (satir.Komisyon > 0)
                        {
                            yazi.Span($"  (kom. −{Para(satir.Komisyon)})").FontColor(Soluk).FontSize(8);
                        }
                    });
                }
            });
        });
    }

    private static IContainer Hucre(IContainer hucre) =>
        hucre.BorderBottom(1).BorderColor(Cizgi).PaddingVertical(5).PaddingHorizontal(6);

    private static IContainer BaslikHucresi(IContainer hucre) =>
        hucre.Background(Vurgu).PaddingVertical(5).PaddingHorizontal(6).DefaultTextStyle(stil => stil.FontColor(Soluk));

    private static string Para(decimal tutar) => tutar.ToString("N2", Turkce) + " ₺";
}
