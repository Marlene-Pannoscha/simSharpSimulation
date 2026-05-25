using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace simSharpSimulation
{
    internal static class KonfigurationJsonExport
    {
        private static readonly JsonSerializerOptions JsonOptionen = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static FinanzKonfigurationJson Finanzen { get; private set; } = new();

        // Hilfszugang: berechnete Mietkosten pro Tag auf Basis der Raumflaechen.
        public static double MietkostenProTag => Finanzen.Fixkosten.BerechneMietkostenProTag();

        public static void LadeAlle()
        {
            string zielOrdner = ErmittleRessourcenOrdner();
            if (!AlleKonfigurationsdateienVorhanden(zielOrdner))
            {
                throw new FileNotFoundException($"Konfigurationen fehlen im Ressourcenordner: {zielOrdner}");
            }

            var arzt = LeseJson<ArztKonfigurationJson>(Path.Combine(zielOrdner, "arzt-konfiguration.json"));
            ArztKonfiguration.ANZAHL_AERZTE = arzt.AnzahlAerzte;
            ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER = arzt.MittlereBehandlungsdauer;

            var schwester = LeseJson<SchwesterKonfigurationJson>(Path.Combine(zielOrdner, "schwester-konfiguration.json"));
            SchwesterKonfiguration.ANZAHL_SCHWESTERN = schwester.AnzahlSchwestern;
            SchwesterKonfiguration.MITTLERE_BEHANDLUNGSDAUER = schwester.MittlereBehandlungsdauer;

            var rezeption = LeseJson<RezeptionKonfigurationJson>(Path.Combine(zielOrdner, "rezeption-konfiguration.json"));
            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = rezeption.AnzahlRezeptionisten;
            RezeptionKonfiguration.MITTELREZEPTIONSZEIT = rezeption.MittelRezeptionszeit;
            RezeptionKonfiguration.VARIATIONSKOEFFIZIENT_REZEPTION = rezeption.VariationskoeffizientRezeption;

            var patienten = LeseJson<PatientenKonfigurationJson>(Path.Combine(zielOrdner, "patienten-konfiguration.json"));
            PatientenKonfiguration.ANZAHL_PATIENTEN_TAG = patienten.AnzahlPatientenTag;
            PatientenKonfiguration.ERWARTUNGSWERT = patienten.Erwartungswert;
            PatientenKonfiguration.STANDARDABWEICHUNG = patienten.Standardabweichung;
            PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT = patienten.TerminWahrscheinlichkeit;
            PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT = patienten.TerminVorbereitungWahrscheinlichkeit;
            PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT = patienten.OhneTerminVorbereitungWahrscheinlichkeit;
            PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER = patienten.MittlereWartezimmerDauerSchwester;
            PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_ARZT = patienten.MittlereWartezimmerDauerArzt;
            PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER = patienten.MitTerminWartezimmerFaktorSchwester;
            PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER = patienten.OhneTerminWartezimmerFaktorSchwester;
            PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT = patienten.MitTerminWartezimmerFaktorArzt;
            PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT = patienten.OhneTerminWartezimmerFaktorArzt;
            PatientenKonfiguration.TYPEN_VERTEILUNG = patienten.TypenVerteilung
                .Select(t => (t.Typ, t.Wahrscheinlichkeit, t.BehandlungszeitArzt, t.VariationskoeffizientArzt, t.BehandlungszeitSchwester, t.VariationskoeffizientSchwester, t.Behandlungskosten))
                .ToArray();

            var simulation = LeseJson<SimulationKonfigurationJson>(Path.Combine(zielOrdner, "simulation-konfiguration.json"));
            SimulationKonfiguration.RANDOM_SEED = simulation.RandomSeed;
            SimulationKonfiguration.SIMULATIONSDAUER = simulation.Simulationsdauer;
            SimulationKonfiguration.BEWEGUNGSZEIT_EINGANG_ZUR_REZEPTION_SEKUNDEN = simulation.BewegungszeitEingangZurRezeptionSekunden;
            SimulationKonfiguration.BEWEGUNGSZEIT_INNERHALB_KLINIK_SEKUNDEN = simulation.BewegungszeitInnerhalbKlinikSekunden;
            SimulationKonfiguration.BEWEGUNGSZEIT_ARZT_ZUM_AUSGANG_SEKUNDEN = simulation.BewegungszeitArztZumAusgangSekunden;
            SimulationKonfiguration.BEWEGUNGSZEIT_REZEPTION_ZUM_AUSGANG_SEKUNDEN = simulation.BewegungszeitRezeptionZumAusgangSekunden;

            LadeFinanzen();
        }

        public static void LadeFinanzen()
        {
            string zielOrdner = ErmittleRessourcenOrdner();
            if (!AlleKonfigurationsdateienVorhanden(zielOrdner))
            {
                throw new FileNotFoundException($"Konfigurationen fehlen im Ressourcenordner: {zielOrdner}");
            }

            Finanzen = LeseJson<FinanzKonfigurationJson>(Path.Combine(zielOrdner, "finanz-konfiguration.json"));
        }

        public static void ExportiereAlle()
        {
            throw new InvalidOperationException("ExportiereAlle ist deaktiviert. Bitte JSON-Konfigurationen bereitstellen.");
        }

        private static T LeseJson<T>(string pfad)
        {
            string json = File.ReadAllText(pfad);
            return JsonSerializer.Deserialize<T>(json, JsonOptionen)
                ?? throw new InvalidDataException($"Konfiguration konnte nicht geladen werden: {pfad}");
        }

        private static void SchreibeJson<T>(string pfad, T objekt)
        {
            string json = JsonSerializer.Serialize(objekt, JsonOptionen);
            File.WriteAllText(pfad, json);
        }

        private static bool AlleKonfigurationsdateienVorhanden(string ordner)
        {
            return File.Exists(Path.Combine(ordner, "arzt-konfiguration.json"))
                && File.Exists(Path.Combine(ordner, "schwester-konfiguration.json"))
                && File.Exists(Path.Combine(ordner, "rezeption-konfiguration.json"))
                && File.Exists(Path.Combine(ordner, "patienten-konfiguration.json"))
                && File.Exists(Path.Combine(ordner, "simulation-konfiguration.json"))
                && File.Exists(Path.Combine(ordner, "finanz-konfiguration.json"));
        }

        private static string ErmittleRessourcenOrdner()
        {
            string aktuellerOrdner = Directory.GetCurrentDirectory();
            string projektRessourcen = Path.Combine(aktuellerOrdner, "simSharpSimulation", "Ressourcen");
            if (Directory.Exists(projektRessourcen))
            {
                return projektRessourcen;
            }

            return Path.Combine(aktuellerOrdner, "Ressourcen");
        }
    }

    internal sealed class ArztKonfigurationJson
    {
        public int AnzahlAerzte { get; set; }
        public double MittlereBehandlungsdauer { get; set; }
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class SchwesterKonfigurationJson
    {
        public int AnzahlSchwestern { get; set; }
        public double MittlereBehandlungsdauer { get; set; }
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class RezeptionKonfigurationJson
    {
        public int AnzahlRezeptionisten { get; set; }
        public double MittelRezeptionszeit { get; set; }
        public double VariationskoeffizientRezeption { get; set; } = 1.0;
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class PatientenKonfigurationJson
    {
        public int AnzahlPatientenTag { get; set; }
        public double Erwartungswert { get; set; }
        public double Standardabweichung { get; set; }
        public double TerminWahrscheinlichkeit { get; set; }
        public double TerminVorbereitungWahrscheinlichkeit { get; set; }
        public double OhneTerminVorbereitungWahrscheinlichkeit { get; set; }
        public double MittlereWartezimmerDauerSchwester { get; set; }
        public double MittlereWartezimmerDauerArzt { get; set; }
        public double MitTerminWartezimmerFaktorSchwester { get; set; }
        public double OhneTerminWartezimmerFaktorSchwester { get; set; }
        public double MitTerminWartezimmerFaktorArzt { get; set; }
        public double OhneTerminWartezimmerFaktorArzt { get; set; }
        public List<PatientenTypKonfigurationJson> TypenVerteilung { get; set; } = new();
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class PatientenTypKonfigurationJson
    {
        public PatientenTyp Typ { get; set; }
        public double Wahrscheinlichkeit { get; set; }
        public double BehandlungszeitArzt { get; set; }
        public double VariationskoeffizientArzt { get; set; }
        public double BehandlungszeitSchwester { get; set; }
        public double VariationskoeffizientSchwester { get; set; }
        public double Behandlungskosten { get; set; }
    }

    internal sealed class SimulationKonfigurationJson
    {
        public int RandomSeed { get; set; }
        public double Simulationsdauer { get; set; }
        public int BewegungszeitEingangZurRezeptionSekunden { get; set; }
        public int BewegungszeitInnerhalbKlinikSekunden { get; set; }
        public int BewegungszeitArztZumAusgangSekunden { get; set; }
        public int BewegungszeitRezeptionZumAusgangSekunden { get; set; }
    }

    internal sealed class FinanzKonfigurationJson
    {
        public PersonalKostenJson Personal { get; set; } = new();
        public FixkostenJson Fixkosten { get; set; } = new();
        public VersicherungsKostenJson Versicherung { get; set; } = new();
        public BehandlungskostenJson Behandlungskosten { get; set; } = new();
    }

    internal sealed class PersonalKostenJson
    {
        public double ArztLohnProPatient { get; set; } = 30.0;
        public double ArztLohnProStunde { get; set; } = 85.0;
        public double SchwesterLohnProStunde { get; set; } = 32.0;
        public double RezeptionLohnProStunde { get; set; } = 24.0;
        public int ArbeitsstundenProTag { get; set; } = 8;
    }

    internal sealed class FixkostenJson
    {
        public double MietkostenProQuadratmeterProTag { get; set; } = 8.5;
        public int AnzahlBehandlungsraeumeSchwester { get; set; } = 3;
        public double FlaecheBehandlungsraumSchwesterQuadratmeter { get; set; } = 12.0;
        public int AnzahlBehandlungsraeumeArzt { get; set; } = 2;
        public double FlaecheBehandlungsraumArztQuadratmeter { get; set; } = 18.0;
        public double FlaecheWartezimmerQuadratmeter { get; set; } = 30.0;

        [JsonIgnore]
        public int AnzahlBehandlungsraeumeGesamt => AnzahlBehandlungsraeumeSchwester + AnzahlBehandlungsraeumeArzt;

        public double WeitereFixkostenProTag { get; set; } = 450.0;

        public double BerechneMietkostenProTag()
        {
            double flaecheGesamt = (AnzahlBehandlungsraeumeSchwester * FlaecheBehandlungsraumSchwesterQuadratmeter)
                + (AnzahlBehandlungsraeumeArzt * FlaecheBehandlungsraumArztQuadratmeter)
                + FlaecheWartezimmerQuadratmeter;

            return MietkostenProQuadratmeterProTag * Math.Max(flaecheGesamt, 0.0);
        }
    }

    internal sealed class VersicherungsKostenJson
    {
        public double AnteilPrivatversichert { get; set; } = 0.2;
        public double EinnahmePrivatpatient { get; set; } = 150.0;
        public double EinnahmeGesetzlichPatient { get; set; } = 90.0;
    }

    internal sealed class BehandlungskostenJson
    {
        public double Kurz { get; set; } = 18.0;
        public double Mittel { get; set; } = 35.0;
        public double Lang { get; set; } = 60.0;
    }
}
