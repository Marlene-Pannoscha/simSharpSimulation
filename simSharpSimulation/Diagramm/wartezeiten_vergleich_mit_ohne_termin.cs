using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

#pragma warning disable CA1416

namespace simSharpSimulation
{
    // Gemeinsamer Boxplot-Vergleich der stationsbezogenen Wartezeiten nach Terminstatus.
    internal static partial class GenerateDiagramme
    {
        private const double TerminstatusBoxplotSkalenbruchStart = 50.0;
        private const double TerminstatusBoxplotSkalenbruchEnde = 250.0;
        private const double TerminstatusBoxplotSkalenbruchAngezeigteSpanne = 24.0;

        private static void ErzeugeGemeinsamesWartezeitenVergleichsDiagramm(
            IReadOnlyList<double> arztMitTermin,
            IReadOnlyList<double> arztOhneTermin,
            IReadOnlyList<double> schwesterMitTermin,
            IReadOnlyList<double> schwesterOhneTermin)
        {
            Color mitTerminFarbe = Color.FromArgb(55, 116, 181);
            Color ohneTerminFarbe = Color.FromArgb(231, 120, 70);
            (string Station, string Terminstatus, IReadOnlyList<double> Werte, Color Farbe)[] gruppen =
            {
                ("Arzt", "mit Termin", arztMitTermin, mitTerminFarbe),
                ("Arzt", "ohne Termin", arztOhneTermin, ohneTerminFarbe),
                ("Schwester", "mit Termin", schwesterMitTermin, mitTerminFarbe),
                ("Schwester", "ohne Termin", schwesterOhneTermin, ohneTerminFarbe)
            };

            if (gruppen.All(g => g.Werte.Count == 0))
                return;

            const int breite = 1400;
            const int hoehe = 900;
            string outputPath = ErzeugeOutputPfad("wartezeiten_vergleich_mit_ohne_termin.png");

            using Bitmap bitmap = new(breite, hoehe);
            using Graphics g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            RectangleF plotArea = new(135, 160, breite - 205, 470);
            using Font titelFont = new("Arial", 22, FontStyle.Bold);
            using Font untertitelFont = new("Arial", 12, FontStyle.Regular);
            using Font achsenFont = new("Arial", 12, FontStyle.Regular);
            using Font labelFont = new("Arial", 12, FontStyle.Regular);
            using Font stationFont = new("Arial", 15, FontStyle.Bold);
            using Font kleinFont = new("Arial", 10, FontStyle.Regular);
            using Pen achsenPen = new(Color.FromArgb(45, 50, 55), 1.3f);
            using Pen gridPen = new(Color.FromArgb(224, 229, 234), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            using Pen trennPen = new(Color.FromArgb(190, 198, 206), 1.2f);
            using Brush textBrush = new SolidBrush(Color.Black);
            using Brush hintergrundLinks = new SolidBrush(Color.FromArgb(248, 251, 255));
            using Brush hintergrundRechts = new SolidBrush(Color.FromArgb(250, 250, 250));
            using Pen rahmenPen = new(Color.FromArgb(218, 224, 230), 1);

            g.DrawString(
                "Wartezeitenvergleich nach Terminstatus",
                titelFont,
                textBrush,
                58,
                24);
            g.DrawString(
                "Stationsbezogene Wartezeit ab Betreten des jeweiligen Wartebereichs bis Behandlungsstart",
                untertitelFont,
                Brushes.DimGray,
                60,
                76);

            double maxY = gruppen
                .Select(g => BerechneBoxplotStatistik(g.Werte).Max)
                .DefaultIfEmpty(1.0)
                .Max();
            maxY = Math.Max(1.0, maxY * 1.12);
            BoxplotSkala skala = ErzeugeBoxplotSkala(
                maxY,
                TerminstatusBoxplotSkalenbruchStart,
                TerminstatusBoxplotSkalenbruchEnde,
                TerminstatusBoxplotSkalenbruchAngezeigteSpanne);

            RectangleF arztBereich = new(plotArea.Left, plotArea.Top, plotArea.Width / 2, plotArea.Height);
            RectangleF schwesterBereich = new(plotArea.Left + plotArea.Width / 2, plotArea.Top, plotArea.Width / 2, plotArea.Height);
            g.FillRectangle(hintergrundLinks, arztBereich);
            g.FillRectangle(hintergrundRechts, schwesterBereich);
            g.DrawRectangle(rahmenPen, plotArea.Left, plotArea.Top, plotArea.Width, plotArea.Height);
            g.DrawLine(trennPen, plotArea.Left + plotArea.Width / 2, plotArea.Top, plotArea.Left + plotArea.Width / 2, plotArea.Bottom);

            foreach (double wert in ErzeugeBoxplotAchsenwerte(maxY, skala))
            {
                float y = SkaliereY(wert, 0.0, maxY, plotArea, skala);
                g.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
                string label = wert.ToString("N1", BoxplotCulture);
                SizeF labelSize = g.MeasureString(label, achsenFont);
                g.DrawString(label, achsenFont, textBrush, plotArea.Left - labelSize.Width - 10, y - labelSize.Height / 2);
            }

            g.DrawLine(achsenPen, plotArea.Left, plotArea.Bottom, plotArea.Right, plotArea.Bottom);
            g.DrawLine(achsenPen, plotArea.Left, plotArea.Top, plotArea.Left, plotArea.Bottom);
            if (skala.IstKomprimiert)
                ZeichneBoxplotSkalenbruch(g, plotArea, maxY, skala, achsenPen, kleinFont);

            ZeichneGedrehtenText(g, "Wartezeit in Minuten", achsenFont, textBrush, 28, plotArea.Top + plotArea.Height / 2 + 62);

            ZeichneLegende(g, "mit Termin", "ohne Termin", mitTerminFarbe, ohneTerminFarbe, new PointF(breite - 310, 34));

            for (int i = 0; i < gruppen.Length; i++)
            {
                float x = plotArea.Left + plotArea.Width * ((i + 0.5f) / gruppen.Length);
                BoxplotStatistik statistik = BerechneBoxplotStatistik(gruppen[i].Werte);
                ZeichneBoxplot(
                    g,
                    plotArea,
                    statistik,
                    gruppen[i].Werte.Count,
                    x,
                    0.0,
                    maxY,
                    skala,
                    gruppen[i].Farbe,
                    kleinFont,
                    i % 2 == 0,
                    false);

                ZeichneStatistikUnterBoxplot(
                    g,
                    x,
                    plotArea.Bottom + 18,
                    gruppen[i].Werte.Count,
                    statistik.Median,
                    kleinFont,
                    gruppen[i].Farbe);

                ZeichneZentriertenText(g, gruppen[i].Terminstatus, labelFont, textBrush, x, plotArea.Bottom + 92);
            }

            ZeichneZentriertenText(g, "Arzt", stationFont, textBrush, plotArea.Left + plotArea.Width * 0.25f, plotArea.Bottom + 134);
            ZeichneZentriertenText(g, "Schwester", stationFont, textBrush, plotArea.Left + plotArea.Width * 0.75f, plotArea.Bottom + 134);

            using Font hinweisFont = new("Arial", 11, FontStyle.Italic);
            g.DrawString(
                "Box: Q1-Median-Q3, Linien: Minimum/Maximum. Grundlage sind die simulierten stationsbezogenen Wartezeiten.",
                hinweisFont,
                Brushes.DimGray,
                plotArea.Left,
                hoehe - 48);

            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"--- Diagramm gemeinsamer Wartezeitenvergleich gespeichert: {outputPath} ---");
        }

        private static void ZeichneStatistikUnterBoxplot(
            Graphics g,
            float centerX,
            float y,
            int anzahl,
            double median,
            Font font,
            Color akzentFarbe)
        {
            string text = $"n = {anzahl}\nMedian = {median.ToString("N1", BoxplotCulture)} min";
            SizeF textGroesse = g.MeasureString(text, font);
            const float horizontalerInnenabstand = 10;
            const float vertikalerInnenabstand = 6;
            float breite = textGroesse.Width + horizontalerInnenabstand * 2;
            float hoehe = textGroesse.Height + vertikalerInnenabstand * 2;
            RectangleF karte = new(
                centerX - breite / 2,
                y,
                breite,
                hoehe);

            using Brush hintergrund = new SolidBrush(Color.FromArgb(248, 250, 252));
            using Pen rahmen = new(Color.FromArgb(210, 216, 222), 1);
            using Pen akzent = new(akzentFarbe, 3);
            g.FillRectangle(hintergrund, karte);
            g.DrawRectangle(rahmen, karte.Left, karte.Top, karte.Width, karte.Height);
            g.DrawLine(akzent, karte.Left, karte.Top, karte.Left, karte.Bottom);
            g.DrawString(
                text,
                font,
                Brushes.Black,
                karte.Left + horizontalerInnenabstand,
                karte.Top + vertikalerInnenabstand);
        }

        private static void ZeichneGedrehtenText(
            Graphics g,
            string text,
            Font font,
            Brush brush,
            float x,
            float y)
        {
            var zustand = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(-90);
            g.DrawString(text, font, brush, 0, 0);
            g.Restore(zustand);
        }
    }
}
