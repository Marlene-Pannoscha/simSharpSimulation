using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 6 - Histogramm der Gesamtprozesszeit (Eintritt bis Austritt).
    internal static partial class GenerateDiagramme
    {
        // Diagramm 6
        private static void ErzeugeGesamtprozesszeitDiagramm(IReadOnlyList<double> gesamtprozesszeiten)
        {
            var plot = new ScottPlot.Plot(1000, 600);

            if (gesamtprozesszeiten.Count > 0)
            {
                int bins = 20;
                double maxProzesszeit = Math.Max(gesamtprozesszeiten.Max(), 1.0);
                var (counts, centers, binWidth) = BuildHistogram(gesamtprozesszeiten, bins, 0, maxProzesszeit);
                var bars = plot.AddBar(counts, centers);
                bars.BarWidth = binWidth * 0.9;
                bars.FillColor = Color.MediumPurple;
                bars.BorderColor = Color.Black;
            }

            plot.Title("Verteilung der Gesamtprozesszeit (Klinik-Eintritt bis Austritt)");
            plot.XLabel("Gesamtprozesszeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("gesamtprozesszeit_histogramm.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 6 gespeichert: {outputPath} ---");
        }
    }
}
