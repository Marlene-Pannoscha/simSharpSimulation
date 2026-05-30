using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 12 - Hit/Miss-Auswertung je Simulationstag.
    internal static partial class GenerateDiagramme
    {
        private static void ErzeugeHitMissProTagDiagramm(IReadOnlyList<TagesHitMissPunkt> hitMissProTag)
        {
            var plot = new ScottPlot.Plot(1100, 600);

            if (hitMissProTag.Count > 0)
            {
                double[] basisX = Enumerable.Range(0, hitMissProTag.Count).Select(i => (double)i).ToArray();
                double[] hitX = basisX.Select(x => x - 0.18).ToArray();
                double[] missX = basisX.Select(x => x + 0.18).ToArray();
                double[] hitWerte = hitMissProTag.Select(p => (double)p.Hit).ToArray();
                double[] missWerte = hitMissProTag.Select(p => (double)p.Miss).ToArray();
                string[] labels = hitMissProTag.Select(p => p.Label).ToArray();

                var hitBars = plot.AddBar(hitWerte, hitX);
                hitBars.BarWidth = 0.32;
                hitBars.FillColor = Color.SeaGreen;
                hitBars.BorderColor = Color.Black;
                hitBars.Label = "Hit";
                hitBars.ShowValuesAboveBars = true;
                hitBars.ValueFormatter = value =>
                {
                    int index = FindeNaechstenIndex(hitWerte, value, hitBars.Positions);
                    double gesamtTag = hitWerte[index] + missWerte[index];
                    double quote = gesamtTag > 0 ? (value / gesamtTag) * 100.0 : 0.0;
                    return $"{value:N0} ({quote:N1} %)";
                };

                var missBars = plot.AddBar(missWerte, missX);
                missBars.BarWidth = 0.32;
                missBars.FillColor = Color.IndianRed;
                missBars.BorderColor = Color.Black;
                missBars.Label = "Miss";
                missBars.ShowValuesAboveBars = true;
                missBars.ValueFormatter = value =>
                {
                    int index = FindeNaechstenIndex(missWerte, value, missBars.Positions);
                    double gesamtTag = hitWerte[index] + missWerte[index];
                    double quote = gesamtTag > 0 ? (value / gesamtTag) * 100.0 : 0.0;
                    return $"{value:N0} ({quote:N1} %)";
                };

                plot.XTicks(basisX, labels);
                plot.XAxis.TickLabelStyle(fontSize: 14, rotation: 0);
                plot.YAxis.TickLabelStyle(fontSize: 13);
                plot.Legend(location: ScottPlot.Alignment.UpperRight);
            }

            plot.Title("Hit/Miss pro Tag");
            plot.XLabel("Simulationstag");
            plot.YLabel("Anzahl Patienten");
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            plot.SetAxisLimits(yMin: 0);

            string outputPath = ErzeugeOutputPfad("hit_miss_pro_tag.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 12 gespeichert: {outputPath} ---");
        }

        private static int FindeNaechstenIndex(IReadOnlyList<double> werte, double wert, IReadOnlyList<double> positionen)
        {
            for (int i = 0; i < werte.Count; i++)
            {
                if (Math.Abs(werte[i] - wert) < 0.0001 && i < positionen.Count)
                    return i;
            }

            return 0;
        }
    }
}
