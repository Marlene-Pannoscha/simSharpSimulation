using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 3 - Histogramm der Arzt-Wartezeiten.
    internal static partial class GenerateDiagramme
    {
        // Diagramm 3
        private static void ErzeugeWartezeitenDiagramm(IReadOnlyList<double> wartezeiten, int anzahlAerzte)
        {
            var plot = new ScottPlot.Plot(1000, 600);
            if (wartezeiten.Count > 0)
            {
                int bins = 20;
                double maxWartezeit = Math.Max(wartezeiten.Max(), 1.0);
                var (counts, centers, binWidth) = BuildHistogram(wartezeiten, bins, 0, maxWartezeit);
                var bars = plot.AddBar(counts, centers);
                bars.BarWidth = binWidth * 0.9;
                bars.FillColor = Color.Teal;
                bars.BorderColor = Color.Black;
            }

            plot.Title($"Verteilung der Wartezeiten (Ärzte: {anzahlAerzte})");
            plot.XLabel("Wartezeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("wartezeiten_histogramm.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 3 gespeichert: {outputPath} ---");
        }
    }
}
