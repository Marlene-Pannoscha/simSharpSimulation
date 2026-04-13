using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    internal static partial class GenerateDiagramme
    {
        // Diagramm 5
        private static void ErzeugeWartezeitenVergleichsDiagramm(
            IReadOnlyList<double> wartezeiten,
            IReadOnlyList<double> schwesternWartezeiten,
            int anzahlAerzte,
            int anzahlSchwestern)
        {
            var plot = new ScottPlot.Plot(1000, 600);
            int bins = 20;

            double maxWartezeit = Math.Max(
                wartezeiten.Count > 0 ? wartezeiten.Max() : 0,
                schwesternWartezeiten.Count > 0 ? schwesternWartezeiten.Max() : 0);
            maxWartezeit = Math.Max(maxWartezeit, 1.0);

            var (doctorCounts, centers, binWidth) = BuildHistogram(wartezeiten, bins, 0, maxWartezeit);
            var (nurseCounts, _, _) = BuildHistogram(schwesternWartezeiten, bins, 0, maxWartezeit);

            double offset = binWidth * 0.2;

            var doctorBars = plot.AddBar(doctorCounts, centers.Select(c => c - offset).ToArray());
            doctorBars.BarWidth = binWidth * 0.35;
            doctorBars.FillColor = Color.Teal;
            doctorBars.BorderColor = Color.Black;
            doctorBars.Label = "Arzt-Wartezeiten";

            var nurseBars = plot.AddBar(nurseCounts, centers.Select(c => c + offset).ToArray());
            nurseBars.BarWidth = binWidth * 0.35;
            nurseBars.FillColor = Color.PeachPuff;
            nurseBars.BorderColor = Color.Black;
            nurseBars.Label = "Schwester-Wartezeiten";

            plot.Title($"Wartezeitenvergleich: Ärzte vs. Schwestern\n(Ärzte: {anzahlAerzte}, Schwestern: {anzahlSchwestern})");
            plot.XLabel("Wartezeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            plot.Legend(location: ScottPlot.Alignment.UpperRight);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("wartezeiten_vergleich_arzt_schwester.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 5 gespeichert: {outputPath} ---");
        }
    }
}
