using System;
using System.Collections.Generic;
using SimSharp;

// Ein 'namespace' (Namensraum) ist wie ein Ordner für Klassen, um den Code zu organisieren und Namenskonflikte zu vermeiden.
namespace simSharpSimulation
{
    // Kern-Datei der partial class:
    // Konstruktor, Felder und Tages-Orchestrierung der Simulation.
    /* Enthält die komplette Ablauf-Logik der Simulation:
     - Patientenprozess
     - Generator für Ankünfte
     - Start/Run der SimSharp-Umgebung
    */
    /* internal: Klasse ist nur innerhalb dieses Projekts sichtbar.
    sealed: Keine andere Klasse darf von dieser Klasse erben (sie ist "versiegelt").
    class: Der Bauplan für die Klinik-Simulation. */
    internal sealed partial class PatientenProzess
    {
        private const double WahrscheinlichkeitNachArztZurRezeption = 0.6;
        private readonly Random rnd;
        private readonly SimulationsDaten daten;
        private bool aufnahmeprognoseAktiviert;
        private readonly Dictionary<int, AktiverPatientPrognose> aktivePatientenPrognosen = new();
        private readonly HashSet<int> aufnahmeprognoseZugelassenePatienten = new();
        private readonly HashSet<int> aufnahmeprognoseAbgewiesenePatienten = new();
        private PrognoseRessourcenStatus rezeptionStatus = null!;
        private PrognoseRessourcenStatus schwesterStatus = null!;
        private PrognoseRessourcenStatus arztStatus = null!;

        // Schritt P1: Vorbereitung (Konstruktor)
        // Erhält einen Startwert für den Zufallsgenerator und ein Objekt zum Speichern der Ergebnisse.
        public PatientenProzess(int randomSeed, SimulationsDaten daten)
        {
            this.rnd = new Random(randomSeed);
            this.daten = daten;
        }

        // Schritt P2: Der Start
        // Richtet die Simulationsuhr und die Ärzte ein und startet den Ablauf.
        public void FuehreAus()
        {
            // Phase P-A: Tages-Simulation vorbereiten und starten.
            // Wir simulieren eine Arbeitswoche: 5 Tage (Montag bis Freitag).
            // Der 3. Januar 2000 war ein Montag.
            DateTime startDatum = new DateTime(2000, 1, 3);
            TimeSpan maximaleTagesdauer = BerechneMaximaleTagesdauer();

            for (int tag = 0; tag < Program.SimulierteArbeitstage; tag++) // 0: Montag, 1: Dienstag, ... 4: Freitag
            {
                // Schritt P2.1: Für jeden Tag eine neue Simulationsumgebung erzeugen.
                // Jeder Tag bekommt seine eigene Simulations-Umgebung (Uhr) und neue Ressourcen.
                // Das Datum wird für jeden Durchlauf um 'tag' Tage erhöht.
                var env = new Simulation(startDatum.AddDays(tag));
                aufnahmeprognoseAktiviert = false;
                aktivePatientenPrognosen.Clear();
                aufnahmeprognoseZugelassenePatienten.Clear();
                aufnahmeprognoseAbgewiesenePatienten.Clear();
                var aerzte = new BeweglicherArztPool(env, ArztKonfiguration.ANZAHL_AERZTE);
                var schwestern = new BeweglicherSchwesterPool(env, SchwesterKonfiguration.ANZAHL_SCHWESTERN);
                var rezeption = new Resource(env, capacity: RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN);
                rezeptionStatus = new PrognoseRessourcenStatus(RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN);
                schwesterStatus = new PrognoseRessourcenStatus(SchwesterKonfiguration.ANZAHL_SCHWESTERN);
                arztStatus = new PrognoseRessourcenStatus(ArztKonfiguration.ANZAHL_AERZTE);
                env.Process(AktiviereAufnahmeprognoseEineStundeVorSchliessung(env));

                // Schritt P3: PatientenGenerator für den jeweiligen Tag starten
                // PatientenGenerator liefert die Ankunftszeiten und startet für jede Ankunft
                // diesen Patient()-Ablauf als eigenen Simulationsprozess.
                // Eindeutige Patienten-IDs pro Tag, damit Trace-Auswertungen (z.B. Zeitachse eines Patienten) sauber sind.
                int patientIdStart = (tag * 10_000) + 1;
                env.Process(PatientenGenerator.Generiere(env, rezeption, aerzte, schwestern, rnd, daten, patientIdStart, Patient));

                // Schritt P2.2: Tages-Simulation ausführen.
                // Die Ankünfte enden nach SIMULATIONSDAUER, aber einzelne Prozesse können
                // noch nachlaufen. Ein fester Nachlaufpuffer verhindert, dass der Tag bei
                // offenen Warteschlangen oder extrem langen Zufallsdauern unbegrenzt läuft.
                env.Run(maximaleTagesdauer);
            }
        }

        private static TimeSpan BerechneMaximaleTagesdauer()
        {
            const double nachlaufPufferMinuten = 180.0;
            return TimeSpan.FromMinutes(SimulationKonfiguration.SIMULATIONSDAUER + nachlaufPufferMinuten);
        }

        private sealed record AktiverPatientPrognose(
            int PatientId,
            double ZeitpunktMinuten,
            double PrognoseRestMinuten,
            double RestRezeptionMinuten,
            double RestSchwesterMinuten,
            double RestArztMinuten);
    }
}
