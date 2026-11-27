using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace simSharpSimulation
{
    internal static class KonfigurationJsonExport
    {
        private static readonly JsonSerializerOptions JsonOptionen = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static FinanzKonfigurationJson Finanzen { get; private set; } = new();

        // Hilfszugang: berechnete Mietkosten pro Tag auf Basis der Raumflaechen.
        public static double MietkostenProTag => Finanzen.Fixkosten.BerechneMietkostenProTag();

        public static void LadeAlle() => LadeAlle(ErmittleRessourcenOrdner());

        internal static void LadeAlle(string zielOrdner)
        {
            var arzt = LeseJson<ArztKonfigurationJson>(zielOrdner, "arzt-konfiguration.json");
            var schwester = LeseJson<SchwesterKonfigurationJson>(zielOrdner, "schwester-konfiguration.json");
            var rezeption = LeseJson<RezeptionKonfigurationJson>(zielOrdner, "rezeption-konfiguration.json");
            var patienten = LeseJson<PatientenKonfigurationJson>(zielOrdner, "patienten-konfiguration.json");
            var simulation = LeseJson<SimulationKonfigurationJson>(zielOrdner, "simulation-konfiguration.json");
            var finanzen = LeseFinanzen(zielOrdner);

            Konfigurationspruefung.Pruefe(arzt, schwester, rezeption, patienten, simulation);
            var typenVerteilung = patienten.TypenVerteilung
                .Select(t => (t.Typ, t.Wahrscheinlichkeit, t.BehandlungszeitArzt, t.VariationskoeffizientArzt,
                    t.BehandlungszeitSchwester, t.VariationskoeffizientSchwester))
                .ToArray();

            ArztKonfiguration.ANZAHL_AERZTE = arzt.AnzahlAerzte;
            ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER = arzt.MittlereBehandlungsdauer;

            SchwesterKonfiguration.ANZAHL_SCHWESTERN = schwester.AnzahlSchwestern;
            SchwesterKonfiguration.MITTLERE_BEHANDLUNGSDAUER = schwester.MittlereBehandlungsdauer;

            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = rezeption.AnzahlRezeptionisten;
            RezeptionKonfiguration.MITTELREZEPTIONSZEIT = rezeption.MittelRezeptionszeit;
            RezeptionKonfiguration.VARIATIONSKOEFFIZIENT_REZEPTION = rezeption.VariationskoeffizientRezeption;

            PatientenKonfiguration.ZWISCHENANKUNFT_ERSTE_2_STUNDEN_MINUTEN = patienten.ZwischenankunftErste2StundenMinuten;
            PatientenKonfiguration.ZWISCHENANKUNFT_NAECHSTE_3_STUNDEN_MINUTEN = patienten.ZwischenankunftNaechste3StundenMinuten;
            PatientenKonfiguration.ZWISCHENANKUNFT_LETZTE_3_STUNDEN_MINUTEN = patienten.ZwischenankunftLetzte3StundenMinuten;
            PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT = patienten.TerminWahrscheinlichkeit;
            PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT = patienten.TerminVorbereitungWahrscheinlichkeit;
            PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT = patienten.OhneTerminVorbereitungWahrscheinlichkeit;
            PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER = patienten.MittlereWartezimmerDauerSchwester;
            PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_ARZT = patienten.MittlereWartezimmerDauerArzt;
            PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER = patienten.MitTerminWartezimmerFaktorSchwester;
            PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER = patienten.OhneTerminWartezimmerFaktorSchwester;
            PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT = patienten.MitTerminWartezimmerFaktorArzt;
            PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT = patienten.OhneTerminWartezimmerFaktorArzt;
            PatientenKonfiguration.TYPEN_VERTEILUNG = typenVerteilung;

            SimulationKonfiguration.RANDOM_SEED = simulation.RandomSeed;
            SimulationKonfiguration.SIMULATIONSDAUER = simulation.Simulationsdauer;
            SimulationKonfiguration.PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN = simulation.PrognosePruefungVorSchliessungMinuten;
            SimulationKonfiguration.NACHLAUF_MINUTEN = simulation.NachlaufMinuten;
            SimulationKonfiguration.WAHRSCHEINLICHKEIT_NACH_ARZT_ZUR_REZEPTION = simulation.WahrscheinlichkeitNachArztZurRezeption;
            SimulationKonfiguration.BEWEGUNGSZEIT_EINGANG_ZUR_REZEPTION_SEKUNDEN = simulation.BewegungszeitEingangZurRezeptionSekunden;
            SimulationKonfiguration.BEWEGUNGSZEIT_INNERHALB_KLINIK_SEKUNDEN = simulation.BewegungszeitInnerhalbKlinikSekunden;
            SimulationKonfiguration.BEWEGUNGSZEIT_ARZT_ZUM_AUSGANG_SEKUNDEN = simulation.BewegungszeitArztZumAusgangSekunden;
            SimulationKonfiguration.BEWEGUNGSZEIT_REZEPTION_ZUM_AUSGANG_SEKUNDEN = simulation.BewegungszeitRezeptionZumAusgangSekunden;

            Finanzen = finanzen;
        }

        public static void LadeFinanzen() => Finanzen = LeseFinanzen(ErmittleRessourcenOrdner());

        private static FinanzKonfigurationJson LeseFinanzen(string ordner)
        {
            var finanzen = LeseJson<FinanzKonfigurationJson>(ordner, "finanz-konfiguration.json");
            Konfigurationspruefung.PruefeFinanzen(finanzen);
            return finanzen;
        }

        private static T LeseJson<T>(string ordner, string dateiname)
        {
            string pfad = Path.Combine(ordner, dateiname);
            string json = File.ReadAllText(pfad);
            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptionen)
                    ?? throw new InvalidDataException($"Konfiguration fehlt in {pfad}.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Ungueltige Konfiguration in {pfad} bei {ex.Path}.", ex);
            }
        }

        private static string ErmittleRessourcenOrdner()
        {
            string ausgabeRessourcen = Path.Combine(AppContext.BaseDirectory, "Ressourcen");
            if (Directory.Exists(ausgabeRessourcen))
                return ausgabeRessourcen;

            string aktuellerOrdner = Directory.GetCurrentDirectory();
            string projektRessourcen = Path.Combine(aktuellerOrdner, "simSharpSimulation", "Ressourcen");
            if (Directory.Exists(projektRessourcen))
            {
                return projektRessourcen;
            }

            return Path.Combine(aktuellerOrdner, "Ressourcen");
        }
    }
}
