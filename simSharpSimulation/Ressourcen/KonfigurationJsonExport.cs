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

        // Hilfszugang: berechnete Mietkosten pro Tag (Anzahl * Kosten pro Behandlungsraum).
        public static double MietkostenProTag => Finanzen.Fixkosten.MietkostenProBehandlungsraumProTag * Math.Max(Finanzen.Fixkosten.AnzahlBehandlungsraeume, 0);

        public static void LadeAlle()
        {
            string zielOrdner = ErmittleRessourcenOrdner();
            if (!AlleKonfigurationsdateienVorhanden(zielOrdner))
            {
                ExportiereAlle();
            }

            var arzt = LeseJson<ArztKonfigurationJson>(Path.Combine(zielOrdner, "arzt-konfiguration.json"));
            ArztKonfiguration.ANZAHL_AERZTE = arzt.AnzahlAerzte;
            ArztKonfiguration.MITTLERE_BEHANDLUNGSZEIT = arzt.MittlereBehandlungszeit;

            var schwester = LeseJson<SchwesterKonfigurationJson>(Path.Combine(zielOrdner, "schwester-konfiguration.json"));
            SchwesterKonfiguration.ANZAHL_SCHWESTERN = schwester.AnzahlSchwestern;
            SchwesterKonfiguration.MITTLERE_SCHWESTER_ZEIT = schwester.MittlereSchwesterZeit;

            var rezeption = LeseJson<RezeptionKonfigurationJson>(Path.Combine(zielOrdner, "rezeption-konfiguration.json"));
            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = rezeption.AnzahlRezeptionisten;
            RezeptionKonfiguration.MITTELREZEPTIONSZEIT = rezeption.MittelRezeptionszeit;

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
                ExportiereAlle();
            }

            Finanzen = LeseJson<FinanzKonfigurationJson>(Path.Combine(zielOrdner, "finanz-konfiguration.json"));
        }

        public static void ExportiereAlle()
        {
            string zielOrdner = ErmittleRessourcenOrdner();
            Directory.CreateDirectory(zielOrdner);

            SchreibeJson(Path.Combine(zielOrdner, "arzt-konfiguration.json"), new ArztKonfigurationJson
            {
                AnzahlAerzte = ArztKonfiguration.ANZAHL_AERZTE,
                MittlereBehandlungszeit = ArztKonfiguration.MITTLERE_BEHANDLUNGSZEIT,
                Beschreibung = new ArztKonfiguration().Beschreibung
            });

            SchreibeJson(Path.Combine(zielOrdner, "schwester-konfiguration.json"), new SchwesterKonfigurationJson
            {
                AnzahlSchwestern = SchwesterKonfiguration.ANZAHL_SCHWESTERN,
                MittlereSchwesterZeit = SchwesterKonfiguration.MITTLERE_SCHWESTER_ZEIT,
                Beschreibung = new SchwesterKonfiguration().Beschreibung
            });

            SchreibeJson(Path.Combine(zielOrdner, "rezeption-konfiguration.json"), new RezeptionKonfigurationJson
            {
                AnzahlRezeptionisten = RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN,
                MittelRezeptionszeit = RezeptionKonfiguration.MITTELREZEPTIONSZEIT,
                Beschreibung = new RezeptionKonfiguration().Beschreibung
            });

            SchreibeJson(Path.Combine(zielOrdner, "patienten-konfiguration.json"), new PatientenKonfigurationJson
            {
                AnzahlPatientenTag = PatientenKonfiguration.ANZAHL_PATIENTEN_TAG,
                Erwartungswert = PatientenKonfiguration.ERWARTUNGSWERT,
                Standardabweichung = PatientenKonfiguration.STANDARDABWEICHUNG,
                TerminWahrscheinlichkeit = PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT,
                TerminVorbereitungWahrscheinlichkeit = PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT,
                OhneTerminVorbereitungWahrscheinlichkeit = PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT,
                MittlereWartezimmerDauerSchwester = PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER,
                MittlereWartezimmerDauerArzt = PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_ARZT,
                MitTerminWartezimmerFaktorSchwester = PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER,
                OhneTerminWartezimmerFaktorSchwester = PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER,
                MitTerminWartezimmerFaktorArzt = PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT,
                OhneTerminWartezimmerFaktorArzt = PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT,
                TypenVerteilung = PatientenKonfiguration.TYPEN_VERTEILUNG.Select(t => new PatientenTypKonfigurationJson
                {
                    Typ = t.Typ,
                    Wahrscheinlichkeit = t.Wahrscheinlichkeit,
                    BehandlungszeitArzt = t.BehandlungszeitArzt,
                    VariationskoeffizientArzt = t.VariationskoeffizientArzt,
                    BehandlungszeitSchwester = t.BehandlungszeitSchwester,
                    VariationskoeffizientSchwester = t.VariationskoeffizientSchwester,
                    Behandlungskosten = t.Behandlungskosten
                }).ToList(),
                Beschreibung = new PatientenKonfiguration().Beschreibung
            });

            SchreibeJson(Path.Combine(zielOrdner, "simulation-konfiguration.json"), new SimulationKonfigurationJson
            {
                RandomSeed = SimulationKonfiguration.RANDOM_SEED,
                Simulationsdauer = SimulationKonfiguration.SIMULATIONSDAUER,
                BewegungszeitEingangZurRezeptionSekunden = SimulationKonfiguration.BEWEGUNGSZEIT_EINGANG_ZUR_REZEPTION_SEKUNDEN,
                BewegungszeitInnerhalbKlinikSekunden = SimulationKonfiguration.BEWEGUNGSZEIT_INNERHALB_KLINIK_SEKUNDEN,
                BewegungszeitArztZumAusgangSekunden = SimulationKonfiguration.BEWEGUNGSZEIT_ARZT_ZUM_AUSGANG_SEKUNDEN,
                BewegungszeitRezeptionZumAusgangSekunden = SimulationKonfiguration.BEWEGUNGSZEIT_REZEPTION_ZUM_AUSGANG_SEKUNDEN
            });

            SchreibeJson(Path.Combine(zielOrdner, "finanz-konfiguration.json"), new FinanzKonfigurationJson
            {
                Personal = new PersonalKostenJson(),
                Fixkosten = new FixkostenJson(),
                Versicherung = new VersicherungsKostenJson(),
                Behandlungskosten = new BehandlungskostenJson()
            });
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
        public double MittlereBehandlungszeit { get; set; }
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class SchwesterKonfigurationJson
    {
        public int AnzahlSchwestern { get; set; }
        public double MittlereSchwesterZeit { get; set; }
        public string Beschreibung { get; set; } = string.Empty;
    }

    internal sealed class RezeptionKonfigurationJson
    {
        public int AnzahlRezeptionisten { get; set; }
        public double MittelRezeptionszeit { get; set; }
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
        public double MietkostenProBehandlungsraumProTag { get; set; } = 50.0;
        public int AnzahlBehandlungsraeume { get; set; } = 5;
        public double WeitereFixkostenProTag { get; set; } = 450.0;
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
