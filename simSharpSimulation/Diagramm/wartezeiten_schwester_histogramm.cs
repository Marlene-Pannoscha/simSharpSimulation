using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 4 - Histogramm der Wartezeiten bei den Schwestern.
    internal static partial class GenerateDiagramme
    {
        // Diagramm 4
        private static void ErzeugeSchwesternWartezeitenDiagramm(IReadOnlyList<double> schwesternWartezeiten, int anzahlSchwestern)
        {
            var plot = new ScottPlot.Plot(1000, 600);
            if (schwesternWartezeiten.Count > 0)
            {
                int bins = 20;
                double maxWartezeit = Math.Max(schwesternWartezeiten.Max(), 1.0);
                var (counts, centers, binWidth) = BuildHistogram(schwesternWartezeiten, bins, 0, maxWartezeit);
                var bars = plot.AddBar(counts, centers);
                bars.BarWidth = binWidth * 0.9;
                bars.FillColor = Color.PeachPuff;
                bars.BorderColor = Color.Black;
            }

            plot.Title($"Verteilung der Wartezeiten bei den Schwestern (Schwestern: {anzahlSchwestern})");
            plot.XLabel("Wartezeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("wartezeiten_schwester_histogramm.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 4 gespeichert: {outputPath} ---");
        }
    }
}
