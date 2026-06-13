using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace simSharpSimulation
{
    /// <summary>
    /// HÃ¤lt alle wÃ¤hrend der Simulation gesammelten Daten (Trace, Wartezeiten, AnkÃ¼nfte).
    /// Diese Klasse entkoppelt Datenspeicherung von der eigentlichen Simulationslogik.
    /// </summary>
    public sealed class SimulationsDaten
    {
        private const string ZustandUnveraendert = "UNVERAENDERT";
        private const double PrognoseTrefferToleranzAnteil = 0.30;
        private const double PrognoseTrefferMindestToleranzMinuten = 5.0;

        private static readonly Dictionary<string, (string Von, string Zu)> EventZustandsMapping =
            ErstelleEventZustandsMapping();

        // Rohdaten der Simulation
        public List<string> TraceData { get; } = new();
        public List<double> Wartezeiten { get; } = new();
        public List<double> WartezeitenMitTermin { get; } = new();
        public List<double> WartezeitenOhneTermin { get; } = new();
        public Dictionary<PatientenTyp, List<double>> WartezeitenArztNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());

        public List<double> SchwesternWartezeiten { get; } = new();
        public Dictionary<PatientenTyp, List<double>> WartezeitenSchwesterNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());
        public List<double> SchwesternWartezeitenMitTermin { get; } = new();
        public List<double> SchwesternWartezeitenOhneTermin { get; } = new();
        public List<double> SchwesternBehandlungszeitenMitTermin { get; } = new();
        public List<double> SchwesternBehandlungszeitenOhneTermin { get; } = new();

        public List<double> RezeptionsWartezeiten { get; } = new();
        public List<double> RezeptionsWartezeitenMitTermin { get; } = new();
        public List<double> RezeptionsWartezeitenOhneTermin { get; } = new();
        public List<double> RezeptionsBehandlungszeiten { get; } = new();
        public List<double> RezeptionsBehandlungszeitenMitTermin { get; } = new();
        public List<double> RezeptionsBehandlungszeitenOhneTermin { get; } = new();

        public List<double> Gesamtprozesszeiten { get; } = new();
        public List<double> GesamtprozesszeitenMitTermin { get; } = new();
        public List<double> GesamtprozesszeitenOhneTermin { get; } = new();
        public List<double> EchteAnkunftszeiten { get; } = new();
        public List<double> EchteAnkunftszeitenMitTermin { get; } = new();
        public List<double> EchteAnkunftszeitenOhneTermin { get; } = new();

        public Dictionary<PatientenTyp, int> PatientenTypZaehler { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => 0);

        public List<double> ArztBehandlungszeitenMitTermin { get; } = new();
        public List<double> ArztBehandlungszeitenOhneTermin { get; } = new();
        public Dictionary<PatientenTyp, List<double>> ArztBehandlungszeitenNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());
        public Dictionary<PatientenTyp, List<double>> SchwesternBehandlungszeitenNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());
        private readonly SortedDictionary<DateTime, TagesHitMissZaehler> hitMissProTag = new();

        private readonly Dictionary<int, List<PrognosePruefung>> prognoseOffen = new();
        private readonly List<PrognoseErgebnis> prognoseErgebnisse = new();
        private readonly List<PrognoseAbbruchPunkt> prognoseAbbrueche = new();
        private readonly List<PrognoseAufnahmePruefung> prognoseAufnahmePruefungen = new();
        private readonly List<PrognoseAufnahmeEntscheidung> prognoseAufnahmeEntscheidungen = new();

        public int AnzahlBehandeltHit { get; private set; }
        public int AnzahlAbgebrochenMiss { get; private set; }

        public int AnzahlNichtBehandeltArztFeierabend { get; private set; }
        public int AnzahlNichtBehandeltArztGesamt => AnzahlNichtBehandeltArztFeierabend;
        public int AnzahlNichtBehandeltSchwesterFeierabend { get; private set; }
        public int AnzahlNichtBehandeltSchwesterGesamt => AnzahlNichtBehandeltSchwesterFeierabend;
        public int AnzahlNichtBehandeltRezeptionFeierabend { get; private set; }
        public int AnzahlNichtBehandeltRezeptionGesamt => AnzahlNichtBehandeltRezeptionFeierabend;

        public int AnzahlPrognosePruefungen { get; private set; }
        public int AnzahlPrognoseRichtig { get; private set; }
        public int AnzahlPrognoseAbbruch { get; private set; }
        public int AnzahlPrognoseAufnahmeAbgewiesen { get; private set; }

        // Abgeleitete Kennzahlen
        public double DurchschnittlicheWartezeitArzt => MittelwertOder0(Wartezeiten);
        public double DurchschnittlicheWartezeitArztMitTermin => MittelwertOder0(WartezeitenMitTermin);
        public double DurchschnittlicheWartezeitArztOhneTermin => MittelwertOder0(WartezeitenOhneTermin);
        public double DurchschnittlicheWartezeitSchwester => MittelwertOder0(SchwesternWartezeiten);
        public double DurchschnittlicheWartezeitSchwesterMitTermin => MittelwertOder0(SchwesternWartezeitenMitTermin);
        public double DurchschnittlicheWartezeitSchwesterOhneTermin => MittelwertOder0(SchwesternWartezeitenOhneTermin);
        public double DurchschnittlicheBehandlungszeitSchwesterMitTermin => MittelwertOder0(SchwesternBehandlungszeitenMitTermin);
        public double DurchschnittlicheBehandlungszeitSchwesterOhneTermin => MittelwertOder0(SchwesternBehandlungszeitenOhneTermin);
        public double DurchschnittlicheWartezeitRezeption => MittelwertOder0(RezeptionsWartezeiten);
        public double DurchschnittlicheWartezeitRezeptionMitTermin => MittelwertOder0(RezeptionsWartezeitenMitTermin);
        public double DurchschnittlicheWartezeitRezeptionOhneTermin => MittelwertOder0(RezeptionsWartezeitenOhneTermin);
        public double DurchschnittlicheBehandlungszeitRezeptionMitTermin => MittelwertOder0(RezeptionsBehandlungszeitenMitTermin);
        public double DurchschnittlicheBehandlungszeitRezeptionOhneTermin => MittelwertOder0(RezeptionsBehandlungszeitenOhneTermin);
        public double DurchschnittlicheBehandlungszeitArztMitTermin => MittelwertOder0(ArztBehandlungszeitenMitTermin);
        public double DurchschnittlicheBehandlungszeitArztOhneTermin => MittelwertOder0(ArztBehandlungszeitenOhneTermin);
        public double DurchschnittlicheGesamtprozesszeit => MittelwertOder0(Gesamtprozesszeiten);
        public double DurchschnittlicheGesamtprozesszeitMitTermin => MittelwertOder0(GesamtprozesszeitenMitTermin);
        public double DurchschnittlicheGesamtprozesszeitOhneTermin => MittelwertOder0(GesamtprozesszeitenOhneTermin);
        public double PrognoseTrefferquote => AnzahlPrognosePruefungen > 0
            ? (AnzahlPrognoseRichtig / (double)AnzahlPrognosePruefungen) * 100.0
            : 0.0;
        internal IReadOnlyList<TagesHitMissPunkt> HitMissProTag => hitMissProTag
            .Select(eintrag => new TagesHitMissPunkt(
                eintrag.Key.ToString("ddd dd.MM", CultureInfo.GetCultureInfo("de-DE")),
                eintrag.Value.Hit,
                eintrag.Value.Miss))
            .ToList();

        /// <summary>
        /// Speichert ein Ereignis im Trace-Format:
        /// "Zeit;EventTyp;VonZustand;ZuZustand;PatientId;ArztId;SchwesterId".
        /// </summary>
        public void LogEvent(double zeit, string eventTyp, int patientId, int? arztId = null, int? schwesterId = null)
        {
            var (vonZustand, zuZustand) = ErmittleZustandswechsel(eventTyp);
            string timeStr = zeit.ToString("000.00", CultureInfo.InvariantCulture);
            string arztStr = arztId.HasValue ? arztId.Value.ToString() : "";
            string schwesterStr = schwesterId.HasValue ? schwesterId.Value.ToString() : "";
            string logEntry = $"{timeStr};{eventTyp};{vonZustand};{zuZustand};{patientId};{arztStr};{schwesterStr}";
            TraceData.Add(logEntry);
        }

        public void ErfasseArztWartezeit(double wartezeitArzt, bool hatTermin, PatientenTyp patientenTyp)
        {
            Wartezeiten.Add(wartezeitArzt);
            WartezeitenArztNachTyp[patientenTyp].Add(wartezeitArzt);
            FuegeNachTerminHinzu(wartezeitArzt, hatTermin, WartezeitenMitTermin, WartezeitenOhneTermin);
        }

        public void ErfasseArztAbbruchWartezeit(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseArztAbbruchFeierabend(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            AnzahlNichtBehandeltArztFeierabend++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseArztBehandlungBegonnen(DateTime tag)
        {
            AnzahlBehandeltHit++;
            ErmittleOderErzeugeTagesHitMiss(tag).Hit++;
        }

        public void ErfasseSchwesterAbbruchWartezeit(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseSchwesterAbbruchFeierabend(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            AnzahlNichtBehandeltSchwesterFeierabend++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseRezeptionAbbruchWartezeit(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseRezeptionAbbruchFeierabend(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            AnzahlNichtBehandeltRezeptionFeierabend++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfassePrognoseAbbruch(DateTime tag, double zeitpunktMinuten, string phase)
        {
            AnzahlAbgebrochenMiss++;
            AnzahlPrognoseAbbruch++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
            prognoseAbbrueche.Add(new PrognoseAbbruchPunkt(zeitpunktMinuten, phase));
        }

        public void ErfassePrognoseAufnahmepruefung(DateTime tag, double zeitpunktMinuten, int aufnahmeKapazitaet)
        {
            prognoseAufnahmePruefungen.Add(new PrognoseAufnahmePruefung(tag.Date, zeitpunktMinuten, aufnahmeKapazitaet));
        }

        public void ErfassePrognoseAufnahmeZugelassen(DateTime tag, double zeitpunktMinuten, int patientId, int restKapazitaet)
        {
            prognoseAufnahmeEntscheidungen.Add(new PrognoseAufnahmeEntscheidung(
                tag.Date,
                zeitpunktMinuten,
                patientId,
                true,
                restKapazitaet));
        }

        public void ErfassePrognoseAufnahmeAbgewiesen(DateTime tag, double zeitpunktMinuten, int patientId)
        {
            AnzahlAbgebrochenMiss++;
            AnzahlPrognoseAufnahmeAbgewiesen++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
            prognoseAufnahmeEntscheidungen.Add(new PrognoseAufnahmeEntscheidung(
                tag.Date,
                zeitpunktMinuten,
                patientId,
                false,
                0));
        }

        public void ErfasseSchwesterWartezeit(double wartezeitSchwester, PatientenTyp patientenTyp, bool hatTermin)
        {
            SchwesternWartezeiten.Add(wartezeitSchwester);
            WartezeitenSchwesterNachTyp[patientenTyp].Add(wartezeitSchwester);
            FuegeNachTerminHinzu(
                wartezeitSchwester,
                hatTermin,
                SchwesternWartezeitenMitTermin,
                SchwesternWartezeitenOhneTermin);
        }

        public void ErfasseSchwesterBehandlungszeit(double dauerSchwester, bool hatTermin, PatientenTyp patientenTyp)
        {
            SchwesternBehandlungszeitenNachTyp[patientenTyp].Add(dauerSchwester);
            FuegeNachTerminHinzu(
                dauerSchwester,
                hatTermin,
                SchwesternBehandlungszeitenMitTermin,
                SchwesternBehandlungszeitenOhneTermin);
        }

        public void ErfasseRezeptionWartezeit(double wartezeitRezeption, bool hatTermin)
        {
            RezeptionsWartezeiten.Add(wartezeitRezeption);
            FuegeNachTerminHinzu(
                wartezeitRezeption,
                hatTermin,
                RezeptionsWartezeitenMitTermin,
                RezeptionsWartezeitenOhneTermin);
        }

        public void ErfasseRezeptionBehandlungszeit(double dauerRezeption, bool hatTermin)
        {
            RezeptionsBehandlungszeiten.Add(dauerRezeption);
            FuegeNachTerminHinzu(
                dauerRezeption,
                hatTermin,
                RezeptionsBehandlungszeitenMitTermin,
                RezeptionsBehandlungszeitenOhneTermin);
        }

        public void ErfasseArztBehandlungszeit(double dauerArzt, bool hatTermin, PatientenTyp patientenTyp)
        {
            ArztBehandlungszeitenNachTyp[patientenTyp].Add(dauerArzt);
            FuegeNachTerminHinzu(
                dauerArzt,
                hatTermin,
                ArztBehandlungszeitenMitTermin,
                ArztBehandlungszeitenOhneTermin);
        }

        public void ErfasseGesamtprozesszeit(double gesamtprozesszeit, bool hatTermin)
        {
            Gesamtprozesszeiten.Add(gesamtprozesszeit);
            FuegeNachTerminHinzu(
                gesamtprozesszeit,
                hatTermin,
                GesamtprozesszeitenMitTermin,
                GesamtprozesszeitenOhneTermin);
        }

        public void ErfasseAnkunftszeit(double ankunftszeit, bool hatTermin)
        {
            EchteAnkunftszeiten.Add(ankunftszeit);
            FuegeNachTerminHinzu(
                ankunftszeit,
                hatTermin,
                EchteAnkunftszeitenMitTermin,
                EchteAnkunftszeitenOhneTermin);
        }

        public void ErfassePrognosePruefung(
            int patientId,
            string phase,
            double zeitpunktMinuten,
            double prognoseRestMinuten,
            double prognoseBearbeitungsRestMinuten,
            double verbrauchteBearbeitungsMinuten,
            bool prognoseFertigBisSchichtende)
        {
            if (!prognoseOffen.TryGetValue(patientId, out List<PrognosePruefung>? liste))
            {
                liste = new List<PrognosePruefung>();
                prognoseOffen[patientId] = liste;
            }

            liste.Add(new PrognosePruefung(
                patientId,
                phase,
                zeitpunktMinuten,
                prognoseRestMinuten,
                prognoseBearbeitungsRestMinuten,
                verbrauchteBearbeitungsMinuten,
                prognoseFertigBisSchichtende));
            AnzahlPrognosePruefungen++;

            if (AnzahlPrognosePruefungen == 1)
            {
                Console.WriteLine($"[Prognose] Erste Prüfung: Patient {patientId}, Phase {phase}, t={zeitpunktMinuten:F2}, Rest={prognoseRestMinuten:F2}");
            }
        }

        public void SchliessePrognosen(
            int patientId,
            double endZeitpunktMinuten,
            double? gesampelteBearbeitungszeitGesamt = null)
        {
            if (!prognoseOffen.TryGetValue(patientId, out List<PrognosePruefung>? liste) || liste.Count == 0)
                return;

            foreach (var pruefung in liste)
            {
                double actualRest = Math.Max(0.0, endZeitpunktMinuten - pruefung.ZeitpunktMinuten);
                double abweichungMinuten = pruefung.PrognoseRestMinuten - actualRest;
                double? abweichungProzent = actualRest > 0.0001
                    ? (abweichungMinuten / actualRest) * 100.0
                    : null;
                double? gesampelteBearbeitungsRestMinuten = gesampelteBearbeitungszeitGesamt.HasValue
                    ? Math.Max(0.0, gesampelteBearbeitungszeitGesamt.Value - pruefung.VerbrauchteBearbeitungsMinuten)
                    : null;
                double? abweichungBearbeitungMinuten = gesampelteBearbeitungsRestMinuten.HasValue
                    ? pruefung.PrognoseBearbeitungsRestMinuten - gesampelteBearbeitungsRestMinuten.Value
                    : null;
                double? abweichungBearbeitungProzent =
                    gesampelteBearbeitungsRestMinuten.HasValue && gesampelteBearbeitungsRestMinuten.Value > 0.0001
                        ? (abweichungBearbeitungMinuten!.Value / gesampelteBearbeitungsRestMinuten.Value) * 100.0
                        : null;
                bool korrekt;

                if (actualRest <= 0.0001)
                {
                    korrekt = pruefung.PrognoseRestMinuten <= 0.0001;
                }
                else
                {
                    double toleranz = BerechnePrognoseTrefferToleranz(actualRest);
                    korrekt = Math.Abs(pruefung.PrognoseRestMinuten - actualRest) <= toleranz;
                }

                if (korrekt)
                    AnzahlPrognoseRichtig++;

                prognoseErgebnisse.Add(new PrognoseErgebnis(
                    pruefung.PatientId,
                    pruefung.Phase,
                    pruefung.ZeitpunktMinuten,
                    pruefung.PrognoseRestMinuten,
                    pruefung.PrognoseBearbeitungsRestMinuten,
                    gesampelteBearbeitungsRestMinuten,
                    abweichungBearbeitungMinuten,
                    abweichungBearbeitungProzent,
                    actualRest,
                    abweichungMinuten,
                    abweichungProzent,
                    korrekt,
                    pruefung.PrognoseFertigBisSchichtende));
            }

            prognoseOffen.Remove(patientId);
        }

        private static double BerechnePrognoseTrefferToleranz(double istRestMinuten)
        {
            return Math.Max(
                PrognoseTrefferMindestToleranzMinuten,
                istRestMinuten * PrognoseTrefferToleranzAnteil);
        }

        public string ErzeugePrognoseReportText()
        {
            var culture = CultureInfo.GetCultureInfo("de-DE");
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("Prognosemodell-Auswertung");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine($"Prognoseprüfungen gesamt: {AnzahlPrognosePruefungen.ToString("N0", culture)}");
            sb.AppendLine(
                $"Richtig (+/-{(PrognoseTrefferToleranzAnteil * 100.0).ToString("N0", culture)} % " +
                $"oder +/-{PrognoseTrefferMindestToleranzMinuten.ToString("N0", culture)} min): {AnzahlPrognoseRichtig.ToString("N0", culture)}");
            sb.AppendLine($"Trefferquote: {PrognoseTrefferquote.ToString("N2", culture)} %");
            sb.AppendLine($"Prognose-Abbrüche: {AnzahlPrognoseAbbruch.ToString("N0", culture)}");
            sb.AppendLine($"Aufnahmeprognose-Abweisungen: {AnzahlPrognoseAufnahmeAbgewiesen.ToString("N0", culture)}");
            if (prognoseErgebnisse.Count > 0)
            {
                var ergebnisseMitBearbeitung = prognoseErgebnisse
                    .Where(e => e.GesampelteBearbeitungsRestMinuten.HasValue)
                    .ToList();
                sb.AppendLine($"Mittlere geschaetzte Restzeit: {prognoseErgebnisse.Average(e => e.PrognoseRestMinuten).ToString("N2", culture)} min");
                sb.AppendLine($"Mittlere tatsaechliche Restzeit: {prognoseErgebnisse.Average(e => e.IstRestMinuten).ToString("N2", culture)} min");
                sb.AppendLine($"Mittlere absolute Abweichung: {prognoseErgebnisse.Average(e => Math.Abs(e.AbweichungMinuten)).ToString("N2", culture)} min");
                if (ergebnisseMitBearbeitung.Count > 0)
                {
                    sb.AppendLine($"Mittlere prognostizierte Bearbeitungs-Restzeit: {ergebnisseMitBearbeitung.Average(e => e.PrognoseBearbeitungsRestMinuten).ToString("N2", culture)} min");
                    sb.AppendLine($"Mittlere gesampelte Bearbeitungs-Restzeit: {ergebnisseMitBearbeitung.Average(e => e.GesampelteBearbeitungsRestMinuten!.Value).ToString("N2", culture)} min");
                    sb.AppendLine($"Mittlere absolute Bearbeitungs-Abweichung: {ergebnisseMitBearbeitung.Average(e => Math.Abs(e.AbweichungBearbeitungMinuten!.Value)).ToString("N2", culture)} min");
                }
            }
            sb.AppendLine();

            var nachPhase = prognoseErgebnisse
                .GroupBy(e => e.Phase)
                .OrderBy(g => g.Key)
                .ToList();

            if (nachPhase.Count > 0)
            {
                sb.AppendLine("Trefferquote je Phase");
                sb.AppendLine(new string('-', 50));
                foreach (var gruppe in nachPhase)
                {
                    int total = gruppe.Count();
                    int korrekt = gruppe.Count(e => e.Korrekt);
                    double quote = total > 0 ? (korrekt / (double)total) * 100.0 : 0.0;
                    double mittlerePrognose = gruppe.Average(e => e.PrognoseRestMinuten);
                    double mittlereIstRestzeit = gruppe.Average(e => e.IstRestMinuten);
                    double mittlereAbsoluteAbweichung = gruppe.Average(e => Math.Abs(e.AbweichungMinuten));
                    var gruppeMitBearbeitung = gruppe
                        .Where(e => e.GesampelteBearbeitungsRestMinuten.HasValue)
                        .ToList();
                    sb.AppendLine(
                        $"{gruppe.Key}: {korrekt}/{total} ({quote.ToString("N2", culture)} %) " +
                        $"| Prognose {mittlerePrognose.ToString("N2", culture)} min " +
                        $"| Ist {mittlereIstRestzeit.ToString("N2", culture)} min " +
                        $"| abs. Abw. {mittlereAbsoluteAbweichung.ToString("N2", culture)} min");
                    if (gruppeMitBearbeitung.Count > 0)
                    {
                        double mittlereBearbeitungsPrognose =
                            gruppeMitBearbeitung.Average(e => e.PrognoseBearbeitungsRestMinuten);
                        double mittlereBearbeitung =
                            gruppeMitBearbeitung.Average(e => e.GesampelteBearbeitungsRestMinuten!.Value);
                        double mittlereAbsoluteBearbeitungsAbweichung =
                            gruppeMitBearbeitung.Average(e => Math.Abs(e.AbweichungBearbeitungMinuten!.Value));
                        sb.AppendLine(
                            $"  Bearbeitung: Prognose {mittlereBearbeitungsPrognose.ToString("N2", culture)} min " +
                            $"| Sample {mittlereBearbeitung.ToString("N2", culture)} min " +
                            $"| abs. Abw. {mittlereAbsoluteBearbeitungsAbweichung.ToString("N2", culture)} min");
                    }
                }
            }

            return sb.ToString();
        }

        public void SchreibePrognoseReport(string dateiPfad)
        {
            string report = ErzeugePrognoseReportText();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(report);
            sb.AppendLine();
            sb.AppendLine("Details");
            sb.AppendLine("PatientId;Phase;ZeitpunktMin;PrognoseRestMin;IstRestMin;AbweichungMin;AbweichungProzent;PrognoseBearbeitungsRestMin;GesampelteBearbeitungsRestMin;AbweichungBearbeitungMin;AbweichungBearbeitungProzent;Korrekt;PrognoseFertigBisSchichtende");
            foreach (var eintrag in prognoseErgebnisse)
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    eintrag.PatientId.ToString(CultureInfo.InvariantCulture),
                    eintrag.Phase,
                    eintrag.ZeitpunktMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.PrognoseRestMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.IstRestMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.AbweichungMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.AbweichungProzent?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                    eintrag.PrognoseBearbeitungsRestMinuten.ToString("F2", CultureInfo.InvariantCulture),
                    eintrag.GesampelteBearbeitungsRestMinuten?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                    eintrag.AbweichungBearbeitungMinuten?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                    eintrag.AbweichungBearbeitungProzent?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                    eintrag.Korrekt ? "1" : "0",
                    eintrag.PrognoseFertigBisSchichtende ? "1" : "0"
                }));
            }

            File.WriteAllText(dateiPfad, sb.ToString());
        }

        public void SchreibePrognoseDatenJson(string dateiPfad)
        {
            var phaseDaten = prognoseErgebnisse
                .GroupBy(e => e.Phase)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Phase = g.Key,
                    Anzahl = g.Count(),
                    Korrekt = g.Count(e => e.Korrekt),
                    Trefferquote = g.Any() ? (g.Count(e => e.Korrekt) / (double)g.Count()) * 100.0 : 0.0
                })
                .ToList();

            var daten = new
            {
                AnzahlPrognosePruefungen,
                AnzahlPrognoseRichtig,
                AnzahlPrognoseAbbruch,
                PrognoseTrefferToleranzProzent = PrognoseTrefferToleranzAnteil * 100.0,
                PrognoseTrefferMindestToleranzMinuten,
                PrognoseTrefferquote,
                Abbruchgruende = new
                {
                    Prognose = AnzahlPrognoseAbbruch,
                    Aufnahmeprognose = AnzahlPrognoseAufnahmeAbgewiesen,
                    RezeptionFeierabend = AnzahlNichtBehandeltRezeptionFeierabend,
                    SchwesterFeierabend = AnzahlNichtBehandeltSchwesterFeierabend,
                    ArztFeierabend = AnzahlNichtBehandeltArztFeierabend
                },
                Phasen = phaseDaten,
                Ergebnisse = prognoseErgebnisse.Select(e => new
                {
                    e.PatientId,
                    e.Phase,
                    e.ZeitpunktMinuten,
                    e.PrognoseRestMinuten,
                    IstRestMinuten = e.IstRestMinuten,
                    e.AbweichungMinuten,
                    e.AbweichungProzent,
                    e.PrognoseBearbeitungsRestMinuten,
                    e.GesampelteBearbeitungsRestMinuten,
                    e.AbweichungBearbeitungMinuten,
                    e.AbweichungBearbeitungProzent,
                    e.Korrekt,
                    e.PrognoseFertigBisSchichtende
                }),
                PrognoseAbbrueche = prognoseAbbrueche.Select(a => new
                {
                    a.ZeitpunktMinuten,
                    a.Phase
                }),
                AufnahmeprognosePruefungen = prognoseAufnahmePruefungen.Select(p => new
                {
                    p.Tag,
                    p.ZeitpunktMinuten,
                    p.AufnahmeKapazitaet
                }),
                AufnahmeprognoseEntscheidungen = prognoseAufnahmeEntscheidungen.Select(e => new
                {
                    e.Tag,
                    e.ZeitpunktMinuten,
                    e.PatientId,
                    e.Zugelassen,
                    e.RestKapazitaet
                })
            };

            var optionen = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(dateiPfad, JsonSerializer.Serialize(daten, optionen));
        }

        public void ErfassePatientenTyp(PatientenTyp typ)
        {
            PatientenTypZaehler[typ]++;
        }

        public double DurchschnittlicheArztWartezeitNachTyp(PatientenTyp typ)
        {
            return MittelwertOder0(WartezeitenArztNachTyp[typ]);
        }

        public double DurchschnittlicheSchwesterWartezeitNachTyp(PatientenTyp typ)
        {
            return MittelwertOder0(WartezeitenSchwesterNachTyp[typ]);
        }

        private static (string Von, string Zu) ErmittleZustandswechsel(string eventTyp)
        {
            if (EventZustandsMapping.TryGetValue(eventTyp, out var mapping))
            {
                return mapping;
            }

            return (ZustandUnveraendert, ZustandUnveraendert);
        }

        private static void FuegeNachTerminHinzu(
            double wert,
            bool hatTermin,
            List<double> werteMitTermin,
            List<double> werteOhneTermin)
        {
            if (hatTermin)
            {
                werteMitTermin.Add(wert);
                return;
            }

            werteOhneTermin.Add(wert);
        }

        private static double MittelwertOder0(List<double> werte)
        {
            return werte.Count > 0 ? werte.Average() : 0;
        }

        private static Dictionary<string, (string Von, string Zu)> ErstelleEventZustandsMapping()
        {
            string dateiPfad = ErmittleRessourcenDateiPfad("event-zustandsmapping.json");

            string json = File.ReadAllText(dateiPfad);
            var jsonDaten = JsonSerializer.Deserialize<Dictionary<string, EventZustandsEintrag>>(json);

            if (jsonDaten is null || jsonDaten.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Die Event-Mapping-Datei ist leer oder ungültig: {dateiPfad}");
            }

            return jsonDaten.ToDictionary(
                eintrag => eintrag.Key,
                eintrag => (eintrag.Value.Von, eintrag.Value.Zu),
                StringComparer.Ordinal);
        }

        private static string ErmittleRessourcenDateiPfad(string dateiname)
        {
            string[] kandidaten =
            {
                Path.Combine(AppContext.BaseDirectory, "Ressourcen", dateiname),
                Path.Combine(Directory.GetCurrentDirectory(), "Ressourcen", dateiname),
                Path.Combine(Directory.GetCurrentDirectory(), "simSharpSimulation", "Ressourcen", dateiname)
            };

            foreach (string kandidat in kandidaten)
            {
                if (File.Exists(kandidat))
                    return kandidat;
            }

            string? projektOrdner = FindeOrdnerMitDatei(AppContext.BaseDirectory, "simSharpSimulation.csproj")
                ?? FindeOrdnerMitDatei(Directory.GetCurrentDirectory(), "simSharpSimulation.csproj");

            if (!string.IsNullOrEmpty(projektOrdner))
            {
                string pfadImProjekt = Path.Combine(projektOrdner, "Ressourcen", dateiname);
                if (File.Exists(pfadImProjekt))
                    return pfadImProjekt;
            }

            throw new FileNotFoundException(
                $"Die Ressourcen-Datei wurde nicht gefunden: {dateiname}",
                dateiname);
        }

        private static string? FindeOrdnerMitDatei(string startPfad, string dateiname)
        {
            DirectoryInfo? current = new(startPfad);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, dateiname)))
                    return current.FullName;

                current = current.Parent;
            }

            return null;
        }

        private TagesHitMissZaehler ErmittleOderErzeugeTagesHitMiss(DateTime tag)
        {
            DateTime datum = tag.Date;
            if (!hitMissProTag.TryGetValue(datum, out TagesHitMissZaehler? zaehler))
            {
                zaehler = new TagesHitMissZaehler();
                hitMissProTag[datum] = zaehler;
            }

            return zaehler;
        }

        private sealed class EventZustandsEintrag
        {
            public string Von { get; set; } = string.Empty;
            public string Zu { get; set; } = string.Empty;
        }

        private sealed class TagesHitMissZaehler
        {
            public int Hit { get; set; }
            public int Miss { get; set; }
        }

        private sealed record PrognosePruefung(
            int PatientId,
            string Phase,
            double ZeitpunktMinuten,
            double PrognoseRestMinuten,
            double PrognoseBearbeitungsRestMinuten,
            double VerbrauchteBearbeitungsMinuten,
            bool PrognoseFertigBisSchichtende);

        private sealed record PrognoseErgebnis(
            int PatientId,
            string Phase,
            double ZeitpunktMinuten,
            double PrognoseRestMinuten,
            double PrognoseBearbeitungsRestMinuten,
            double? GesampelteBearbeitungsRestMinuten,
            double? AbweichungBearbeitungMinuten,
            double? AbweichungBearbeitungProzent,
            double IstRestMinuten,
            double AbweichungMinuten,
            double? AbweichungProzent,
            bool Korrekt,
            bool PrognoseFertigBisSchichtende);

        private sealed record PrognoseAbbruchPunkt(
            double ZeitpunktMinuten,
            string Phase);

        private sealed record PrognoseAufnahmePruefung(
            DateTime Tag,
            double ZeitpunktMinuten,
            int AufnahmeKapazitaet);

        private sealed record PrognoseAufnahmeEntscheidung(
            DateTime Tag,
            double ZeitpunktMinuten,
            int PatientId,
            bool Zugelassen,
            int RestKapazitaet);
    }

    internal readonly record struct TagesHitMissPunkt(string Label, int Hit, int Miss);
}
