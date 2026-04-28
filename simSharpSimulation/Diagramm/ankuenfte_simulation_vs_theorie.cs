using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 2 - Vergleich von simulierten Ankünften mit theoretischer PDF/CDF.
    internal static partial class GenerateDiagramme
    {
        // Diagramm 2
        private static void ErzeugeAnkuenfteVergleichsDiagramm(
            IReadOnlyList<double> echteAnkunftszeiten,
            double[] x,
            double[] pdf,
            double[] cdf,
            double simulationsdauer,
            double erwartungswert,
            double standardabweichung)
        {
            int anzahlBins = 24;
            var (ankunftCounts, ankunftCenters, balkenBreite) = BuildHistogram(echteAnkunftszeiten, anzahlBins, 0, simulationsdauer);

            var plot = new ScottPlot.Plot(1000, 600);
            var bars = plot.AddBar(ankunftCounts, ankunftCenters);
            bars.BarWidth = balkenBreite * 0.9;
            bars.FillColor = Color.LightBlue;
            bars.BorderColor = Color.Black;
            bars.Label = "Tatsächliche Ankünfte (Histogramm)";

            double[] skaliertePdf = pdf.Select(v => v * echteAnkunftszeiten.Count * balkenBreite).ToArray();
            plot.AddScatter(x, skaliertePdf, color: Color.DarkBlue, lineWidth: 3, label: "PDF (Dichtefunktion)");

            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);
            var cdfLine = plot.AddScatter(x, cdf, color: Color.Red, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 3, label: "CDF (Verteilungsfunktion)");
            cdfLine.YAxisIndex = axisRight.AxisIndex;

            plot.Title($"Simulation vs. Theorie: Ankünfte\n(Mittelwert: {erwartungswert}, StdAbw: {standardabweichung})");
            plot.XLabel("Zeit der Simulation in Minuten (0 bis 480)");
            plot.YLabel("Anzahl der Patienten");
            axisRight.Label("Kumulierte Wahrscheinlichkeit (CDF)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);

            string outputPath = ErzeugeOutputPfad("ankuenfte_simulation_vs_theorie.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 2 gespeichert: {outputPath} ---");
        }
    }
}
