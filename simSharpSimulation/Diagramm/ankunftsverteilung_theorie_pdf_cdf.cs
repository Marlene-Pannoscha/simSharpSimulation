using System;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    internal static partial class GenerateDiagramme
    {
        private static void ErzeugeTheorieDiagramm(
            double[] x,
            double[] terminPdf,
            double[] terminCdf,
            double simulationsdauer,
            double erwartungswert,
            double standardabweichung)
        {
            var plot = new ScottPlot.Plot(1000, 600);
            plot.AddScatter(x, terminPdf, color: Color.RoyalBlue, lineWidth: 2, label: "Mit Termin: Normal-PDF");

            double freezeZeitpunkt = Math.Max(
                0.0,
                simulationsdauer - SimulationKonfiguration.PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN);
            double[] poissonPdf = x.Select(BerechneOhneTerminRateProMinute).ToArray();
            plot.AddScatter(x, poissonPdf, color: Color.DarkOrange, lineWidth: 2, label: "Ohne Termin: Exponential-Rate");
            plot.AddScatter(
                new[] { freezeZeitpunkt, freezeZeitpunkt },
                new[] { 0.0, poissonPdf.DefaultIfEmpty(0.0).Max() * 1.15 },
                color: Color.DarkGreen,
                lineStyle: ScottPlot.LineStyle.Dot,
                lineWidth: 2,
                label: "Queue-Freeze / Aufnahmeprognose");

            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);
            var terminCdfLine = plot.AddScatter(x, terminCdf, color: Color.Red, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 2, label: "Mit Termin: Normal-CDF");
            terminCdfLine.YAxisIndex = axisRight.AxisIndex;

            double[] poissonCdf = x.Select(BerechneOhneTerminKumuliertenAnteil).ToArray();
            var poissonCdfLine = plot.AddScatter(x, poissonCdf, color: Color.SaddleBrown, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 2, label: "Ohne Termin: kumulierter Anteil");
            poissonCdfLine.YAxisIndex = axisRight.AxisIndex;

            plot.Title($"Theoretische Patientenankuenfte\nMit Termin: Normalverteilung | Ohne Termin: Exponential mit Tagesanteilen, Queue-Freeze bei Minute {freezeZeitpunkt:N0}");
            plot.XLabel("Zeit in Minuten (0 bis 480)");
            plot.YLabel("Dichte / Rate");
            axisRight.Label("Kumulierte Wahrscheinlichkeit / Rate");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);

            string outputPath = ErzeugeOutputPfad("ankunftsverteilung_theorie_pdf_cdf.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 1 gespeichert: {outputPath} ---");
        }
    }
}
