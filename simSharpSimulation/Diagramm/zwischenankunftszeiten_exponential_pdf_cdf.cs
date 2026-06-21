using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Exponentialverteilung der Zeitabstaende bis zum jeweils naechsten Patienten.
    internal static partial class GenerateDiagramme
    {
        private static void ErzeugeZwischenankunftszeitenExponentialDiagramm(
            IReadOnlyList<double> echteAnkunftszeiten,
            double erstePhase,
            double zweitePhase,
            double drittePhase)
        {
            List<double>[] echteAbstaende = ExtrahiereZwischenankunftszeiten(echteAnkunftszeiten);
            (string Name, double Mittelwert, Color Farbe, List<double> Abstaende)[] phasen =
            {
                ("Stunde 0-2", erstePhase, Color.RoyalBlue, echteAbstaende[0]),
                ("Stunde 2-5", zweitePhase, Color.DarkOrange, echteAbstaende[1]),
                ("Stunde 5-8", drittePhase, Color.SeaGreen, echteAbstaende[2])
            };

            double maxZeit = Math.Max(10.0, phasen.Max(p => p.Mittelwert) * 4.0);
            double[] x = Linspace(0.0, maxZeit, 600);

            var plot = new ScottPlot.Plot(1100, 650);
            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);

            const int histogrammBins = 40;
            double gruppenBreite = maxZeit / histogrammBins;
            double balkenBreite = gruppenBreite / phasen.Length;

            for (int phasenIndex = 0; phasenIndex < phasen.Length; phasenIndex++)
            {
                var phase = phasen[phasenIndex];
                if (phase.Abstaende.Count == 0)
                    continue;

                var (counts, centers, _) = BuildHistogram(
                    phase.Abstaende,
                    histogrammBins,
                    0.0,
                    maxZeit);
                double[] dichte = counts
                    .Select(count => count / (phase.Abstaende.Count * gruppenBreite))
                    .ToArray();
                double verschiebung = (phasenIndex - 1) * balkenBreite;
                double[] verschobeneCenters = centers.Select(center => center + verschiebung).ToArray();

                var bars = plot.AddBar(dichte, verschobeneCenters);
                bars.BarWidth = balkenBreite * 0.9;
                bars.FillColor = Color.FromArgb(90, phase.Farbe);
                bars.BorderColor = phase.Farbe;
                bars.Label = $"Histogramm {phase.Name} (n={phase.Abstaende.Count})";
            }

            foreach (var phase in phasen)
            {
                double lambda = 1.0 / phase.Mittelwert;
                double[] pdf = x.Select(t => lambda * Math.Exp(-lambda * t)).ToArray();
                double[] cdf = x.Select(t => 1.0 - Math.Exp(-lambda * t)).ToArray();

                plot.AddScatter(
                    x,
                    pdf,
                    color: phase.Farbe,
                    lineWidth: 3,
                    label: $"PDF {phase.Name} (Mittel {phase.Mittelwert:0.0} min)");

                var cdfLine = plot.AddScatter(
                    x,
                    cdf,
                    color: phase.Farbe,
                    lineStyle: ScottPlot.LineStyle.Dash,
                    lineWidth: 2,
                    label: $"CDF {phase.Name}");
                cdfLine.YAxisIndex = axisRight.AxisIndex;
            }

            plot.Title("Exponentielle Zwischenankunftszeiten je Tagesphase\nPDF und kumulierte Verteilung (CDF)");
            plot.XLabel("Zwischenankunftszeit bis zum naechsten Patienten (Minuten)");
            plot.YLabel("Wahrscheinlichkeitsdichte (PDF)");
            axisRight.Label("Kumulierte Wahrscheinlichkeit (CDF)");
            axisRight.SetBoundary(0.0, 1.05);
            plot.Legend(location: ScottPlot.Alignment.UpperRight);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("zwischenankunftszeiten_exponential_pdf_cdf.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm Zwischenankunftszeiten gespeichert: {outputPath} ---");
        }

        private static List<double>[] ExtrahiereZwischenankunftszeiten(
            IReadOnlyList<double> ankunftszeiten)
        {
            var ergebnis = new[] { new List<double>(), new List<double>(), new List<double>() };
            double? vorherigeZeit = null;

            foreach (double aktuelleZeit in ankunftszeiten)
            {
                if (vorherigeZeit.HasValue && aktuelleZeit >= vorherigeZeit.Value)
                {
                    int vorherigePhase = ErmittleAnkunftsphase(vorherigeZeit.Value);
                    int aktuellePhase = ErmittleAnkunftsphase(aktuelleZeit);
                    if (vorherigePhase == aktuellePhase)
                        ergebnis[aktuellePhase].Add(aktuelleZeit - vorherigeZeit.Value);
                }

                // Eine kleinere Uhrzeit markiert den Beginn des naechsten Simulationstags.
                vorherigeZeit = aktuelleZeit;
            }

            return ergebnis;
        }

        private static int ErmittleAnkunftsphase(double zeitMinuten)
        {
            if (zeitMinuten < 120.0)
                return 0;
            return zeitMinuten < 300.0 ? 1 : 2;
        }
    }
}
