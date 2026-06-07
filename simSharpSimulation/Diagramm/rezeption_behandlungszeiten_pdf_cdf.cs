using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Rezeption-Prozesszeiten als Histogramm mit Lognormal PDF/CDF.
    internal static partial class GenerateDiagramme
    {
        private static void ErzeugeRezeptionBehandlungszeitenPdfCdfDiagramm(
            IReadOnlyList<double> behandlungszeiten)
        {
            if (behandlungszeiten == null || behandlungszeiten.Count == 0)
                return;

            double erwartungswert = RezeptionKonfiguration.MITTELREZEPTIONSZEIT;
            double variationskoeffizient = RezeptionKonfiguration.VARIATIONSKOEFFIZIENT_REZEPTION;
            double sigma = Math.Sqrt(Math.Log(1 + Math.Pow(variationskoeffizient, 2)));
            double mu = Math.Log(erwartungswert) - 0.5 * Math.Pow(sigma, 2);

            double maxZeit = Math.Max(behandlungszeiten.Max(), erwartungswert * 3.0);
            maxZeit = Math.Max(maxZeit, 1.0);
            double minZeit = 1e-6;

            double[] x = Linspace(minZeit, maxZeit, 400);
            double[] pdf = x
                .Select(v => MathNet.Numerics.Distributions.LogNormal.PDF(mu, sigma, v))
                .Select(v => double.IsFinite(v) ? v : 0.0)
                .ToArray();
            double[] cdf = x
                .Select(v => MathNet.Numerics.Distributions.LogNormal.CDF(mu, sigma, v))
                .Select(v => double.IsFinite(v) ? v : 0.0)
                .ToArray();

            var plot = new ScottPlot.Plot(1000, 600);

            int bins = Math.Max(10, Math.Min(30, (int)Math.Ceiling(behandlungszeiten.Count / 5.0)));
            var (counts, centers, binWidth) = BuildHistogram(behandlungszeiten, bins, 0, maxZeit);
            var bars = plot.AddBar(counts, centers);
            bars.BarWidth = binWidth * 0.9;
            bars.FillColor = Color.MediumPurple;
            bars.BorderColor = Color.Black;
            bars.Label = "Histogramm";

            double scale = behandlungszeiten.Count * binWidth;
            plot.AddScatter(x, pdf.Select(v => v * scale).ToArray(), color: Color.Indigo, lineWidth: 3, label: "PDF (Lognormal)");

            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);
            var cdfLine = plot.AddScatter(x, cdf, color: Color.DarkRed, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 3, label: "CDF (Lognormal)");
            cdfLine.YAxisIndex = axisRight.AxisIndex;

            plot.Title("Rezeption-Prozesszeiten (Lognormal)");
            plot.XLabel("Prozesszeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            axisRight.Label("Kumulierte Wahrscheinlichkeit (CDF)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("rezeption_behandlungszeiten_pdf_cdf.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 11 gespeichert (Rezeption-Prozesszeiten): {outputPath} ---");
        }
    }
}
