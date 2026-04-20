using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 11 - Wartezeiten mit/ohne Termin inkl. Exponential-PDF und CDF.
    internal static partial class GenerateDiagramme
    {
        // Diagramm 11
        private static void ErzeugeWartezeitenTheorieExponentialDiagramm(
            IReadOnlyList<double> wartezeitenMitTermin,
            IReadOnlyList<double> wartezeitenOhneTermin)
        {
            if ((wartezeitenMitTermin == null || wartezeitenMitTermin.Count == 0) &&
                (wartezeitenOhneTermin == null || wartezeitenOhneTermin.Count == 0))
                return;

            wartezeitenMitTermin ??= Array.Empty<double>();
            wartezeitenOhneTermin ??= Array.Empty<double>();

            var plot = new ScottPlot.Plot(1100, 700);
            int bins = 20;

            double maxWartezeit = Math.Max(
                wartezeitenMitTermin.Count > 0 ? wartezeitenMitTermin.Max() : 0,
                wartezeitenOhneTermin.Count > 0 ? wartezeitenOhneTermin.Max() : 0);
            maxWartezeit = Math.Max(maxWartezeit, 1.0);

            var (countsMitTermin, centers, binWidth) = BuildHistogram(wartezeitenMitTermin, bins, 0, maxWartezeit);
            var (countsOhneTermin, _, _) = BuildHistogram(wartezeitenOhneTermin, bins, 0, maxWartezeit);

            double offset = binWidth * 0.2;

            var barsMitTermin = plot.AddBar(countsMitTermin, centers.Select(c => c - offset).ToArray());
            barsMitTermin.BarWidth = binWidth * 0.35;
            barsMitTermin.FillColor = Color.SteelBlue;
            barsMitTermin.BorderColor = Color.Black;
            barsMitTermin.Label = "Histogramm: mit Termin";

            var barsOhneTermin = plot.AddBar(countsOhneTermin, centers.Select(c => c + offset).ToArray());
            barsOhneTermin.BarWidth = binWidth * 0.35;
            barsOhneTermin.FillColor = Color.Orange;
            barsOhneTermin.BorderColor = Color.Black;
            barsOhneTermin.Label = "Histogramm: ohne Termin";

            double[] x = Linspace(0, maxWartezeit, 400);
            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);

            if (wartezeitenMitTermin.Count > 0)
            {
                double meanMitTermin = Math.Max(wartezeitenMitTermin.Average(), 1e-9);
                double lambdaMitTermin = 1.0 / meanMitTermin;
                double[] pdfMitTermin = x.Select(v => lambdaMitTermin * Math.Exp(-lambdaMitTermin * v)).ToArray();
                double[] cdfMitTermin = x.Select(v => 1.0 - Math.Exp(-lambdaMitTermin * v)).ToArray();

                double scaleMitTermin = wartezeitenMitTermin.Count * binWidth;
                plot.AddScatter(
                    x,
                    pdfMitTermin.Select(v => v * scaleMitTermin).ToArray(),
                    color: Color.DarkBlue,
                    lineWidth: 2.5f,
                    label: "PDF Exponential: mit Termin");

                var cdfMitTerminLine = plot.AddScatter(
                    x,
                    cdfMitTermin,
                    color: Color.MidnightBlue,
                    lineStyle: ScottPlot.LineStyle.Dash,
                    lineWidth: 2.0f,
                    label: "CDF Exponential: mit Termin");
                cdfMitTerminLine.YAxisIndex = axisRight.AxisIndex;
            }

            if (wartezeitenOhneTermin.Count > 0)
            {
                double meanOhneTermin = Math.Max(wartezeitenOhneTermin.Average(), 1e-9);
                double lambdaOhneTermin = 1.0 / meanOhneTermin;
                double[] pdfOhneTermin = x.Select(v => lambdaOhneTermin * Math.Exp(-lambdaOhneTermin * v)).ToArray();
                double[] cdfOhneTermin = x.Select(v => 1.0 - Math.Exp(-lambdaOhneTermin * v)).ToArray();

                double scaleOhneTermin = wartezeitenOhneTermin.Count * binWidth;
                plot.AddScatter(
                    x,
                    pdfOhneTermin.Select(v => v * scaleOhneTermin).ToArray(),
                    color: Color.DarkOrange,
                    lineWidth: 2.5f,
                    label: "PDF Exponential: ohne Termin");

                var cdfOhneTerminLine = plot.AddScatter(
                    x,
                    cdfOhneTermin,
                    color: Color.IndianRed,
                    lineStyle: ScottPlot.LineStyle.Dash,
                    lineWidth: 2.0f,
                    label: "CDF Exponential: ohne Termin");
                cdfOhneTerminLine.YAxisIndex = axisRight.AxisIndex;
            }

            plot.Title("Wartezeiten-Theorie (Exponential): mit Termin vs. ohne Termin");
            plot.XLabel("Wartezeit in Minuten");
            plot.YLabel("Anzahl der Patienten / skalierte PDF");
            axisRight.Label("Kumulierte Wahrscheinlichkeit (CDF)");
            plot.Legend(location: ScottPlot.Alignment.UpperRight);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("wartezeiten_theorie_exponential_mit_ohne_termin.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 11 gespeichert: {outputPath} ---");
        }
    }
}
