using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.IO;
using ScottPlot;

namespace simSharpSimulation;

internal static partial class FinanzVisualisierung
{
    // Deutsche Kultur fuer gut lesbare EUR- und Zahlenformate in UI und Exporten.
    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    public static readonly string[] ZeitraumOptionen = { "Jahr", "Winter", "Fruehling", "Sommer", "Herbst" };

    public static (string PatientenPfad, string GewinnPfad, string KostenstrukturPfad) ErzeugeDiagramme(
        FinanzErgebnis ergebnis,
        int anzahlAerzte,
        int anzahlSchwestern)
    {
        string outputOrdner = ErzeugeKostenImageOrdner();
        string zeitraumSlug = BereinigeDateiname(ergebnis.Zeitraum.ToLowerInvariant());

        string finanzenPfad = Path.Combine(outputOrdner, $"finanzen_{zeitraumSlug}.png");
        string gewinnPfad = Path.Combine(outputOrdner, $"gewinn_{zeitraumSlug}.png");
        string kostenstrukturPfad = Path.Combine(outputOrdner, $"kostenstruktur_{zeitraumSlug}.png");

        ErzeugeFinanzenDiagramm(ergebnis, anzahlAerzte, anzahlSchwestern, finanzenPfad);
        ErzeugeGewinnDiagramm(ergebnis, anzahlAerzte, anzahlSchwestern, gewinnPfad);
        ErzeugeKostenstrukturDiagramm(ergebnis, kostenstrukturPfad);

        return (finanzenPfad, gewinnPfad, kostenstrukturPfad);
    }

    public static (int AnzahlHit, int AnzahlMiss, string DiagrammPfad) ErzeugeHitMissDiagramm(FinanzErgebnis ergebnis)
    {
        string outputOrdner = ErzeugeKostenImageOrdner();
        string zeitraumSlug = BereinigeDateiname(ergebnis.Zeitraum.ToLowerInvariant());
        string hitMissPfad = Path.Combine(outputOrdner, $"behandelte_patienten_{zeitraumSlug}.png");

        int anzahlHit = ergebnis.GesamtBehandelt;
        int anzahlMiss = ergebnis.GesamtNichtBehandelt;

        ErzeugeHitMissDiagrammIntern(anzahlHit, anzahlMiss, ergebnis.Zeitraum, hitMissPfad);
        return (anzahlHit, anzahlMiss, hitMissPfad);
    }

    public static string FormatEuro(double wert) => string.Format(DeCulture, "{0:N2} EUR", wert);

    public static string FormatBreakEven(BreakEvenPoint breakEven, double aktuellePatientenProTag)
    {
        if (breakEven.Patienten == int.MaxValue || double.IsInfinity(breakEven.Tage) || double.IsNaN(breakEven.Tage))
            return "Break-Even: nicht erreichbar";

        double differenz = aktuellePatientenProTag - breakEven.Patienten;
        string differenzText = differenz >= 0
            ? $"+{differenz.ToString("N1", DeCulture)} Patienten/Tag"
            : $"{differenz.ToString("N1", DeCulture)} Patienten/Tag";

        return $"Break-Even: {breakEven.Patienten} Patienten/Tag ({breakEven.Tage.ToString("N2", DeCulture)} Tage bei aktueller Auslastung, Differenz {differenzText})";
    }

    // Generiert den gleichen Textbericht wie die WPF-Ansicht (für Konsolen-Ausgabe oder Tests).
    public static string ErzeugeErgebnisReportText(FinanzErgebnis ergebnis, string finanzenPfad, string gewinnPfad, string kostenstrukturPfad)
    {
        // Reuse the formatting and labels used in the WPF view so console and WPF are identical.
        StringBuilder sb = new();
        sb.AppendLine("Ergebnis");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"Zeitraum: {ergebnis.Zeitraum}");
        sb.AppendLine($"Simulierte Tage: {ergebnis.SimulierteTage}");
        sb.AppendLine($"Gesamtumsatz: {FormatEuro(ergebnis.GesamtUmsatz)}");
        sb.AppendLine($"Gesamtkosten: {FormatEuro(ergebnis.Gesamtkosten)}");
        sb.AppendLine($"Gesamtgewinn: {FormatEuro(ergebnis.Gesamtgewinn)}");
        sb.AppendLine($"Durchschnitt Umsatz pro Tag: {FormatEuro(ergebnis.DurchschnittlicherUmsatzProTag)}");
        sb.AppendLine($"Durchschnitt Kosten pro Tag: {FormatEuro(ergebnis.DurchschnittlicheKostenProTag)}");
        sb.AppendLine($"Durchschnitt Gewinn pro {ergebnis.DurchschnittLabel}: {FormatEuro(ergebnis.DurchschnittlicherGewinnProEinheit)}");
        sb.AppendLine($"Durchschnitt behandelte Patienten pro Tag: {ergebnis.DurchschnittBehandeltePatientenProTag.ToString("N1", DeCulture)}");
        sb.AppendLine(FormatBreakEven(ergebnis.BreakEven, ergebnis.DurchschnittBehandeltePatientenProTag));
        sb.AppendLine();
        sb.AppendLine("Praxisdetails");
        sb.AppendLine($"Gesamtfläche: {ergebnis.Gesamtflaeche.ToString("N2", DeCulture)} m²");
        sb.AppendLine($"Mietkosten pro m²/Monat: {FormatEuro(ergebnis.MietkostenProQm)}");
        sb.AppendLine($"Gesamtmietkosten pro Tag: {FormatEuro(ergebnis.GesamtMietkostenProTag)}");
        double gesamtMietkostenMonat = ergebnis.MietkostenProQm * ergebnis.Gesamtflaeche;
        sb.AppendLine($"Gesamtmietkosten pro Monat: {FormatEuro(gesamtMietkostenMonat)}");
        sb.AppendLine($"Gesamtmietkosten pro Jahr: {FormatEuro(gesamtMietkostenMonat * 12)}");
        sb.AppendLine();
        sb.AppendLine("Kostenstruktur (Basis: Umsatz)");
        sb.AppendLine($"Personalkosten: {FormatEuro(ergebnis.GesamtPersonalkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtPersonalkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Mietkosten (im Zeitraum, gesamt): {FormatEuro(ergebnis.GesamtMietkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtMietkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Energie: {FormatEuro(ergebnis.GesamtEnergiekosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtEnergiekosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Reinigung: {FormatEuro(ergebnis.GesamtReinigungskosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtReinigungskosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Infrastruktur: {FormatEuro(ergebnis.GesamtInfrastrukturkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtInfrastrukturkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"IT und Verwaltung: {FormatEuro(ergebnis.GesamtITUndVerwaltungskosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtITUndVerwaltungskosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Versicherungen: {FormatEuro(ergebnis.GesamtVersicherungskosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtVersicherungskosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Medizinisches Material (patientenabhängig): {FormatEuro(ergebnis.GesamtMaterialkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtMaterialkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Geräte-Leasing: {FormatEuro(ergebnis.GesamtLeasingkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtLeasingkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Geräte-Wartung: {FormatEuro(ergebnis.GesamtWartungskosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtWartungskosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Sonstige Fixkosten: {FormatEuro(ergebnis.GesamtSonstigeFixkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtSonstigeFixkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Gewinn: {FormatEuro(ergebnis.Gesamtgewinn)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.Gesamtgewinn / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine();
        sb.AppendLine("Zielkorridore (Kostenanteile)");
        sb.AppendLine($"Personalkosten: {(ergebnis.Gesamtkosten > 0 ? ergebnis.GesamtPersonalkosten / ergebnis.Gesamtkosten : 0.0):P2} (Ziel unter 50 %)");
        sb.AppendLine($"IT, Verwaltung und Versicherungen: {(ergebnis.Gesamtkosten > 0 ? ergebnis.GesamtITVerwaltungVersicherung / ergebnis.Gesamtkosten : 0.0):P2} (Ziel 8-15 %)");
        sb.AppendLine($"Medizinisches Verbrauchsmaterial: {(ergebnis.Gesamtkosten > 0 ? ergebnis.GesamtMaterialkosten / ergebnis.Gesamtkosten : 0.0):P2} (Ziel 15 %)");
        sb.AppendLine($"Raeume, Energie und Reinigung: {(ergebnis.Gesamtkosten > 0 ? ergebnis.GesamtRaumkosten / ergebnis.Gesamtkosten : 0.0):P2} (Ziel 7-12 %)");
        sb.AppendLine($"Geraete, Leasing und Wartung: {(ergebnis.Gesamtkosten > 0 ? ergebnis.GesamtGeraetekosten / ergebnis.Gesamtkosten : 0.0):P2} (Ziel 5-10 %)");
        sb.AppendLine($"Gewinnmarge: {(ergebnis.GesamtUmsatz > 0 ? ergebnis.Gesamtgewinn / ergebnis.GesamtUmsatz : 0.0):P2} (Ziel 7-10 %)");
        sb.AppendLine();
        sb.AppendLine("Saisonaler Gewinn (im gewählten Zeitraum)");
        foreach (string saison in new[] { "Winter", "Fruehling", "Sommer", "Herbst" })
        {
            double saisonWert = ergebnis.SaisonGewinn.TryGetValue(saison, out double wert) ? wert : 0.0;
            sb.AppendLine($"{saison}: {FormatEuro(saisonWert)}");
        }
        sb.AppendLine();
        sb.AppendLine("Versicherung");
        sb.AppendLine($"Privat: {ergebnis.VersicherungenGesamt.PrivatPatienten} Patienten / {FormatEuro(ergebnis.UmsatzverteilungGesamt.UmsatzPrivat)}");
        sb.AppendLine($"Gesetzlich: {ergebnis.VersicherungenGesamt.GesetzlichPatienten} Patienten / {FormatEuro(ergebnis.UmsatzverteilungGesamt.UmsatzGesetzlich)}");
        sb.AppendLine();
        sb.AppendLine("Behandlungsdauer");
        sb.AppendLine($"Kurz: {ergebnis.BehandlungsmixGesamt.KurzPatienten} Patienten");
        sb.AppendLine($"Mittel: {ergebnis.BehandlungsmixGesamt.MittelPatienten} Patienten");
        sb.AppendLine($"Lang: {ergebnis.BehandlungsmixGesamt.LangPatienten} Patienten");
        sb.AppendLine();
        sb.AppendLine("Dateien");
        sb.AppendLine($"- Finanzen: {finanzenPfad}");
        sb.AppendLine($"- Gewinn: {gewinnPfad}");
        sb.AppendLine($"- Kostenstruktur: {kostenstrukturPfad}");
        return sb.ToString();
    }

    private static void ErzeugeFinanzenDiagramm(
        FinanzErgebnis ergebnis,
        int anzahlAerzte,
        int anzahlSchwestern,
        string outputPfad)
    {
        // Das erste Diagramm stellt Umsatz und Kosten direkt gegenueber.
        Plot plot = new(1300, 600);
        double[] xs = Enumerable.Range(0, ergebnis.Tagespunkte.Count).Select(i => (double)i).ToArray();
        double[] umsatz = ergebnis.Tagespunkte.Select(p => p.Umsatz).ToArray();
        double[] kosten = ergebnis.Tagespunkte.Select(p => p.Kosten).ToArray();

        var umsatzLinie = plot.AddScatterLines(xs, umsatz, color: Color.SeaGreen);
        umsatzLinie.MarkerSize = 4;
        umsatzLinie.Label = "Umsatz";

        var kostenLinie = plot.AddScatterLines(xs, kosten, color: Color.OrangeRed);
        kostenLinie.MarkerSize = 4;
        kostenLinie.Label = "Kosten";

        plot.XTicks(xs, ergebnis.Achsenwerte.ToArray());
        plot.XAxis.TickLabelStyle(fontSize: 14, rotation: ergebnis.Achsenwerte.Count > 20 ? 45 : 0);
        plot.XAxis.LabelStyle(fontSize: 16);
        plot.YAxis.LabelStyle(fontSize: 16);
        plot.Title($"Umsatz und Kosten - Aerzte: {anzahlAerzte}, Schwestern: {anzahlSchwestern} - {ergebnis.Zeitraum}", size: 18);
        plot.Legend(location: Alignment.UpperRight);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SaveFig(outputPfad);
    }

    private static void ErzeugeGewinnDiagramm(
        FinanzErgebnis ergebnis,
        int anzahlAerzte,
        int anzahlSchwestern,
        string outputPfad)
    {
        // Positive und negative Gewinne werden getrennt eingefaerbt, damit Verlustphasen schneller auffallen.
        Plot plot = new(1300, 600);
        double[] xs = Enumerable.Range(0, ergebnis.GewinnVerlauf.Count).Select(i => (double)i).ToArray();
        double[] positive = ergebnis.GewinnVerlauf.Select(v => v > 0 ? v : 0).ToArray();
        double[] negative = ergebnis.GewinnVerlauf.Select(v => v < 0 ? v : 0).ToArray();

        var barsPos = plot.AddBar(positive, xs);
        barsPos.FillColor = Color.SeaGreen;
        barsPos.BorderColor = Color.DarkGreen;
        barsPos.Label = "Gewinn (>= 0)";

        var barsNeg = plot.AddBar(negative, xs);
        barsNeg.FillColor = Color.IndianRed;
        barsNeg.BorderColor = Color.DarkRed;
        barsNeg.Label = "Gewinn (< 0)";

        var nullLinie = plot.AddHorizontalLine(0, color: Color.Black);
        nullLinie.LineStyle = LineStyle.Dash;

        plot.XTicks(xs, ergebnis.Achsenwerte.ToArray());
        plot.XAxis.TickLabelStyle(fontSize: 14, rotation: ergebnis.Achsenwerte.Count > 20 ? 45 : 0);
        plot.XAxis.LabelStyle(fontSize: 16);
        plot.YAxis.LabelStyle(fontSize: 16);

        string titel = string.Equals(ergebnis.Zeitraum, "Jahr", StringComparison.OrdinalIgnoreCase)
            ? "Gewinn pro Monat"
            : "Gewinn pro Tag";

        plot.Title($"{titel} - Aerzte: {anzahlAerzte}, Schwestern: {anzahlSchwestern} - {ergebnis.Zeitraum}", size: 18);
        plot.Legend(location: Alignment.UpperRight);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SaveFig(outputPfad);
    }

    private static void ErzeugeHitMissDiagrammIntern(
        int anzahlHit,
        int anzahlMiss,
        string zeitraum,
        string outputPfad)
    {
        Plot plot = new(1000, 600);
        double[] positionen = { 0, 1 };
        string[] beschriftungen = { "Hit", "Miss" };

        var hitBalken = plot.AddBar(new[] { (double)anzahlHit }, new[] { 0.0 });
        hitBalken.FillColor = Color.SeaGreen;
        hitBalken.BorderColor = Color.Black;
        hitBalken.ShowValuesAboveBars = true;
        hitBalken.ValueFormatter = value => value.ToString("N0", DeCulture);

        var missBalken = plot.AddBar(new[] { (double)anzahlMiss }, new[] { 1.0 });
        missBalken.FillColor = Color.IndianRed;
        missBalken.BorderColor = Color.Black;
        missBalken.ShowValuesAboveBars = true;
        missBalken.ValueFormatter = value => value.ToString("N0", DeCulture);

        plot.XTicks(positionen, beschriftungen);
        plot.XAxis.TickLabelStyle(fontSize: 16);
        plot.YAxis.TickLabelFormat("N0", dateTimeFormat: false);
        plot.YAxis.Label("Patienten");
        plot.Title($"Hit/Miss Analyse - {zeitraum}", size: 18);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SetAxisLimits(yMin: 0);
        plot.SaveFig(outputPfad);
    }

#pragma warning disable CA1416 // Die Anwendung und dieses Renderingziel sind ausschliesslich Windows/WPF.
    private static void ErzeugeKostenstrukturDiagramm(FinanzErgebnis ergebnis, string outputPfad)
    {
        double umsatz = ergebnis.GesamtUmsatz;
        double gewinn = ergebnis.Gesamtgewinn;
        bool mitGewinn = gewinn >= 0.0;
        double basis = mitGewinn ? umsatz : ergebnis.Gesamtkosten;
        string basisLabel = mitGewinn ? "Umsatz" : "Gesamtkosten";

        List<double> amounts = new()
        {
            ergebnis.GesamtPersonalkosten,
            ergebnis.GesamtMietkosten,
            ergebnis.GesamtEnergiekosten,
            ergebnis.GesamtReinigungskosten,
            ergebnis.GesamtInfrastrukturkosten,
            ergebnis.GesamtITUndVerwaltungskosten,
            ergebnis.GesamtVersicherungskosten,
            ergebnis.GesamtMaterialkosten,
            ergebnis.GesamtLeasingkosten,
            ergebnis.GesamtWartungskosten,
            ergebnis.GesamtSonstigeFixkosten
        };

        List<string> labels = new() { "Personal", "Miete", "Energie", "Reinigung", "Infrastruktur", "IT/Verwaltung", "Versicherung", "Material", "Leasing", "Wartung", "Sonstige" };
        List<Color> farben = new()
        {
            Color.FromArgb(52, 152, 219),
            Color.FromArgb(155, 89, 182),
            Color.FromArgb(26, 188, 156),
            Color.FromArgb(127, 140, 141),
            Color.FromArgb(46, 204, 113),
            Color.FromArgb(52, 73, 94),
            Color.FromArgb(41, 128, 185),
            Color.FromArgb(241, 196, 15),
            Color.FromArgb(231, 76, 60),
            Color.FromArgb(192, 57, 43),
            Color.FromArgb(149, 165, 166)
        };

        if (mitGewinn)
        {
            amounts.Add(gewinn);
            labels.Add("Gewinn");
            farben.Add(Color.FromArgb(230, 126, 34));
        }

        bool hatWerte = amounts.Sum() > 0.0;
        if (!hatWerte)
        {
            amounts = new List<double> { 1.0 };
            labels = new List<string> { "Keine Werte" };
            farben = new List<Color> { Color.LightGray };
            basis = 0.0;
        }

        string verlustHinweis = mitGewinn ? string.Empty : $", Verlust: {FormatEuro(Math.Abs(gewinn))}";
        string titel = $"Gewinn und Kostenstruktur ({ergebnis.Zeitraum}, Basis: {basisLabel}{verlustHinweis})";

        const int breite = 1400;
        const int hoehe = 1600;
        const float mittelpunktX = 700;
        const float mittelpunktY = 780;
        const float radius = 290;

        using Bitmap bitmap = new(breite, hoehe);
        using Graphics grafik = Graphics.FromImage(bitmap);
        grafik.SmoothingMode = SmoothingMode.AntiAlias;
        grafik.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        grafik.Clear(Color.White);

        using Font titelFont = new("Segoe UI", 38, FontStyle.Bold, GraphicsUnit.Pixel);
        using Font labelFont = new("Segoe UI", 24, FontStyle.Regular, GraphicsUnit.Pixel);
        using Font hervorhebungFont = new("Segoe UI", 32, FontStyle.Bold, GraphicsUnit.Pixel);
        using Font legendeFont = new("Segoe UI", 23, FontStyle.Regular, GraphicsUnit.Pixel);
        using Font legendeFettFont = new("Segoe UI", 23, FontStyle.Bold, GraphicsUnit.Pixel);
        using Pen segmentRand = new(Color.White, 3);
        using Pen fuehrungsLinie = new(Color.FromArgb(45, 55, 65), 2.5f);
        using Brush textPinsel = new SolidBrush(Color.FromArgb(25, 32, 40));

        using StringFormat titelFormat = new() { Alignment = StringAlignment.Center };
        grafik.DrawString(titel, titelFont, textPinsel, new RectangleF(30, 175, breite - 60, 65), titelFormat);

        RectangleF kreis = new(mittelpunktX - radius, mittelpunktY - radius, radius * 2, radius * 2);
        double summe = amounts.Sum();
        float startWinkel = -90f;
        List<KostenstrukturBeschriftung> beschriftungen = new();

        for (int i = 0; i < amounts.Count; i++)
        {
            if (amounts[i] <= 0 || summe <= 0)
                continue;

            float sweep = (float)(amounts[i] / summe * 360.0);
            using Brush segmentPinsel = new SolidBrush(farben[i]);
            grafik.FillPie(segmentPinsel, kreis, startWinkel, sweep);
            grafik.DrawPie(segmentRand, kreis, startWinkel, sweep);

            double mitteGrad = startWinkel + sweep / 2.0;
            double mitteRad = mitteGrad * Math.PI / 180.0;
            bool rechts = Math.Cos(mitteRad) >= 0;
            if (labels[i] == "Gewinn")
            {
                beschriftungen.Add(new KostenstrukturBeschriftung
                {
                    Index = i,
                    WinkelRad = mitteRad,
                    Rechts = rechts,
                    ZielY = mittelpunktY + (float)Math.Sin(mitteRad) * (radius + 105)
                });
            }

            startWinkel += sweep;
        }

        ZeichneKostenstrukturLegende(
            grafik,
            labels,
            amounts,
            farben,
            basis,
            hatWerte,
            new RectangleF(100, 1130, breite - 200, 400),
            legendeFont,
            legendeFettFont);

        VerteileKostenstrukturBeschriftungen(beschriftungen.Where(b => !b.Rechts).ToList(), 455, 1100, 125);
        VerteileKostenstrukturBeschriftungen(beschriftungen.Where(b => b.Rechts).ToList(), 455, 1100, 125);

        foreach (KostenstrukturBeschriftung beschriftung in beschriftungen)
        {
            float randX = mittelpunktX + (float)Math.Cos(beschriftung.WinkelRad) * radius;
            float randY = mittelpunktY + (float)Math.Sin(beschriftung.WinkelRad) * radius;
            float knickX = beschriftung.Rechts ? mittelpunktX + radius + 60 : mittelpunktX - radius - 60;
            float linienEndeX = beschriftung.Rechts ? breite - 90 : 90;

            grafik.DrawLine(fuehrungsLinie, randX, randY, knickX, beschriftung.ZielY);
            grafik.DrawLine(fuehrungsLinie, knickX, beschriftung.ZielY, linienEndeX, beschriftung.ZielY);

            string prozent = (basis > 0 ? amounts[beschriftung.Index] / basis : 0.0)
                .ToString("P2", DeCulture);
            string text = $"{labels[beschriftung.Index]}:\n" +
                $"{FormatEuro(hatWerte ? amounts[beschriftung.Index] : 0.0)}\n" +
                $"({prozent})";
            Font verwendeterFont = labels[beschriftung.Index] == "Gewinn" ? hervorhebungFont : labelFont;
            float textHoehe = labels[beschriftung.Index] == "Gewinn" ? 165 : 100;
            RectangleF textBereich = beschriftung.Rechts
                ? new RectangleF(knickX + 10, beschriftung.ZielY - textHoehe - 5, breite - knickX - 100, textHoehe)
                : new RectangleF(90, beschriftung.ZielY - textHoehe - 5, knickX - 100, textHoehe);
            using StringFormat textFormat = new()
            {
                Alignment = beschriftung.Rechts ? StringAlignment.Near : StringAlignment.Far,
                LineAlignment = StringAlignment.Far,
                Trimming = StringTrimming.EllipsisCharacter
            };
            grafik.DrawString(text, verwendeterFont, textPinsel, textBereich, textFormat);
        }

        bitmap.Save(outputPfad, ImageFormat.Png);
    }

    private static void ZeichneKostenstrukturLegende(
        Graphics grafik,
        IReadOnlyList<string> labels,
        IReadOnlyList<double> amounts,
        IReadOnlyList<Color> farben,
        double basis,
        bool hatWerte,
        RectangleF bereich,
        Font font,
        Font fettFont)
    {
        List<int> indices = Enumerable.Range(0, labels.Count)
            .Where(i => labels[i] != "Gewinn" && (amounts[i] > 0.0 || !hatWerte))
            .ToList();

        if (indices.Count == 0)
            return;

        int spalten = indices.Count > 6 ? 2 : 1;
        int zeilen = (int)Math.Ceiling(indices.Count / (double)spalten);
        float spaltenBreite = bereich.Width / spalten;
        float zeilenHoehe = Math.Min(62, bereich.Height / Math.Max(1, zeilen));

        using Pen rahmen = new(Color.FromArgb(224, 229, 234), 1.2f);
        using Brush hintergrund = new SolidBrush(Color.FromArgb(248, 250, 252));
        using Brush textPinsel = new SolidBrush(Color.FromArgb(25, 32, 40));
        using Brush nebenTextPinsel = new SolidBrush(Color.FromArgb(85, 96, 108));
        using StringFormat rechtsBuendig = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        grafik.FillRectangle(hintergrund, bereich);
        grafik.DrawRectangle(rahmen, bereich.Left, bereich.Top, bereich.Width, bereich.Height);

        for (int indexInLegende = 0; indexInLegende < indices.Count; indexInLegende++)
        {
            int wertIndex = indices[indexInLegende];
            int spalte = indexInLegende / zeilen;
            int zeile = indexInLegende % zeilen;
            float x = bereich.Left + spalte * spaltenBreite + 24;
            float y = bereich.Top + 24 + zeile * zeilenHoehe;
            double wert = hatWerte ? amounts[wertIndex] : 0.0;
            string prozent = (basis > 0 ? wert / basis : 0.0).ToString("P2", DeCulture);

            using Brush farbPinsel = new SolidBrush(farben[wertIndex]);
            grafik.FillRectangle(farbPinsel, x, y + 13, 20, 20);
            grafik.DrawRectangle(Pens.White, x, y + 13, 20, 20);
            grafik.DrawString(labels[wertIndex], fettFont, textPinsel, x + 34, y + 4);

            RectangleF wertBereich = new(x + spaltenBreite - 350, y, 300, zeilenHoehe);
            grafik.DrawString($"{FormatEuro(wert)} ({prozent})", font, nebenTextPinsel, wertBereich, rechtsBuendig);
        }
    }
#pragma warning restore CA1416

    private static void VerteileKostenstrukturBeschriftungen(
        List<KostenstrukturBeschriftung> beschriftungen,
        float minimumY,
        float maximumY,
        float mindestAbstand)
    {
        beschriftungen.Sort((a, b) => a.ZielY.CompareTo(b.ZielY));
        for (int i = 0; i < beschriftungen.Count; i++)
        {
            float minimum = i == 0 ? minimumY : beschriftungen[i - 1].ZielY + mindestAbstand;
            beschriftungen[i].ZielY = Math.Max(beschriftungen[i].ZielY, minimum);
        }

        for (int i = beschriftungen.Count - 1; i >= 0; i--)
        {
            float maximum = i == beschriftungen.Count - 1
                ? maximumY
                : beschriftungen[i + 1].ZielY - mindestAbstand;
            beschriftungen[i].ZielY = Math.Min(beschriftungen[i].ZielY, maximum);
        }
    }

    private sealed class KostenstrukturBeschriftung
    {
        public int Index { get; init; }
        public double WinkelRad { get; init; }
        public bool Rechts { get; init; }
        public float ZielY { get; set; }
    }

    private static string ErzeugeKostenImageOrdner()
    {
        string projektOrdner = ErmittleProjektRoot();
        string outputOrdner = Path.Combine(projektOrdner, "Kosten", "images");
        // Der Zielordner wird bei Bedarf automatisch angelegt.
        Directory.CreateDirectory(outputOrdner);
        return outputOrdner;
    }

    private static string ErmittleProjektRoot()
    {
        return ProjektPfade.ErmittleProjektOrdner();
    }

    private static string BereinigeDateiname(string text)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            text = text.Replace(c, '_');
        return text;
    }

}
