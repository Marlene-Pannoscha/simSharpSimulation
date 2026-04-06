
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimSharp;

namespace simSharpSimulation
{
    class Program
    {
        // --- 1. PARAMETER ---
        const int RANDOM_SEED = 42; // gleiche Zufallswerte bei jedem Lauf.
        const double MITTLERE_BEHANDLUNGSZEIT = 5.0; // durchschnittliche Dauer, wie lange ein Patient beim Arzt ist.
        const double SIMULATIONSDAUER = 480.0; // 8 Stunden Simulation für stabilere Statistik
        const int ANZAHL_AERZTE = 3;

        // NEU: Parameter für die Inversionsmethode
        const int ANZAHL_PATIENTEN_TAG = 100; // Wie viele Patienten erwarten wir insgesamt heute?
        const double ERWARTUNGSWERT = 180.0; // Wann ist am meisten los? (Minute 240 = nach 2 Stunden / hier 180)
        const double STANDARDABWEICHUNG = 80.0; // Wie breit ist die Glockenkurve? (Standardabweichung in Minuten)

        static Random rnd = new Random(RANDOM_SEED); // initialisiert den Zufallsgenerator mit einem festen Startwert

        // --- 2. DATENSAMMLUNG & DATEI-EXPORT ---
        static List<string> trace_data = new List<string>(); // Liste, um Ereignisse während der Sim zu sammeln
        static List<double> wartezeiten = new List<double>(); // Liste, um die Wartezeiten der Patienten zu sammeln
        static List<double> echte_ankunftszeiten = new List<double>(); // NEU: Liste für die tatsächlichen Ankunftszeiten

        // Speichert ein Ereignis in unserer Trace-Liste.
        static void LogEvent(double zeit, string eventTyp, int patientId)
        {
            string timeStr = zeit.ToString("000.00", System.Globalization.CultureInfo.InvariantCulture); // Formatierte Zeit
            string logEntry = $"{timeStr};{eventTyp};{patientId}";
            trace_data.Add(logEntry);
        }

        // --- 3. DER PATIENTEN-PROZESS (Der logische Weg) ---
        static IEnumerable<Event> Patient(Simulation env, int patientId, Resource arzt)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes; // now: Abruf der aktuellen Simulationszeit
            
            // EREIGNIS 1: Patient betritt die Klinik
            LogEvent(nowMinutes, "betritt_klinik", patientId);
            double ankunftszeit = nowMinutes;
            echte_ankunftszeiten.Add(ankunftszeit); // NEU: Speichere Ankunftszeit für das Diagramm

            using (var anfrage = arzt.Request())
            {
                // EREIGNIS 2: Patient geht zur Warteschlange
                LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zu_warteschlange", patientId);
                yield return anfrage; // pausiert den Prozess, bis ein Arzt frei wird

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double wartezeit = nowMinutes - ankunftszeit;
                wartezeiten.Add(wartezeit);
                
                // EREIGNIS 3: Patient verlässt die Warteschlange
                LogEvent(nowMinutes, "verlaesst_warteschlange", patientId);
                // EREIGNIS 4: Patient geht zum Arzt
                LogEvent(nowMinutes, "geht_zu_arzt", patientId);

                double behandlungsdauer = MathNet.Numerics.Distributions.Exponential.Sample(rnd, 1.0 / MITTLERE_BEHANDLUNGSZEIT);
                yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer)); // Zeitspanne "pausiert"
            }
            // Die 'using'-Anweisung ist hier zu Ende. Der Arzt wird automatisch freigegeben.

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            // EREIGNIS 5: Patient verlässt den Arzt
            LogEvent(nowMinutes, "verlaesst_arzt", patientId);
            // EREIGNIS 6: Patient verlässt die Klinik
            LogEvent(nowMinutes, "verlaesst_klinik", patientId);
        }

        // --- 4. DIE QUELLE ---
        static IEnumerable<Event> PatientenGenerator(Simulation env, Resource arzt)
        {
            var ankunftszeiten = new List<double>();

            // Schritt 1: Wir generieren die Ankunftszeiten nach der Normalverteilung
            for (int i = 0; i < ANZAHL_PATIENTEN_TAG; i++)
            {
                double z = MathNet.Numerics.Distributions.Normal.Sample(rnd, ERWARTUNGSWERT, STANDARDABWEICHUNG);
                
                // Schritt 2: Zeiten filtern, die außerhalb der Öffnungszeiten liegen (<0 oder >480)
                if (z >= 0 && z <= SIMULATIONSDAUER)
                {
                    ankunftszeiten.Add(z);
                }
            }

            // Schritt 3: Zeiten sortieren, wichtig für die Simulation.
            ankunftszeiten.Sort();

            int patientCount = 1;
            // Schritt 4: Die Simulation springt nun von Ereignis zu Ereignis
            foreach (double ankunftszeit in ankunftszeiten)
            {
                // Berechne, wie lange die Simulation "warten" muss, bis dieser Patient ankommt
                double warteBisAnkunft = ankunftszeit - (env.Now - env.StartDate).TotalMinutes;
                if (warteBisAnkunft > 0)
                {
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));
                }

                // Wenn die Zeit erreicht ist, wird der Patient in die Klinik geschickt
                env.Process(Patient(env, patientCount, arzt));
                patientCount++;
            }
        }

        // --- 5. HAUPTPROGRAMM (Setup & Start) ---
        static void Main(string[] args)
        {
            Console.WriteLine("--- Start der SimSharp-Klinik-Simulation ---");

            // rng setup & env setup als Uhr der Simulation
            var env = new Simulation(new DateTime(2000, 1, 1));
            var arzt = new Resource(env, capacity: ANZAHL_AERZTE);

            // startet den Patienten-Generator als Prozess in der Simulationsumgebung
            env.Process(PatientenGenerator(env, arzt));
            env.Run(TimeSpan.FromMinutes(SIMULATIONSDAUER));

            Console.WriteLine("--- Ende der SimSharp-Simulation. ---");

            // --- 6. VISUALISIERUNG (Diagramme) ---
            KlinikDiagramme.GeneriereDiagramme(
                echte_ankunftszeiten,
                wartezeiten,
                SIMULATIONSDAUER,
                ERWARTUNGSWERT,
                STANDARDABWEICHUNG,
                ANZAHL_AERZTE);

            // --- 7. EXPORT IN TEXTDATEI ---
            Console.WriteLine("--- Speichere Trace-File: klinik_trace.txt ---");
            File.WriteAllLines("klinik_trace.txt", trace_data);
            Console.WriteLine("--- Trace-File erfolgreich gespeichert. ---");
            
            double avgWartezeit = wartezeiten.Count > 0 ? wartezeiten.Average() : 0;
            Console.WriteLine($"Simulation beendet. {echte_ankunftszeiten.Count} Patienten empfangen.");
            Console.WriteLine($"Durchschnittliche Wartezeit: {avgWartezeit:F2} Minuten");
        }
    }
}
