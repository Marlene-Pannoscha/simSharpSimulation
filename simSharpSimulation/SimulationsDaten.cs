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
        public List<double> RezeptionsBehandlungszeitenMitTermin { get; } = new();
        public List<double> RezeptionsBehandlungszeitenOhneTermin { get; } = new();

        public List<double> Gesamtprozesszeiten { get; } = new();
        public List<double> GesamtprozesszeitenMitTermin { get; } = new();
        public List<double> GesamtprozesszeitenOhneTermin { get; } = new();
        public List<double> EchteAnkunftszeiten { get; } = new();

        public Dictionary<PatientenTyp, int> PatientenTypZaehler { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => 0);

        public List<double> ArztBehandlungszeitenMitTermin { get; } = new();
        public List<double> ArztBehandlungszeitenOhneTermin { get; } = new();
        public Dictionary<PatientenTyp, List<double>> ArztBehandlungszeitenNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());
        public Dictionary<PatientenTyp, List<double>> SchwesternBehandlungszeitenNachTyp { get; } =
            Enum.GetValues<PatientenTyp>().ToDictionary(typ => typ, _ => new List<double>());
        private readonly SortedDictionary<DateTime, TagesHitMissZaehler> hitMissProTag = new();

        public int AnzahlBehandeltHit { get; private set; }
        public int AnzahlAbgebrochenMiss { get; private set; }

        public int AnzahlNichtBehandeltArztWartezeit { get; private set; }
        public int AnzahlNichtBehandeltArztFeierabend { get; private set; }
        public int AnzahlNichtBehandeltArztGesamt =>
            AnzahlNichtBehandeltArztWartezeit + AnzahlNichtBehandeltArztFeierabend;
        public int AnzahlNichtBehandeltSchwesterWartezeit { get; private set; }
        public int AnzahlNichtBehandeltSchwesterFeierabend { get; private set; }
        public int AnzahlNichtBehandeltSchwesterGesamt =>
            AnzahlNichtBehandeltSchwesterWartezeit + AnzahlNichtBehandeltSchwesterFeierabend;
        public int AnzahlNichtBehandeltRezeptionWartezeit { get; private set; }
        public int AnzahlNichtBehandeltRezeptionFeierabend { get; private set; }
        public int AnzahlNichtBehandeltRezeptionGesamt =>
            AnzahlNichtBehandeltRezeptionWartezeit + AnzahlNichtBehandeltRezeptionFeierabend;

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
            AnzahlNichtBehandeltArztWartezeit++;
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
            AnzahlNichtBehandeltSchwesterWartezeit++;
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
            AnzahlNichtBehandeltRezeptionWartezeit++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
        }

        public void ErfasseRezeptionAbbruchFeierabend(DateTime tag)
        {
            AnzahlAbgebrochenMiss++;
            AnzahlNichtBehandeltRezeptionFeierabend++;
            ErmittleOderErzeugeTagesHitMiss(tag).Miss++;
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
    }

    internal readonly record struct TagesHitMissPunkt(string Label, int Hit, int Miss);
}
