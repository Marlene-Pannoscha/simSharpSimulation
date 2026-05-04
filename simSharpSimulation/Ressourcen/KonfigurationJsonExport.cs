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
                .Select(t => (t.Typ, t.Wahrscheinlichkeit, t.BehandlungszeitArzt, t.BehandlungszeitSchwester))
                .ToArray();

            var simulation = LeseJson<SimulationKonfigurationJson>(Path.Combine(zielOrdner, "simulation-konfiguration.json"));
            SimulationKonfiguration.RANDOM_SEED = simulation.RandomSeed;
            SimulationKonfiguration.SIMULATIONSDAUER = simulation.Simulationsdauer;
            SimulationKonfiguration.BEWEGUNGSZEIT_EINGANG_ZUR_REZEPTION_SEKUNDEN = simulation.BewegungszeitEingangZurRezeptionSekunden;
            SimulationKonfiguration.BEWEGUNGSZEIT_INNERHALB_KLINIK_SEKUNDEN = simulation.BewegungszeitInnerhalbKlinikSekunden;
            SimulationKonfiguration.BEWEGUNGSZEIT_ARZT_ZUM_AUSGANG_SEKUNDEN = simulation.BewegungszeitArztZumAusgangSekunden;
            SimulationKonfiguration.BEWEGUNGSZEIT_REZEPTION_ZUM_AUSGANG_SEKUNDEN = simulation.BewegungszeitRezeptionZumAusgangSekunden;

            var finanzen = LeseJson<FinanzKonfigurationJson>(Path.Combine(zielOrdner, "finanz-konfiguration.json"));
            FinanzKonfiguration.ARZT_LOHN_PRO_PATIENT = finanzen.Personal.ArztLohnProPatient;
            FinanzKonfiguration.ARZT_LOHN_PRO_STUNDE = finanzen.Personal.ArztLohnProStunde;
            FinanzKonfiguration.SCHWESTER_LOHN_PRO_STUNDE = finanzen.Personal.SchwesterLohnProStunde;
            FinanzKonfiguration.REZEPTION_LOHN_PRO_STUNDE = finanzen.Personal.RezeptionLohnProStunde;
            FinanzKonfiguration.MIETKOSTEN_PRO_TAG = finanzen.Fixkosten.MietkostenProTag;
            FinanzKonfiguration.WEITERE_FIXKOSTEN_PRO_TAG = finanzen.Fixkosten.WeitereFixkostenProTag;
            FinanzKonfiguration.ARBEITSSTUNDEN_PRO_TAG = finanzen.Personal.ArbeitsstundenProTag;
            FinanzKonfiguration.ANTEIL_PRIVATVERSICHERT = finanzen.Versicherung.AnteilPrivatversichert;
            FinanzKonfiguration.EINNAHME_PRIVATPATIENT = finanzen.Versicherung.EinnahmePrivatpatient;
            FinanzKonfiguration.EINNAHME_GESETZLICH_PATIENT = finanzen.Versicherung.EinnahmeGesetzlichPatient;
            FinanzKonfiguration.BEHANDLUNGSKOSTEN_KURZ = finanzen.Behandlungskosten.Kurz;
            FinanzKonfiguration.BEHANDLUNGSKOSTEN_MITTEL = finanzen.Behandlungskosten.Mittel;
            FinanzKonfiguration.BEHANDLUNGSKOSTEN_LANG = finanzen.Behandlungskosten.Lang;
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
                    BehandlungszeitSchwester = t.BehandlungszeitSchwester
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
                Personal = new PersonalKostenJson
                {
                    ArztLohnProPatient = FinanzKonfiguration.ARZT_LOHN_PRO_PATIENT,
                    ArztLohnProStunde = FinanzKonfiguration.ARZT_LOHN_PRO_STUNDE,
                    SchwesterLohnProStunde = FinanzKonfiguration.SCHWESTER_LOHN_PRO_STUNDE,
                    RezeptionLohnProStunde = FinanzKonfiguration.REZEPTION_LOHN_PRO_STUNDE,
                    ArbeitsstundenProTag = FinanzKonfiguration.ARBEITSSTUNDEN_PRO_TAG
                },
                Fixkosten = new FixkostenJson
                {
                    MietkostenProTag = FinanzKonfiguration.MIETKOSTEN_PRO_TAG,
                    WeitereFixkostenProTag = FinanzKonfiguration.WEITERE_FIXKOSTEN_PRO_TAG
                },
                Versicherung = new VersicherungsKostenJson
                {
                    AnteilPrivatversichert = FinanzKonfiguration.ANTEIL_PRIVATVERSICHERT,
                    EinnahmePrivatpatient = FinanzKonfiguration.EINNAHME_PRIVATPATIENT,
                    EinnahmeGesetzlichPatient = FinanzKonfiguration.EINNAHME_GESETZLICH_PATIENT
                },
                Behandlungskosten = new BehandlungskostenJson
                {
                    Kurz = FinanzKonfiguration.BEHANDLUNGSKOSTEN_KURZ,
                    Mittel = FinanzKonfiguration.BEHANDLUNGSKOSTEN_MITTEL,
                    Lang = FinanzKonfiguration.BEHANDLUNGSKOSTEN_LANG
                }
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
        public double BehandlungszeitSchwester { get; set; }
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
        public double ArztLohnProPatient { get; set; }
        public double ArztLohnProStunde { get; set; }
        public double SchwesterLohnProStunde { get; set; }
        public double RezeptionLohnProStunde { get; set; }
        public int ArbeitsstundenProTag { get; set; }
    }

    internal sealed class FixkostenJson
    {
        public double MietkostenProTag { get; set; }
        public double WeitereFixkostenProTag { get; set; }
    }

    internal sealed class VersicherungsKostenJson
    {
        public double AnteilPrivatversichert { get; set; }
        public double EinnahmePrivatpatient { get; set; }
        public double EinnahmeGesetzlichPatient { get; set; }
    }

    internal sealed class BehandlungskostenJson
    {
        public double Kurz { get; set; }
        public double Mittel { get; set; }
        public double Lang { get; set; }
    }
}
