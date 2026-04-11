using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace simSharpSimulation
{
    internal static class GenerateDiagramme
    {
        private const string ImagesOrdner = "images";

        /// <summary>
        /// Erstellt alle Diagramme analog zur SimPy-Version:
        /// 1) Theorie: PDF + CDF
        /// 2) Simulation vs. Theorie: Histogramm der Ankünfte + PDF + CDF
        /// 3) Histogramm der Wartezeiten (Arzt)
        /// 4) Histogramm der Wartezeiten (Schwester)
        /// 5) Vergleich der Wartezeiten (Arzt vs. Schwester)
        /// </summary>
        public static void GeneriereDiagramme(
            IReadOnlyList<double> echteAnkunftszeiten,
            IReadOnlyList<double> wartezeiten,
            IReadOnlyList<double> schwesternWartezeiten,
            double simulationsdauer,
            double erwartungswert,
            double standardabweichung,
            int anzahlAerzte,
            int anzahlSchwestern)
        {
            Console.WriteLine("Generiere Diagramme (Wartezeiten, Ankünfte, Theorie)...");

            double[] x = Linspace(0, simulationsdauer, 500);

            double[] pdf = x
                .Select(v => MathNet.Numerics.Distributions.Normal.PDF(erwartungswert, standardabweichung, v))
                .ToArray();

            double[] cdf = x
                .Select(v => MathNet.Numerics.Distributions.Normal.CDF(erwartungswert, standardabweichung, v))
                .ToArray();

            ErzeugeTheorieDiagramm(x, pdf, cdf, erwartungswert, standardabweichung);
            ErzeugeAnkuenfteVergleichsDiagramm(echteAnkunftszeiten, x, pdf, cdf, simulationsdauer, erwartungswert, standardabweichung);
            ErzeugeWartezeitenDiagramm(wartezeiten, anzahlAerzte);
            ErzeugeSchwesternWartezeitenDiagramm(schwesternWartezeiten, anzahlSchwestern);
            ErzeugeWartezeitenVergleichsDiagramm(wartezeiten, schwesternWartezeiten, anzahlAerzte, anzahlSchwestern);
        }

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

        private static void ErzeugeAnkuenfteVergleichsDiagramm(
            IReadOnlyList<double> echteAnkunftszeiten,
            double[] x,
            double[] pdf,
            double[] cdf,
            double simulationsdauer,
            double erwartungswert,
            double standardabweichung)
        {
            int anzahlBins = 24;
            var (ankunftCounts, ankunftCenters, balkenBreite) = BuildHistogram(echteAnkunftszeiten, anzahlBins, 0, simulationsdauer);
            var plot = new ScottPlot.Plot(1000, 600);
            var bars = plot.AddBar(ankunftCounts, ankunftCenters);
            bars.BarWidth = balkenBreite * 0.9;
            bars.FillColor = Color.LightBlue;
            bars.BorderColor = Color.Black;
            bars.Label = "Tatsächliche Ankünfte (Histogramm)";
            double[] skaliertePdf = pdf.Select(v => v * echteAnkunftszeiten.Count * balkenBreite).ToArray();
            plot.AddScatter(x, skaliertePdf, color: Color.DarkBlue, lineWidth: 3, label: "PDF (Dichtefunktion)");
            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);
            var cdfLine = plot.AddScatter(x, cdf, color: Color.Red, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 3, label: "CDF (Verteilungsfunktion)");
            cdfLine.YAxisIndex = axisRight.AxisIndex;
            plot.Title($"Simulation vs. Theorie: Ankünfte\n(Mittelwert: {erwartungswert}, StdAbw: {standardabweichung})");
            plot.XLabel("Zeit der Simulation in Minuten (0 bis 480)");
            plot.YLabel("Anzahl der Patienten");
            axisRight.Label("Kumulierte Wahrscheinlichkeit (CDF)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);
            string outputPath = ErzeugeOutputPfad("ankuenfte_simulation_vs_theorie.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 2 gespeichert: {outputPath} ---");
        }

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

        private static string ErzeugeOutputPfad(string dateiname)
        {
            string imagesPfad = Path.Combine(Directory.GetCurrentDirectory(), ImagesOrdner);
            Directory.CreateDirectory(imagesPfad);
            return Path.Combine(imagesPfad, dateiname);
        }

        private static double[] Linspace(double start, double end, int count)
        {
            if (count < 2)
                return new[] { start };
            double step = (end - start) / (count - 1);
            return Enumerable.Range(0, count).Select(i => start + i * step).ToArray();
        }

        private static (double[] counts, double[] centers, double binWidth) BuildHistogram(
            IReadOnlyList<double> values,
            int binCount,
            double min,
            double max)
        {
            double binWidth = (max - min) / binCount;
            double[] counts = new double[binCount];
            double[] centers = new double[binCount];
            for (int i = 0; i < binCount; i++)
                centers[i] = min + (i + 0.5) * binWidth;
            foreach (double value in values)
            {
                if (value < min || value > max)
                    continue;
                int index = (int)((value - min) / binWidth);
                if (index == binCount)
                    index = binCount - 1;
                counts[index]++;
            }
            return (counts, centers, binWidth);
        }
    }
}
