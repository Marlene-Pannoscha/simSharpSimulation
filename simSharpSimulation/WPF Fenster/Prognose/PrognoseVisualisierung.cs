using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ScottPlot;

namespace simSharpSimulation;

internal static class PrognoseVisualisierung
{
    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    public static PrognoseDiagrammPfade ErzeugeDiagramme(string jsonPfad)
    {
        string json = File.ReadAllText(jsonPfad);
        PrognoseVisualDaten daten = JsonSerializer.Deserialize<PrognoseVisualDaten>(json)
            ?? throw new InvalidOperationException("Prognose-Daten konnten nicht geladen werden.");

        string outputOrdner = ErzeugePrognoseImageOrdner();

        string phasePfad = Path.Combine(outputOrdner, "prognose_trefferquote_phase.png");
        string scatterPfad = Path.Combine(outputOrdner, "prognose_restzeit_scatter.png");
        string abbruecheZeitPfad = Path.Combine(outputOrdner, "prognose_abbrueche_zeitachse.png");
        string abbruchGruendePfad = Path.Combine(outputOrdner, "prognose_abbruchgruende.png");

        ErzeugeTrefferquoteJePhaseDiagramm(daten, phasePfad);
        ErzeugeRestzeitScatterDiagramm(daten, scatterPfad);
        ErzeugePrognoseAbbruecheZeitachseDiagramm(daten, abbruecheZeitPfad);
        ErzeugeAbbruchgruendeDiagramm(daten, abbruchGruendePfad);

        return new PrognoseDiagrammPfade(
            phasePfad,
            scatterPfad,
            abbruecheZeitPfad,
            abbruchGruendePfad);
    }

    private static void ErzeugeTrefferquoteJePhaseDiagramm(PrognoseVisualDaten daten, string outputPfad)
    {
        Plot plot = new(1200, 600);
        double[] xs = Enumerable.Range(0, daten.Phasen.Count).Select(i => (double)i).ToArray();
        double[] ys = daten.Phasen.Select(p => p.Trefferquote).ToArray();
        string[] labels = daten.Phasen.Select(p => p.Phase).ToArray();

        var bars = plot.AddBar(ys, xs);
        bars.FillColor = Color.SteelBlue;
        bars.BorderColor = Color.DarkSlateBlue;
        bars.ShowValuesAboveBars = true;
        bars.ValueFormatter = value => $"{value.ToString("N1", DeCulture)} %";

        plot.XTicks(xs, labels);
        plot.XAxis.TickLabelStyle(fontSize: 14, rotation: 20);
        plot.YAxis.Label("Trefferquote (%)");
        plot.Title("Prognose-Trefferquote je Phase", size: 18);
        plot.SetAxisLimits(yMin: 0);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SaveFig(outputPfad);
    }

    private static void ErzeugeRestzeitScatterDiagramm(PrognoseVisualDaten daten, string outputPfad)
    {
        Plot plot = new(1200, 700);

        PrognoseErgebnisPunkt[] korrekte = daten.Ergebnisse.Where(e => e.Korrekt).ToArray();
        PrognoseErgebnisPunkt[] falsche = daten.Ergebnisse.Where(e => !e.Korrekt).ToArray();

        if (korrekte.Length > 0)
        {
            var korrektScatter = plot.AddScatter(
                korrekte.Select(e => e.PrognoseRestMinuten).ToArray(),
                korrekte.Select(e => e.IstRestMinuten).ToArray(),
                color: Color.SeaGreen,
                lineWidth: 0,
                markerSize: 4,
                label: "Korrekt");
            korrektScatter.MarkerShape = MarkerShape.filledCircle;
        }

        if (falsche.Length > 0)
        {
            var falschScatter = plot.AddScatter(
                falsche.Select(e => e.PrognoseRestMinuten).ToArray(),
                falsche.Select(e => e.IstRestMinuten).ToArray(),
                color: Color.IndianRed,
                lineWidth: 0,
                markerSize: 4,
                label: "Nicht korrekt");
            falschScatter.MarkerShape = MarkerShape.openCircle;
        }

        double maxWert = daten.Ergebnisse.Count > 0
            ? daten.Ergebnisse.Max(e => Math.Max(e.PrognoseRestMinuten, e.IstRestMinuten))
            : 1.0;

        var diagonal = plot.AddLine(0, 0, maxWert, maxWert, Color.DimGray);
        diagonal.LineStyle = LineStyle.Dash;
        diagonal.Label = "Ideal: Prognose = Ist";

        plot.XAxis.Label("Prognostizierte Restzeit (Minuten)");
        plot.YAxis.Label("Tatsächliche Restzeit (Minuten)");
        plot.Title("Prognose Restzeit vs. Ist-Restzeit", size: 18);
        plot.Legend(location: Alignment.UpperLeft);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SaveFig(outputPfad);
    }

    private static void ErzeugePrognoseAbbruecheZeitachseDiagramm(PrognoseVisualDaten daten, string outputPfad)
    {
        Plot plot = new(1200, 600);
        string[] phasen = { "Aufnahmeprognose", "Ankunft", "NachRezeption", "VorSchwester", "NachSchwester", "VorArzt", "NachArzt" };
        double[] yTicks = Enumerable.Range(0, phasen.Length).Select(i => (double)i).ToArray();
        Color[] farben =
        {
            Color.IndianRed, Color.SteelBlue, Color.DarkOrange, Color.MediumPurple,
            Color.Teal, Color.Firebrick, Color.ForestGreen
        };

        for (int i = 0; i < phasen.Length; i++)
        {
            string phase = phasen[i];
            PrognoseAbbruchPunkt[] phasePunkte = daten.PrognoseAbbrueche.Where(a => a.Phase == phase).ToArray();
            if (phasePunkte.Length == 0)
                continue;

            var scatter = plot.AddScatter(
                phasePunkte.Select(a => a.ZeitpunktMinuten).ToArray(),
                Enumerable.Repeat((double)i, phasePunkte.Length).ToArray(),
                color: farben[i % farben.Length],
                lineWidth: 0,
                markerSize: 9,
                label: phase);
            scatter.MarkerShape = MarkerShape.filledDiamond;
        }

        if (daten.PrognoseAbbrueche.Count == 0)
        {
            plot.AddText("Keine Prognose-Abbrüche vorhanden", 240, 2.5, size: 18, color: Color.DimGray);
        }

        plot.YTicks(yTicks, phasen);
        plot.XAxis.Label("Zeitpunkt im Tag (Minuten seit Tagesstart)");
        plot.YAxis.Label("Phase");
        plot.Title("Prognose-Abbrüche über Zeit", size: 18);
        plot.Legend(location: Alignment.UpperRight);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SaveFig(outputPfad);
    }

    private static void ErzeugeAbbruchgruendeDiagramm(PrognoseVisualDaten daten, string outputPfad)
    {
        Plot plot = new(1100, 600);

        double[] positionen = { 0.0, 1.0, 2.0, 3.0, 4.0 };
        double[] werte =
        {
            daten.Abbruchgruende.Prognose,
            daten.Abbruchgruende.Aufnahmeprognose,
            daten.Abbruchgruende.RezeptionFeierabend,
            daten.Abbruchgruende.SchwesterFeierabend,
            daten.Abbruchgruende.ArztFeierabend
        };

        var balken = plot.AddBar(werte, positionen);
        balken.FillColor = Color.SlateBlue;
        balken.BorderColor = Color.Black;
        balken.ShowValuesAboveBars = true;
        balken.ValueFormatter = value => value.ToString("N0", DeCulture);

        plot.XTicks(positionen, new[]
        {
            "Prognose",
            "Aufnahme-\nprognose",
            "Rezeption\nFeierabend",
            "Schwester\nFeierabend",
            "Arzt\nFeierabend"
        });
        plot.YAxis.Label("Anzahl");
        plot.Title("Abbruchgründe Vergleich", size: 18);
        plot.Grid(enable: true, lineStyle: LineStyle.Dot);
        plot.SetAxisLimits(yMin: 0);
        plot.SaveFig(outputPfad);
    }

    private static string ErzeugePrognoseImageOrdner()
    {
        string projektOrdner = ErmittleProjektRoot();
        string outputOrdner = Path.Combine(projektOrdner, "WPF Fenster", "Prognose", "images");
        Directory.CreateDirectory(outputOrdner);
        return outputOrdner;
    }

    private static string ErmittleProjektRoot()
    {
        string? viaCwd = FindeOrdnerMitDatei(Directory.GetCurrentDirectory(), "simSharpSimulation.csproj");
        if (!string.IsNullOrEmpty(viaCwd))
            return viaCwd;

        string? viaBase = FindeOrdnerMitDatei(AppContext.BaseDirectory, "simSharpSimulation.csproj");
        if (!string.IsNullOrEmpty(viaBase))
            return viaBase;

        return Directory.GetCurrentDirectory();
    }

    private static string? FindeOrdnerMitDatei(string startPfad, string dateiname)
    {
        DirectoryInfo? current = new(startPfad);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, dateiname)))
                return current.FullName;
            current = current.Parent;
        }

        return null;
    }

    internal readonly record struct PrognoseDiagrammPfade(
        string TrefferquoteJePhasePfad,
        string RestzeitScatterPfad,
        string PrognoseAbbruecheZeitPfad,
        string AbbruchgruendePfad);

    private sealed record PrognoseVisualDaten(
        int AnzahlPrognosePruefungen,
        int AnzahlPrognoseRichtig,
        int AnzahlPrognoseAbbruch,
        double PrognoseTrefferquote,
        Abbruchgruende Abbruchgruende,
        List<PrognosePhasePunkt> Phasen,
        List<PrognoseErgebnisPunkt> Ergebnisse,
        List<PrognoseAbbruchPunkt> PrognoseAbbrueche);

    private sealed record Abbruchgruende(
        int Prognose,
        int Aufnahmeprognose,
        int RezeptionFeierabend,
        int SchwesterFeierabend,
        int ArztFeierabend);

    private sealed record PrognosePhasePunkt(
        string Phase,
        int Anzahl,
        int Korrekt,
        double Trefferquote);

    private sealed record PrognoseErgebnisPunkt(
        int PatientId,
        string Phase,
        double ZeitpunktMinuten,
        double PrognoseRestMinuten,
        double IstRestMinuten,
        bool Korrekt,
        bool PrognoseFertigBisSchichtende);

    private sealed record PrognoseAbbruchPunkt(
        double ZeitpunktMinuten,
        string Phase);
}
