using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

// Das Rendering verwendet GDI+ und ist wie die gesamte WPF-Anwendung auf Windows beschraenkt.
#pragma warning disable CA1416

namespace simSharpSimulation
{
    internal static partial class GenerateDiagramme
    {
        private static void ErzeugeSzenarioBoxplotDiagramm(
            string titel,
            string untertitel,
            SzenarioBoxplotDaten erstesSzenario,
            SzenarioBoxplotDaten zweitesSzenario,
            string dateiname,
            int diagrammNummer,
            double boxplotSkalenbruchEnde = BoxplotSkalenbruchEnde)
        {
            const int breite = 1800;
            const int hoehe = 1040;
            BoxplotGruppe[] gruppen =
            {
                new("Wartezeit (min)", erstesSzenario.Wartezeiten, zweitesSzenario.Wartezeiten),
                new("Personalauslastung (%)", erstesSzenario.PersonalAuslastungenProzent, zweitesSzenario.PersonalAuslastungenProzent),
                new("Warteschlange (Patienten)", erstesSzenario.Warteschlangenlaengen, zweitesSzenario.Warteschlangenlaengen)
            };

            Color basisFarbe = Color.FromArgb(55, 116, 181);
            Color vergleichFarbe = Color.FromArgb(41, 150, 93);
            string outputPath = ErzeugeOutputPfad(dateiname);

            using Bitmap bitmap = new(breite, hoehe);
            using Graphics g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            using Font panelTitelFont = new("Arial", 14, FontStyle.Bold);
            using Font achsenFont = new("Arial", 12, FontStyle.Regular);
            using Font kleinFont = new("Arial", 11, FontStyle.Regular);
            using Brush textBrush = new SolidBrush(Color.Black);
            using Pen achsenPen = new(Color.FromArgb(45, 50, 55), 1.2f);

            ZeichneLegende(g, erstesSzenario.Name, zweitesSzenario.Name, basisFarbe, vergleichFarbe, new PointF(breite - 500, 26));

            RectangleF[] panels =
            {
                new(60, 82, 800, 410),
                new(940, 82, 800, 410),
                new(60, 560, 800, 410),
                new(940, 560, 800, 410)
            };

            ZeichneBoxplotPanel(
                g,
                panels[0],
                gruppen[0],
                erstesSzenario.Name,
                zweitesSzenario.Name,
                basisFarbe,
                vergleichFarbe,
                panelTitelFont,
                achsenFont,
                kleinFont,
                textBrush,
                achsenPen,
                boxplotSkalenbruchEnde);
            ZeichneBoxplotPanel(
                g,
                panels[1],
                gruppen[1],
                erstesSzenario.Name,
                zweitesSzenario.Name,
                basisFarbe,
                vergleichFarbe,
                panelTitelFont,
                achsenFont,
                kleinFont,
                textBrush,
                achsenPen,
                boxplotSkalenbruchEnde);
            ZeichneKostenPanel(
                g,
                panels[2],
                erstesSzenario,
                zweitesSzenario,
                panelTitelFont,
                achsenFont,
                kleinFont,
                textBrush,
                achsenPen);
            ZeichneBoxplotPanel(
                g,
                panels[3],
                gruppen[2],
                erstesSzenario.Name,
                zweitesSzenario.Name,
                basisFarbe,
                vergleichFarbe,
                panelTitelFont,
                achsenFont,
                kleinFont,
                textBrush,
                achsenPen,
                boxplotSkalenbruchEnde);

            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"--- Diagramm {diagrammNummer} gespeichert: {outputPath} ---");
        }

        private static void ZeichneLegende(
            Graphics g,
            string erstesLabel,
            string zweitesLabel,
            Color erstesFarbe,
            Color zweitesFarbe,
            PointF start)
        {
            using Font font = new("Arial", 14, FontStyle.Regular);
            using Brush erstesBrush = new SolidBrush(erstesFarbe);
            using Brush zweitesBrush = new SolidBrush(zweitesFarbe);
            using Pen rahmenPen = new(Color.FromArgb(80, 80, 80), 1);
            g.FillRectangle(erstesBrush, start.X, start.Y, 22, 14);
            g.DrawRectangle(rahmenPen, start.X, start.Y, 22, 14);
            g.DrawString(erstesLabel, font, Brushes.Black, start.X + 32, start.Y - 4);
            g.FillRectangle(zweitesBrush, start.X, start.Y + 28, 22, 14);
            g.DrawRectangle(rahmenPen, start.X, start.Y + 28, 22, 14);
            g.DrawString(zweitesLabel, font, Brushes.Black, start.X + 32, start.Y + 24);
        }

        private static void ZeichneBoxplotPanel(
            Graphics g,
            RectangleF panel,
            BoxplotGruppe gruppe,
            string erstesLabel,
            string zweitesLabel,
            Color erstesFarbe,
            Color zweitesFarbe,
            Font titelFont,
            Font achsenFont,
            Font kleinFont,
            Brush textBrush,
            Pen achsenPen,
            double boxplotSkalenbruchEnde)
        {
            RectangleF plotArea = new(panel.Left + 104, panel.Top + 46, panel.Width - 154, panel.Height - 126);
            BoxplotStatistik erstes = BerechneBoxplotStatistik(gruppe.ErstesSzenario);
            BoxplotStatistik zweites = BerechneBoxplotStatistik(gruppe.ZweitesSzenario);
            double maxY = Math.Max(1.0, Math.Max(erstes.Max, zweites.Max));
            double yMax = maxY * 1.12;
            BoxplotSkala skala = ErzeugeBoxplotSkala(yMax, boxplotSkalenbruchEnde);

            using Pen gridPen = new(Color.FromArgb(224, 229, 234), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            using Pen panelPen = new(Color.FromArgb(224, 228, 232), 1);
            using Font szenarioFont = new("Arial", 12, FontStyle.Bold);
            ZeichneTitelUeberPanel(g, gruppe.Name, titelFont, textBrush, panel);
            g.DrawRectangle(panelPen, panel.Left, panel.Top, panel.Width, panel.Height);

            foreach (double wert in ErzeugeBoxplotAchsenwerte(yMax, skala))
            {
                float y = SkaliereY(wert, 0.0, yMax, plotArea, skala);
                g.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
                string label = wert.ToString("N1", BoxplotCulture);
                SizeF labelSize = g.MeasureString(label, achsenFont);
                g.DrawString(label, achsenFont, Brushes.Black, plotArea.Left - labelSize.Width - 8, y - labelSize.Height / 2);
            }

            g.DrawLine(achsenPen, plotArea.Left, plotArea.Bottom, plotArea.Right, plotArea.Bottom);
            g.DrawLine(achsenPen, plotArea.Left, plotArea.Top, plotArea.Left, plotArea.Bottom);
            if (skala.IstKomprimiert)
                ZeichneBoxplotSkalenbruch(g, plotArea, yMax, skala, achsenPen, kleinFont);

            float erstesX = plotArea.Left + plotArea.Width * 0.33f;
            float zweitesX = plotArea.Left + plotArea.Width * 0.67f;
            ZeichneBoxplot(g, plotArea, erstes, gruppe.ErstesSzenario.Count, erstesX, 0.0, yMax, skala, erstesFarbe, kleinFont, true);
            ZeichneBoxplot(g, plotArea, zweites, gruppe.ZweitesSzenario.Count, zweitesX, 0.0, yMax, skala, zweitesFarbe, kleinFont, false);

            ZeichneZentriertenText(g, FormatiereSzenarioAchsenLabel(erstesLabel), szenarioFont, Brushes.Black, erstesX, plotArea.Bottom + 18);
            ZeichneZentriertenText(g, FormatiereSzenarioAchsenLabel(zweitesLabel), szenarioFont, Brushes.Black, zweitesX, plotArea.Bottom + 18);
        }

        private static void ZeichneKostenPanel(
            Graphics g,
            RectangleF panel,
            SzenarioBoxplotDaten erstesSzenario,
            SzenarioBoxplotDaten zweitesSzenario,
            Font titelFont,
            Font achsenFont,
            Font kleinFont,
            Brush textBrush,
            Pen achsenPen)
        {
            RectangleF plotArea = new(panel.Left + 104, panel.Top + 46, panel.Width - 250, panel.Height - 126);
            double maxY = Math.Max(erstesSzenario.Kosten.Gesamt, zweitesSzenario.Kosten.Gesamt) * 1.12;
            maxY = Math.Max(1.0, maxY);
            using Font szenarioFont = new("Arial", 12, FontStyle.Bold);
            using Font legendaFont = new("Arial", 10, FontStyle.Regular);

            using Pen gridPen = new(Color.FromArgb(224, 229, 234), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            using Pen panelPen = new(Color.FromArgb(224, 228, 232), 1);
            ZeichneTitelUeberPanel(g, "Kostenstruktur (EUR/Tag)", titelFont, textBrush, panel);
            g.DrawRectangle(panelPen, panel.Left, panel.Top, panel.Width, panel.Height);

            for (int i = 0; i <= 4; i++)
            {
                double wert = maxY * i / 4.0;
                float y = SkaliereY(wert, 0.0, maxY, plotArea);
                g.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
                string label = wert.ToString("N0", BoxplotCulture);
                SizeF labelSize = g.MeasureString(label, achsenFont);
                g.DrawString(label, achsenFont, Brushes.Black, plotArea.Left - labelSize.Width - 8, y - labelSize.Height / 2);
            }

            g.DrawLine(achsenPen, plotArea.Left, plotArea.Bottom, plotArea.Right, plotArea.Bottom);
            g.DrawLine(achsenPen, plotArea.Left, plotArea.Top, plotArea.Left, plotArea.Bottom);

            float erstesX = plotArea.Left + plotArea.Width * 0.29f;
            float zweitesX = plotArea.Left + plotArea.Width * 0.71f;
            ZeichneKostenBalken(g, plotArea, erstesSzenario.Kosten, erstesX, maxY, kleinFont);
            ZeichneKostenBalken(g, plotArea, zweitesSzenario.Kosten, zweitesX, maxY, kleinFont);

            ZeichneKostenLegendeImPanel(g, panel, legendaFont);

            ZeichneZentriertenText(g, FormatiereSzenarioAchsenLabel(erstesSzenario.Name), szenarioFont, Brushes.Black, erstesX, plotArea.Bottom + 18);
            ZeichneZentriertenText(g, FormatiereSzenarioAchsenLabel(zweitesSzenario.Name), szenarioFont, Brushes.Black, zweitesX, plotArea.Bottom + 18);
        }

        private static void ZeichneKostenBalken(
            Graphics g,
            RectangleF plotArea,
            KostenKomponenten kosten,
            float centerX,
            double maxY,
            Font kleinFont)
        {
            float breite = 86;
            double unten = 0.0;
            (double Wert, Color Farbe)[] teile =
            {
                (kosten.Personal, Color.FromArgb(70, 130, 180)),
                (kosten.Fix, Color.FromArgb(241, 196, 15)),
                (kosten.Material, Color.FromArgb(231, 120, 70))
            };

            foreach ((double wert, Color farbe) in teile)
            {
                float yOben = SkaliereY(unten + wert, 0.0, maxY, plotArea);
                float yUnten = SkaliereY(unten, 0.0, maxY, plotArea);
                using Brush brush = new SolidBrush(Color.FromArgb(210, farbe));
                using Pen pen = new(Color.FromArgb(90, 90, 90), 1);
                g.FillRectangle(brush, centerX - breite / 2, yOben, breite, Math.Max(1, yUnten - yOben));
                g.DrawRectangle(pen, centerX - breite / 2, yOben, breite, Math.Max(1, yUnten - yOben));
                unten += wert;
            }

            string summe = kosten.Gesamt.ToString("N0", BoxplotCulture) + " EUR";
            ZeichneZentriertenText(g, summe, kleinFont, Brushes.Black, centerX, SkaliereY(kosten.Gesamt, 0.0, maxY, plotArea) - 22);
        }

        private static void ZeichneKostenLegendeImPanel(Graphics g, RectangleF panel, Font font)
        {
            (string Label, Color Farbe)[] items =
            {
                ("Personal", Color.FromArgb(70, 130, 180)),
                ("Fixkosten", Color.FromArgb(241, 196, 15)),
                ("Material", Color.FromArgb(231, 120, 70))
            };

            float x = panel.Right - 166;
            float y = panel.Top + 84;
            using Brush textBrush = new SolidBrush(Color.Black);
            using Pen rahmenPen = new(Color.FromArgb(90, 90, 90), 1);

            foreach ((string label, Color farbe) in items)
            {
                using Brush brush = new SolidBrush(Color.FromArgb(210, farbe));
                g.FillRectangle(brush, x, y + 4, 16, 11);
                g.DrawRectangle(rahmenPen, x, y + 4, 16, 11);
                g.DrawString(label, font, textBrush, x + 22, y);
                y += 22;
            }
        }

        private static void ZeichneBoxplot(
            Graphics g,
            RectangleF plotArea,
            BoxplotStatistik statistik,
            int anzahl,
            float x,
            double minY,
            double maxY,
            BoxplotSkala skala,
            Color farbe,
            Font kleinFont,
            bool beschriftungLinks,
            bool infoAnzeigen = true)
        {
            float boxHalbeBreite = 58;
            float min = SkaliereY(statistik.Min, minY, maxY, plotArea, skala);
            float q1 = SkaliereY(statistik.Q1, minY, maxY, plotArea, skala);
            float median = SkaliereY(statistik.Median, minY, maxY, plotArea, skala);
            float q3 = SkaliereY(statistik.Q3, minY, maxY, plotArea, skala);
            float max = SkaliereY(statistik.Max, minY, maxY, plotArea, skala);

            using Brush boxBrush = new SolidBrush(Color.FromArgb(105, farbe));
            using Pen farbPen = new(farbe, 3.0f);
            using Pen medianPen = new(Color.FromArgb(35, 35, 35), 2.4f);

            float boxTop = Math.Min(q1, q3);
            float boxHeight = Math.Max(2, Math.Abs(q3 - q1));
            g.FillRectangle(boxBrush, x - boxHalbeBreite, boxTop, boxHalbeBreite * 2, boxHeight);
            g.DrawRectangle(farbPen, x - boxHalbeBreite, boxTop, boxHalbeBreite * 2, boxHeight);
            g.DrawLine(medianPen, x - boxHalbeBreite, median, x + boxHalbeBreite, median);
            g.DrawLine(farbPen, x, min, x, q1);
            g.DrawLine(farbPen, x, q3, x, max);
            g.DrawLine(farbPen, x - boxHalbeBreite * 0.6f, min, x + boxHalbeBreite * 0.6f, min);
            g.DrawLine(farbPen, x - boxHalbeBreite * 0.6f, max, x + boxHalbeBreite * 0.6f, max);

            if (!infoAnzeigen)
                return;

            string info = $"n={anzahl}\nMed={statistik.Median.ToString("N1", BoxplotCulture)}";
            using Brush infoBrush = new SolidBrush(Color.Black);
            ZeichneInfoText(
                g,
                info,
                kleinFont,
                infoBrush,
                beschriftungLinks ? x - boxHalbeBreite - 18 : x + boxHalbeBreite + 18,
                Math.Max(plotArea.Top + 8, q3 - 20),
                plotArea,
                beschriftungLinks);
        }

        private static void ZeichneBoxplot(
            Graphics g,
            RectangleF plotArea,
            BoxplotStatistik statistik,
            int anzahl,
            float x,
            double minY,
            double maxY,
            Color farbe,
            Font kleinFont,
            bool beschriftungLinks)
        {
            ZeichneBoxplot(
                g,
                plotArea,
                statistik,
                anzahl,
                x,
                minY,
                maxY,
                new BoxplotSkala(false, 0.0, 0.0, 0.0),
                farbe,
                kleinFont,
                beschriftungLinks);
        }

        private static void ZeichneTitelUeberPanel(Graphics g, string titel, Font font, Brush brush, RectangleF panel)
        {
            g.DrawString(titel, font, brush, panel.Left + 8, panel.Top - 28);
        }

        private static float SkaliereY(double wert, double minY, double maxY, RectangleF plotArea)
        {
            double anteil = maxY > minY ? (wert - minY) / (maxY - minY) : 0.0;
            return (float)(plotArea.Bottom - Math.Clamp(anteil, 0.0, 1.0) * plotArea.Height);
        }

        private static float SkaliereY(double wert, double minY, double maxY, RectangleF plotArea, BoxplotSkala skala)
        {
            if (!skala.IstKomprimiert)
                return SkaliereY(wert, minY, maxY, plotArea);

            double skaliert = SkaliereBoxplotWert(wert, skala);
            double minSkaliert = SkaliereBoxplotWert(minY, skala);
            double maxSkaliert = SkaliereBoxplotWert(maxY, skala);
            double anteil = maxSkaliert > minSkaliert ? (skaliert - minSkaliert) / (maxSkaliert - minSkaliert) : 0.0;
            return (float)(plotArea.Bottom - Math.Clamp(anteil, 0.0, 1.0) * plotArea.Height);
        }

        private static BoxplotSkala ErzeugeBoxplotSkala(double maxY, double skalenbruchEnde = BoxplotSkalenbruchEnde)
        {
            return ErzeugeBoxplotSkala(
                maxY,
                BoxplotSkalenbruchStart,
                skalenbruchEnde,
                BoxplotSkalenbruchAngezeigteSpanne);
        }

        private static BoxplotSkala ErzeugeBoxplotSkala(
            double maxY,
            double skalenbruchStart,
            double skalenbruchEnde,
            double angezeigteSpanne)
        {
            return maxY > skalenbruchEnde
                ? new BoxplotSkala(true, skalenbruchStart, skalenbruchEnde, angezeigteSpanne)
                : new BoxplotSkala(false, 0.0, 0.0, 0.0);
        }

        private static double SkaliereBoxplotWert(double wert, BoxplotSkala skala)
        {
            if (!skala.IstKomprimiert || wert <= skala.Start)
                return wert;

            double echteSpanne = skala.Ende - skala.Start;
            if (wert <= skala.Ende)
                return skala.Start + (wert - skala.Start) / echteSpanne * skala.AngezeigteSpanne;

            return wert - echteSpanne + skala.AngezeigteSpanne;
        }

        private static IEnumerable<double> ErzeugeBoxplotAchsenwerte(double yMax, BoxplotSkala skala)
        {
            if (!skala.IstKomprimiert)
            {
                for (int i = 0; i <= 4; i++)
                    yield return yMax * i / 4.0;

                yield break;
            }

            double[] werte =
            {
                0.0,
                skala.Start / 2.0,
                skala.Start,
                skala.Ende,
                skala.Ende + Math.Max(0.0, yMax - skala.Ende) / 2.0,
                yMax
            };

            foreach (double wert in werte.Distinct().OrderBy(wert => wert))
                yield return wert;
        }

        private static void ZeichneBoxplotSkalenbruch(
            Graphics g,
            RectangleF plotArea,
            double maxY,
            BoxplotSkala skala,
            Pen achsenPen,
            Font font)
        {
            float yStart = SkaliereY(skala.Start, 0.0, maxY, plotArea, skala);
            float yEnde = SkaliereY(skala.Ende, 0.0, maxY, plotArea, skala);
            float yMitte = (yStart + yEnde) / 2.0f;
            float bandOben = Math.Min(yStart, yEnde);
            float bandUnten = Math.Max(yStart, yEnde);

            using Pen bruchPen = new(Color.FromArgb(45, 50, 55), 1.4f);
            ZeichneAchsenZacken(g, plotArea.Left, yMitte - 5, bruchPen);
            ZeichneAchsenZacken(g, plotArea.Left, yMitte + 5, bruchPen);

            using Pen komprimierungsPen = new(Color.FromArgb(185, 193, 201), 1)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };
            for (float y = bandOben + 6; y < bandUnten; y += 8)
                g.DrawLine(komprimierungsPen, plotArea.Left + 1, y, plotArea.Right, y);

            string label = $"{skala.Start.ToString("N0", BoxplotCulture)}-{skala.Ende.ToString("N0", BoxplotCulture)} komprimiert";
            SizeF labelSize = g.MeasureString(label, font);
            g.DrawString(label, font, Brushes.DimGray, plotArea.Left + 10, yMitte - labelSize.Height / 2);

            using Pen bandPen = new(Color.FromArgb(214, 219, 224), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.DrawLine(bandPen, plotArea.Left, yStart, plotArea.Right, yStart);
            g.DrawLine(bandPen, plotArea.Left, yEnde, plotArea.Right, yEnde);
        }

        private static void ZeichneAchsenZacken(Graphics g, float x, float y, Pen pen)
        {
            g.DrawLine(pen, x - 6, y + 4, x, y - 4);
            g.DrawLine(pen, x, y - 4, x + 6, y + 4);
        }

        private static void ZeichneZentriertenText(Graphics g, string text, Font font, Brush brush, float centerX, float y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, centerX - size.Width / 2, y);
        }

        private static void ZeichneInfoText(
            Graphics g,
            string text,
            Font font,
            Brush textBrush,
            float anchorX,
            float y,
            RectangleF plotArea,
            bool linksAusgerichtet)
        {
            SizeF size = g.MeasureString(text, font);
            float x = linksAusgerichtet ? anchorX - size.Width : anchorX;
            x = Math.Clamp(x, plotArea.Left + 6, plotArea.Right - size.Width - 6);
            y = Math.Clamp(y, plotArea.Top + 6, plotArea.Bottom - size.Height - 6);

            RectangleF hintergrund = new(x - 5, y - 3, size.Width + 10, size.Height + 6);
            using Brush hintergrundBrush = new SolidBrush(Color.FromArgb(235, Color.White));
            using Pen hintergrundPen = new(Color.FromArgb(210, 215, 220), 1);
            g.FillRectangle(hintergrundBrush, hintergrund);
            g.DrawRectangle(hintergrundPen, hintergrund.Left, hintergrund.Top, hintergrund.Width, hintergrund.Height);
            g.DrawString(text, font, textBrush, x, y);
        }

        private static string FormatiereSzenarioAchsenLabel(string label)
        {
            int trennstelle = label.IndexOf(':');
            if (trennstelle < 0 || trennstelle == label.Length - 1)
                return label;

            return label[..trennstelle].Trim() + "\n" + label[(trennstelle + 1)..].Trim();
        }

        private static BoxplotStatistik BerechneBoxplotStatistik(IReadOnlyList<double> werte)
        {
            double[] sortiert = werte
                .Where(wert => !double.IsNaN(wert) && !double.IsInfinity(wert))
                .OrderBy(wert => wert)
                .ToArray();

            if (sortiert.Length == 0)
                sortiert = new[] { 0.0 };

            return new BoxplotStatistik(
                sortiert.First(),
                Quantil(sortiert, 0.25),
                Quantil(sortiert, 0.50),
                Quantil(sortiert, 0.75),
                sortiert.Last());
        }

        private static double Quantil(IReadOnlyList<double> sortierteWerte, double p)
        {
            if (sortierteWerte.Count == 1)
                return sortierteWerte[0];

            double position = (sortierteWerte.Count - 1) * p;
            int links = (int)Math.Floor(position);
            int rechts = (int)Math.Ceiling(position);
            double anteil = position - links;
            return sortierteWerte[links] + (sortierteWerte[rechts] - sortierteWerte[links]) * anteil;
        }

    }
}
