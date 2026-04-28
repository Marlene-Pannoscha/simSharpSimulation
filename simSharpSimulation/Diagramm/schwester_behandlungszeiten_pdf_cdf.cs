using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 10 - Schwester-Behandlungszeiten je Patiententyp (Histogramm + Lognormal PDF/CDF).
    internal static partial class GenerateDiagramme
    {
        // Diagramm: Schwester-Behandlungszeiten je Patiententyp
        private static void ErzeugeSchwesterBehandlungszeitenPdfCdfDiagramm(
            IReadOnlyList<double> behandlungszeiten,
            PatientenTyp typ,
            double mittlereBehandlungszeit,
            double sigma = 0.35)
        {
            if (behandlungszeiten == null || behandlungszeiten.Count == 0)
                return;

            double mu = Math.Log(mittlereBehandlungszeit) - 0.5 * Math.Pow(sigma, 2);
            double maxZeit = Math.Max(behandlungszeiten.Max(), mittlereBehandlungszeit * 3.0);
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
            bars.FillColor = Color.PeachPuff;
            bars.BorderColor = Color.Black;
            bars.Label = "Histogramm";

            double scale = behandlungszeiten.Count * binWidth;
            plot.AddScatter(x, pdf.Select(v => v * scale).ToArray(), color: Color.DarkOrange, lineWidth: 3, label: "PDF (Lognormal)");

            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);
            var cdfLine = plot.AddScatter(x, cdf, color: Color.DarkRed, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 3, label: "CDF (Lognormal)");
            cdfLine.YAxisIndex = axisRight.AxisIndex;

            plot.Title($"Schwester-Behandlungszeiten {typ} (Lognormal)");
            plot.XLabel("Behandlungszeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            axisRight.Label("Kumulierte Wahrscheinlichkeit (CDF)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad($"schwester_behandlungszeiten_{typ.ToString().ToLowerInvariant()}_pdf_cdf.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 10 gespeichert (Schwester, Typ {typ}): {outputPath} ---");
        }
    }
}
