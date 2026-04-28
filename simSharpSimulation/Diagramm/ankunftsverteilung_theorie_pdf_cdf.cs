using System;
using System.Drawing;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 1 - theoretische Ankunftsverteilung (Normalverteilung) als PDF/CDF.
    internal static partial class GenerateDiagramme
    {
        // Diagramm 1
        private static void ErzeugeTheorieDiagramm(double[] x, double[] pdf, double[] cdf, double erwartungswert, double standardabweichung)
        {
            var plot = new ScottPlot.Plot(1000, 600);
            plot.AddScatter(x, pdf, color: Color.RoyalBlue, lineWidth: 2, label: "PDF (Dichtefunktion)");

            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);
            var cdfLine = plot.AddScatter(x, cdf, color: Color.Red, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 2, label: "CDF (Verteilungsfunktion)");
            cdfLine.YAxisIndex = axisRight.AxisIndex;

            plot.Title($"Theoretische Patientenankünfte (Normalverteilung)\n(Erwartungswert: {erwartungswert}, StdAbw: {standardabweichung})");
            plot.XLabel("Zeit in Minuten (0 bis 480)");
            plot.YLabel("Wahrscheinlichkeitsdichte (PDF)");
            axisRight.Label("Kumulierte Wahrscheinlichkeit (CDF)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);

            string outputPath = ErzeugeOutputPfad("ankunftsverteilung_theorie_pdf_cdf.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 1 gespeichert: {outputPath} ---");
        }
    }
}
