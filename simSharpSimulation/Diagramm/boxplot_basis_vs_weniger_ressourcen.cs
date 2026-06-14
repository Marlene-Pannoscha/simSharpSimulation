using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

#pragma warning disable CA1416

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 16 - Boxplot-Vergleich Basis-Szenario gegen weniger Ressourcen.
    internal static partial class GenerateDiagramme
    {
        private const double BoxplotTagesNachlaufPufferMinuten = 180.0;
        private static readonly CultureInfo BoxplotCulture = CultureInfo.GetCultureInfo("de-DE");

        private static void ErzeugeBoxplotBasisVsWenigerRessourcen(
            IReadOnlyList<string> traceData,
            IReadOnlyList<double> rezeptionsWartezeiten,
            IReadOnlyList<double> schwesternWartezeiten,
            IReadOnlyList<double> arztWartezeiten,
            int behandeltePatientenGesamt)
        {
            RessourcenSzenario basis = new("Basis 1R/1S/2A", 1, 1, 2);
            RessourcenSzenario weniger = new("Weniger 1R/1S/1A", 1, 1, 1);

            SzenarioBoxplotDaten basisDaten = ErzeugeBoxplotDatenAusVorhandenerSimulation(
                basis,
                traceData,
                rezeptionsWartezeiten,
                schwesternWartezeiten,
                arztWartezeiten,
                behandeltePatientenGesamt);
            SzenarioBoxplotDaten wenigerDaten = SimuliereBoxplotSzenario(weniger);

            ErzeugeSzenarioBoxplotDiagramm(
                "Diagramm 16: Boxplot Basis-Szenario vs. weniger Ressourcen",
                "Normale Ressourcen: 1 Rezeption, 1 Schwester, 2 Aerzte | weniger Ressourcen: 1 Rezeption, 1 Schwester, 1 Arzt",
                basisDaten,
                wenigerDaten,
                "boxplot_basis_vs_weniger_ressourcen.png",
                16);
        }

        private static SzenarioBoxplotDaten SimuliereBoxplotSzenario(RessourcenSzenario szenario)
        {
            int alteRezeption = RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN;
            int alteSchwestern = SchwesterKonfiguration.ANZAHL_SCHWESTERN;
            int alteAerzte = ArztKonfiguration.ANZAHL_AERZTE;

            try
            {
                RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = szenario.Rezeptionisten;
                SchwesterKonfiguration.ANZAHL_SCHWESTERN = szenario.Schwestern;
                ArztKonfiguration.ANZAHL_AERZTE = szenario.Aerzte;

                SimulationsDaten daten = new();
                PatientenProzess simulation = new(SimulationKonfiguration.RANDOM_SEED, daten);
                simulation.FuehreAus();

                return ErzeugeBoxplotDatenAusSimulationsDaten(szenario, daten);
            }
            finally
            {
                RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = alteRezeption;
                SchwesterKonfiguration.ANZAHL_SCHWESTERN = alteSchwestern;
                ArztKonfiguration.ANZAHL_AERZTE = alteAerzte;
            }
        }

        private static SzenarioBoxplotDaten ErzeugeBoxplotDatenAusSimulationsDaten(
            RessourcenSzenario szenario,
            SimulationsDaten daten)
        {
            return ErzeugeBoxplotDatenAusVorhandenerSimulation(
                szenario,
                daten.TraceData,
                daten.RezeptionsWartezeiten,
                daten.SchwesternWartezeiten,
                daten.Wartezeiten,
                daten.Gesamtprozesszeiten.Count);
        }

        private static SzenarioBoxplotDaten ErzeugeBoxplotDatenAusVorhandenerSimulation(
            RessourcenSzenario szenario,
            IReadOnlyList<string> traceData,
            IReadOnlyList<double> rezeptionsWartezeiten,
            IReadOnlyList<double> schwesternWartezeiten,
            IReadOnlyList<double> arztWartezeiten,
            int behandeltePatientenGesamt)
        {
            List<TagesBoxplotStatistik> tageswerte = BerechneTagesBoxplotStatistiken(
                traceData,
                szenario,
                behandeltePatientenGesamt);

            List<double> wartezeiten = rezeptionsWartezeiten
                .Concat(schwesternWartezeiten)
                .Concat(arztWartezeiten)
                .Where(wert => wert >= 0.0 && !double.IsNaN(wert) && !double.IsInfinity(wert))
                .ToList();

            return new SzenarioBoxplotDaten(
                szenario.Name,
                wartezeiten,
                tageswerte.Select(t => t.AuslastungProzent).ToList(),
                tageswerte.Select(t => t.Gesamtkosten).ToList(),
                new KostenKomponenten(
                    tageswerte.Count > 0 ? tageswerte.Average(t => t.Personalkosten) : 0.0,
                    tageswerte.Count > 0 ? tageswerte.Average(t => t.Fixkosten) : 0.0,
                    tageswerte.Count > 0 ? tageswerte.Average(t => t.Behandlungskosten) : 0.0),
                tageswerte.Select(t => t.DurchschnittlicheWarteschlangenlaenge).ToList());
        }

        private static List<TagesBoxplotStatistik> BerechneTagesBoxplotStatistiken(
            IReadOnlyList<string> traceData,
            RessourcenSzenario szenario,
            int behandeltePatientenGesamt)
        {
            Dictionary<int, List<BoxplotTraceEvent>> eventsNachTag = traceData
                .Select(ParseBoxplotTraceEvent)
                .Where(e => e is not null)
                .Select(e => e!)
                .GroupBy(e => e.TagIndex)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(e => e.Zeit).ThenBy(e => e.Index).ToList());

            int behandelteProTag = (int)Math.Round(behandeltePatientenGesamt / (double)Math.Max(1, Program.SimulierteArbeitstage));
            List<TagesBoxplotStatistik> result = new();
            for (int tag = 0; tag < Program.SimulierteArbeitstage; tag++)
            {
                eventsNachTag.TryGetValue(tag, out List<BoxplotTraceEvent>? events);
                TagesTraceStatistik statistik = BerechneTagesTraceStatistik(events ?? new List<BoxplotTraceEvent>());
                Tagesergebnis finanzen = FinanzRechner.BerechneTagesergebnis(
                    szenario.Aerzte,
                    szenario.Schwestern,
                    szenario.Rezeptionisten,
                    behandelteProTag);

                double kapazitaetsMinuten = (szenario.Rezeptionisten + szenario.Schwestern + szenario.Aerzte)
                    * SimulationKonfiguration.SIMULATIONSDAUER;
                double auslastung = kapazitaetsMinuten > 0.0
                    ? (statistik.BelegtePersonalMinuten / kapazitaetsMinuten) * 100.0
                    : 0.0;

                result.Add(new TagesBoxplotStatistik(
                    Math.Round(auslastung, 2),
                    Math.Round(finanzen.Kosten.Gesamtkosten, 2),
                    Math.Round(finanzen.Kosten.Personalkosten, 2),
                    Math.Round(finanzen.Kosten.Fixkosten, 2),
                    Math.Round(finanzen.Kosten.Behandlungskosten, 2),
                    Math.Round(statistik.DurchschnittlicheWarteschlangenlaenge, 2)));
            }

            return result;
        }

        private static TagesTraceStatistik BerechneTagesTraceStatistik(IReadOnlyList<BoxplotTraceEvent> events)
        {
            HashSet<int> rezeptionQueue = new();
            HashSet<int> schwesterQueue = new();
            HashSet<int> arztQueue = new();
            HashSet<int> rezeptionBelegt = new();
            HashSet<int> schwesterBelegt = new();
            HashSet<int> arztBelegt = new();

            double letzteZeit = 0.0;
            double queueMinuten = 0.0;
            double belegtePersonalMinuten = 0.0;

            foreach (BoxplotTraceEvent traceEvent in events)
            {
                double zeit = Math.Clamp(traceEvent.Zeit, 0.0, SimulationKonfiguration.SIMULATIONSDAUER + BoxplotTagesNachlaufPufferMinuten);
                double dauer = Math.Max(0.0, zeit - letzteZeit);
                queueMinuten += dauer * (rezeptionQueue.Count + schwesterQueue.Count + arztQueue.Count);
                belegtePersonalMinuten += dauer * (rezeptionBelegt.Count + schwesterBelegt.Count + arztBelegt.Count);

                VerarbeiteBoxplotEvent(
                    traceEvent,
                    rezeptionQueue,
                    schwesterQueue,
                    arztQueue,
                    rezeptionBelegt,
                    schwesterBelegt,
                    arztBelegt);

                letzteZeit = zeit;
            }

            double auswertungsdauer = Math.Max(1.0, SimulationKonfiguration.SIMULATIONSDAUER);
            return new TagesTraceStatistik(
                belegtePersonalMinuten,
                queueMinuten / auswertungsdauer);
        }

        private static void VerarbeiteBoxplotEvent(
            BoxplotTraceEvent traceEvent,
            HashSet<int> rezeptionQueue,
            HashSet<int> schwesterQueue,
            HashSet<int> arztQueue,
            HashSet<int> rezeptionBelegt,
            HashSet<int> schwesterBelegt,
            HashSet<int> arztBelegt)
        {
            int patientId = traceEvent.PatientId;
            switch (traceEvent.EventTyp)
            {
                case "betritt_rezeption_warteschlange":
                    rezeptionQueue.Add(patientId);
                    break;
                case "betritt_rezeption":
                    rezeptionQueue.Remove(patientId);
                    rezeptionBelegt.Add(patientId);
                    break;
                case "beendet_rezeption":
                case "bricht_ab_wegen_feierabend_rezeption":
                    rezeptionQueue.Remove(patientId);
                    rezeptionBelegt.Remove(patientId);
                    break;
                case "betritt_wartezimmer":
                case "betritt_schwester_warteschlange":
                    schwesterQueue.Add(patientId);
                    break;
                case "verlaesst_wartezimmer":
                case "verlaesst_wartezimmer_schwester":
                case "betritt_schwesterzimmer":
                    schwesterQueue.Remove(patientId);
                    break;
                case "geht_zur_schwester":
                    schwesterBelegt.Add(patientId);
                    break;
                case "beendet_schwester_prozess":
                case "bricht_ab_wegen_feierabend_schwester":
                    schwesterQueue.Remove(patientId);
                    schwesterBelegt.Remove(patientId);
                    break;
                case "betritt_wartezimmer_fuer_arzt":
                    arztQueue.Add(patientId);
                    break;
                case "verlaesst_wartezimmer_fuer_arzt":
                case "betritt_arztzimmer":
                    arztQueue.Remove(patientId);
                    break;
                case "geht_zum_arzt":
                    arztBelegt.Add(patientId);
                    break;
                case "beendet_arzt_behandlung":
                case "bricht_ab_wegen_feierabend_arzt":
                    arztQueue.Remove(patientId);
                    arztBelegt.Remove(patientId);
                    break;
                case "geht_zum_ausgang":
                case "verlaesst_klinik":
                    rezeptionQueue.Remove(patientId);
                    schwesterQueue.Remove(patientId);
                    arztQueue.Remove(patientId);
                    rezeptionBelegt.Remove(patientId);
                    schwesterBelegt.Remove(patientId);
                    arztBelegt.Remove(patientId);
                    break;
            }
        }

        private static BoxplotTraceEvent? ParseBoxplotTraceEvent(string zeile, int index)
        {
            string[] teile = zeile.Split(';');
            if (teile.Length < 5)
                return null;

            if (!double.TryParse(teile[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double zeit))
                return null;

            if (!int.TryParse(teile[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int patientId))
                return null;

            int tagIndex = Math.Max(0, (patientId - 1) / 10_000);
            return new BoxplotTraceEvent(index, tagIndex, zeit, teile[1], patientId);
        }

        private static void ErzeugeSzenarioBoxplotDiagramm(
            string titel,
            string untertitel,
            SzenarioBoxplotDaten erstesSzenario,
            SzenarioBoxplotDaten zweitesSzenario,
            string dateiname,
            int diagrammNummer)
        {
            const int breite = 1600;
            const int hoehe = 900;
            BoxplotGruppe[] gruppen =
            {
                new("Wartezeit (min)", erstesSzenario.Wartezeiten, zweitesSzenario.Wartezeiten),
                new("Auslastung (%)", erstesSzenario.AuslastungenProzent, zweitesSzenario.AuslastungenProzent),
                new("Warteschlangenlaenge (Patienten)", erstesSzenario.Warteschlangenlaengen, zweitesSzenario.Warteschlangenlaengen)
            };

            Color basisFarbe = Color.FromArgb(55, 116, 181);
            Color vergleichFarbe = Color.FromArgb(41, 150, 93);
            string outputPath = ErzeugeOutputPfad(dateiname);

            using Bitmap bitmap = new(breite, hoehe);
            using Graphics g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            using Font panelTitelFont = new("Arial", 12, FontStyle.Bold);
            using Font achsenFont = new("Arial", 12, FontStyle.Regular);
            using Font kleinFont = new("Arial", 11, FontStyle.Regular);
            using Brush textBrush = new SolidBrush(Color.FromArgb(28, 32, 36));
            using Pen achsenPen = new(Color.FromArgb(45, 50, 55), 1.2f);

            ZeichneKostenLegendeHorizontal(g, new PointF(55, 24), kleinFont);
            ZeichneLegende(g, erstesSzenario.Name, zweitesSzenario.Name, basisFarbe, vergleichFarbe, new PointF(breite - 420, 24));

            RectangleF[] panels =
            {
                new(55, 70, 705, 365),
                new(840, 70, 705, 365),
                new(55, 490, 705, 365),
                new(840, 490, 705, 365)
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
                achsenPen);
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
                achsenPen);
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
                achsenPen);

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
            using Font font = new("Arial", 13, FontStyle.Regular);
            using Brush erstesBrush = new SolidBrush(erstesFarbe);
            using Brush zweitesBrush = new SolidBrush(zweitesFarbe);
            using Pen rahmenPen = new(Color.FromArgb(80, 80, 80), 1);
            g.FillRectangle(erstesBrush, start.X, start.Y, 22, 14);
            g.DrawRectangle(rahmenPen, start.X, start.Y, 22, 14);
            g.DrawString(erstesLabel, font, Brushes.DimGray, start.X + 32, start.Y - 4);
            g.FillRectangle(zweitesBrush, start.X, start.Y + 28, 22, 14);
            g.DrawRectangle(rahmenPen, start.X, start.Y + 28, 22, 14);
            g.DrawString(zweitesLabel, font, Brushes.DimGray, start.X + 32, start.Y + 24);
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
            Pen achsenPen)
        {
            RectangleF plotArea = new(panel.Left + 92, panel.Top + 46, panel.Width - 128, panel.Height - 96);
            BoxplotStatistik erstes = BerechneBoxplotStatistik(gruppe.ErstesSzenario);
            BoxplotStatistik zweites = BerechneBoxplotStatistik(gruppe.ZweitesSzenario);
            double maxY = Math.Max(1.0, Math.Max(erstes.Max, zweites.Max));
            double yMax = maxY * 1.12;

            using Pen gridPen = new(Color.FromArgb(224, 229, 234), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            using Pen panelPen = new(Color.FromArgb(224, 228, 232), 1);
            g.DrawString(gruppe.Name, titelFont, textBrush, panel.Left + 8, panel.Top + 8);
            g.DrawRectangle(panelPen, panel.Left, panel.Top, panel.Width, panel.Height);

            for (int i = 0; i <= 4; i++)
            {
                double wert = yMax * i / 4.0;
                float y = SkaliereY(wert, 0.0, yMax, plotArea);
                g.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
                string label = wert.ToString("N1", BoxplotCulture);
                SizeF labelSize = g.MeasureString(label, achsenFont);
                g.DrawString(label, achsenFont, Brushes.DimGray, plotArea.Left - labelSize.Width - 8, y - labelSize.Height / 2);
            }

            g.DrawLine(achsenPen, plotArea.Left, plotArea.Bottom, plotArea.Right, plotArea.Bottom);
            g.DrawLine(achsenPen, plotArea.Left, plotArea.Top, plotArea.Left, plotArea.Bottom);

            float erstesX = plotArea.Left + plotArea.Width * 0.33f;
            float zweitesX = plotArea.Left + plotArea.Width * 0.67f;
            ZeichneBoxplot(g, plotArea, erstes, gruppe.ErstesSzenario.Count, erstesX, 0.0, yMax, erstesFarbe, kleinFont);
            ZeichneBoxplot(g, plotArea, zweites, gruppe.ZweitesSzenario.Count, zweitesX, 0.0, yMax, zweitesFarbe, kleinFont);

            ZeichneZentriertenText(g, erstesLabel, achsenFont, Brushes.DimGray, erstesX, plotArea.Bottom + 18);
            ZeichneZentriertenText(g, zweitesLabel, achsenFont, Brushes.DimGray, zweitesX, plotArea.Bottom + 18);
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
            RectangleF plotArea = new(panel.Left + 92, panel.Top + 46, panel.Width - 128, panel.Height - 96);
            double maxY = Math.Max(erstesSzenario.Kosten.Gesamt, zweitesSzenario.Kosten.Gesamt) * 1.12;
            maxY = Math.Max(1.0, maxY);

            using Pen gridPen = new(Color.FromArgb(224, 229, 234), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            using Pen panelPen = new(Color.FromArgb(224, 228, 232), 1);
            g.DrawString("Kostenstruktur (EUR/Tag)", titelFont, textBrush, panel.Left + 8, panel.Top + 8);
            g.DrawRectangle(panelPen, panel.Left, panel.Top, panel.Width, panel.Height);

            for (int i = 0; i <= 4; i++)
            {
                double wert = maxY * i / 4.0;
                float y = SkaliereY(wert, 0.0, maxY, plotArea);
                g.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
                string label = wert.ToString("N0", BoxplotCulture);
                SizeF labelSize = g.MeasureString(label, achsenFont);
                g.DrawString(label, achsenFont, Brushes.DimGray, plotArea.Left - labelSize.Width - 8, y - labelSize.Height / 2);
            }

            g.DrawLine(achsenPen, plotArea.Left, plotArea.Bottom, plotArea.Right, plotArea.Bottom);
            g.DrawLine(achsenPen, plotArea.Left, plotArea.Top, plotArea.Left, plotArea.Bottom);

            float erstesX = plotArea.Left + plotArea.Width * 0.33f;
            float zweitesX = plotArea.Left + plotArea.Width * 0.67f;
            ZeichneKostenBalken(g, plotArea, erstesSzenario.Kosten, erstesX, maxY, kleinFont);
            ZeichneKostenBalken(g, plotArea, zweitesSzenario.Kosten, zweitesX, maxY, kleinFont);

            ZeichneZentriertenText(g, erstesSzenario.Name, achsenFont, Brushes.DimGray, erstesX, plotArea.Bottom + 18);
            ZeichneZentriertenText(g, zweitesSzenario.Name, achsenFont, Brushes.DimGray, zweitesX, plotArea.Bottom + 18);
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
                (kosten.Behandlung, Color.FromArgb(231, 120, 70))
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
            ZeichneZentriertenText(g, summe, kleinFont, Brushes.DimGray, centerX, SkaliereY(kosten.Gesamt, 0.0, maxY, plotArea) - 22);
        }

        private static void ZeichneKostenLegendeHorizontal(Graphics g, PointF start, Font font)
        {
            (string Label, Color Farbe)[] items =
            {
                ("Personal", Color.FromArgb(70, 130, 180)),
                ("Fixkosten", Color.FromArgb(241, 196, 15)),
                ("Behandlung", Color.FromArgb(231, 120, 70))
            };

            using Brush labelBrush = new SolidBrush(Color.FromArgb(90, 94, 98));
            float x = start.X;
            for (int i = 0; i < items.Length; i++)
            {
                using Brush brush = new SolidBrush(Color.FromArgb(210, items[i].Farbe));
                g.FillRectangle(brush, x, start.Y + 3, 18, 12);
                g.DrawRectangle(Pens.Gray, x, start.Y + 3, 18, 12);
                g.DrawString(items[i].Label, font, labelBrush, x + 25, start.Y - 1);
                x += 138;
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
            Color farbe,
            Font kleinFont)
        {
            float boxHalbeBreite = 48;
            float min = SkaliereY(statistik.Min, minY, maxY, plotArea);
            float q1 = SkaliereY(statistik.Q1, minY, maxY, plotArea);
            float median = SkaliereY(statistik.Median, minY, maxY, plotArea);
            float q3 = SkaliereY(statistik.Q3, minY, maxY, plotArea);
            float max = SkaliereY(statistik.Max, minY, maxY, plotArea);

            using Brush boxBrush = new SolidBrush(Color.FromArgb(82, farbe));
            using Pen farbPen = new(farbe, 2.4f);
            using Pen medianPen = new(Color.Black, 2);

            float boxTop = Math.Min(q1, q3);
            float boxHeight = Math.Max(2, Math.Abs(q3 - q1));
            g.FillRectangle(boxBrush, x - boxHalbeBreite, boxTop, boxHalbeBreite * 2, boxHeight);
            g.DrawRectangle(farbPen, x - boxHalbeBreite, boxTop, boxHalbeBreite * 2, boxHeight);
            g.DrawLine(medianPen, x - boxHalbeBreite, median, x + boxHalbeBreite, median);
            g.DrawLine(farbPen, x, min, x, q1);
            g.DrawLine(farbPen, x, q3, x, max);
            g.DrawLine(farbPen, x - boxHalbeBreite * 0.6f, min, x + boxHalbeBreite * 0.6f, min);
            g.DrawLine(farbPen, x - boxHalbeBreite * 0.6f, max, x + boxHalbeBreite * 0.6f, max);

            string info = $"n={anzahl}\nMed={statistik.Median.ToString("N1", BoxplotCulture)}";
            using Brush infoBrush = new SolidBrush(farbe);
            g.DrawString(info, kleinFont, infoBrush, x + boxHalbeBreite + 12, Math.Max(plotArea.Top + 4, q3 - 18));
        }

        private static float SkaliereY(double wert, double minY, double maxY, RectangleF plotArea)
        {
            double anteil = maxY > minY ? (wert - minY) / (maxY - minY) : 0.0;
            return (float)(plotArea.Bottom - Math.Clamp(anteil, 0.0, 1.0) * plotArea.Height);
        }

        private static void ZeichneZentriertenText(Graphics g, string text, Font font, Brush brush, float centerX, float y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, centerX - size.Width / 2, y);
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

        private sealed record RessourcenSzenario(string Name, int Rezeptionisten, int Schwestern, int Aerzte);

        private sealed record SzenarioBoxplotDaten(
            string Name,
            IReadOnlyList<double> Wartezeiten,
            IReadOnlyList<double> AuslastungenProzent,
            IReadOnlyList<double> KostenProTag,
            KostenKomponenten Kosten,
            IReadOnlyList<double> Warteschlangenlaengen);

        private sealed record KostenKomponenten(double Personal, double Fix, double Behandlung)
        {
            public double Gesamt => Personal + Fix + Behandlung;
        }

        private sealed record BoxplotGruppe(
            string Name,
            IReadOnlyList<double> ErstesSzenario,
            IReadOnlyList<double> ZweitesSzenario);

        private sealed record BoxplotStatistik(double Min, double Q1, double Median, double Q3, double Max);

        private sealed record BoxplotTraceEvent(int Index, int TagIndex, double Zeit, string EventTyp, int PatientId);

        private sealed record TagesTraceStatistik(double BelegtePersonalMinuten, double DurchschnittlicheWarteschlangenlaenge);

        private sealed record TagesBoxplotStatistik(
            double AuslastungProzent,
            double Gesamtkosten,
            double Personalkosten,
            double Fixkosten,
            double Behandlungskosten,
            double DurchschnittlicheWarteschlangenlaenge);
    }
}
