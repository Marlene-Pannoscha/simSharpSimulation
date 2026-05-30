using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik fuer die "Arzt-Phase" im Simulationsprozess.
     * Sie ist als statische Klasse implementiert, da sie keine eigenen Zustandsdaten haelt
     * und alle notwendigen Informationen ueber Parameter erhaelt.
     */
    public static class ArztPhase
    {
        private static int GetPriority(PatientenTyp typ)
        {
            // Kuerzere Behandlungen erhalten eine hoehere Prioritaet, damit der Arzt
            // kurze Faelle schneller abarbeiten kann und die Warteschlange beweglich bleibt.
            return typ switch
            {
                PatientenTyp.Kurz => 3,
                PatientenTyp.Mittel => 2,
                PatientenTyp.Lang => 1,
                _ => 1
            };
        }

        /*
         * Beschreibt den Prozess, den ein Patient beim Arzt durchlaeuft.
         * Von der Ankunft in der Warteschlange bis zum Ende der Behandlung.
         *
         * env: Die Simulationsumgebung, die Zeit und Ereignisse verwaltet.
         * patientId: Die eindeutige ID des Patienten fuer das Logging.
         * arzt: Die Arzt-Ressource, die belegt werden muss.
         * ankunftszeit: Der Zeitpunkt des Klinik-Eintritts zur Berechnung der Wartezeit.
         * rnd: Der Zufallsgenerator fuer die Behandlungsdauer.
         * daten: Das Objekt zum Sammeln und Speichern von Simulationsdaten.
         * returns: Eine Sequenz von Simulationsereignissen, die den Ablauf steuern.
         */
        public static IEnumerable<Event> DurchlaufeArzt(
            Simulation env,
            int patientId,
            BeweglicherArztPool aerzte,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            Random rnd,
            SimulationsDaten daten,
            BehandlungsPhaseErgebnis ergebnis)
        {
            // Aktuelle Simulationszeit fuer das Logging holen.
            double limitMinuten = 55.0;
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt A2: Vor dem eigentlichen Warten wird geprueft, ob der Arztbereich
            // fuer diesen Tag ueberhaupt noch offen ist.
            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichArztWartenAb(env, daten, patientId, arztId, interneBewegungsdauer, wegenFeierabend: true, ergebnis))
                    yield return ev;
                yield break;
            }

            double deadlineMinuten = Math.Min(schichtEndeMinuten, nowMinutes + limitMinuten);

            // Schritt A2: Einen Arzt anfordern.
            // 'using' sorgt dafür, dass die Ressource (der Arzt) nach der Behandlung
            // automatisch wieder für den nächsten Patienten freigegeben wird.
            using (var req = arzt.Request(priority: GetPriority(patientenTyp)))
            {
                // Der Prozess pausiert hier (yield return), bis ein Arzt verfügbar ist.
                yield return req;

                // Schritt A3: Arzt ist frei, die Behandlung beginnt.
                int arztId = aerzte.UebernehmeFreienMitarbeiter();
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                // Falls die Schicht exakt zwischen Freigabe und Zuweisung endet,
                // wird der Patient noch vor Betreten des Arztzimmers abgewiesen.
                if (nowMinutes > schichtEndeMinuten)
                {
                    foreach (Event ev in BrichArztWartenAb(env, daten, patientId, arztId, interneBewegungsdauer, wegenFeierabend: true, ergebnis))
                        yield return ev;
                    yield break;
                }

                // HIT: Ab hier hat der Patient den Arzt tatsaechlich erreicht.
                daten.ErfasseArztBehandlungBegonnen(env.StartDate);

                // Patient verlaesst das Wartezimmer, sobald der Arzt frei ist.
                daten.LogEvent(nowMinutes, "verlaesst_wartezimmer_fuer_arzt", patientId);

                // Weg zum Arzt (interne Bewegung).
                daten.LogEvent(nowMinutes, "geht_zum_arzt", patientId);
                yield return env.Timeout(interneBewegungsdauer);

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "betritt_arztzimmer", patientId);

                // Arztbehandlung beginnt.
                daten.LogEvent(nowMinutes, "startet_arzt_behandlung", patientId, arztId: arztId);

                // Wartezeit erfassen.
                double wartezeitArzt = nowMinutes - ankunftszeit;
                daten.ErfasseArztWartezeit(wartezeitArzt, hatTermin, patientenTyp);

                // Behandlungsdauer nach Patienten-Typ.
                var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
                double mittlereDauer = typInfo.BehandlungszeitArzt;
                double variationskoeffizient = typInfo.VariationskoeffizientArzt;

                // Log-Normalverteilung fuer realistischere Zeiten:
                // viele Werte liegen nahe am Mittelwert, einige dauern deutlich laenger.
                // Umrechnung von Mittelwert und Variationskoeffizient in die Parameter mu und sigma der Lognormalverteilung
                double varianz = Math.Pow(variationskoeffizient * mittlereDauer, 2);
                double mu = Math.Log(mittlereDauer) - 0.5 * Math.Log(1 + varianz / Math.Pow(mittlereDauer, 2));
                double sigma = Math.Sqrt(Math.Log(1 + varianz / Math.Pow(mittlereDauer, 2)));

                double dauer = MathNet.Numerics.Distributions.LogNormal.Sample(rnd, mu, sigma);
                daten.ErfasseArztBehandlungszeit(dauer, hatTermin, patientenTyp);

                yield return env.Timeout(TimeSpan.FromMinutes(dauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_arzt_behandlung", patientId, arztId: arztId);
                aerzte.GibMitarbeiterZurueck(arztId);
            }
        }

        private static IEnumerable<Event> BrichArztWartenAb(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int arztId,
            TimeSpan interneBewegungsdauer,
            bool wegenFeierabend,
            BehandlungsPhaseErgebnis ergebnis)
        {
            // Diese Hilfsmethode haelt den Abbruchpfad an einer Stelle zusammen,
            // damit Wartezeit- und Feierabend-Abbrueche identisch behandelt werden.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            if (wegenFeierabend)
            {
                daten.ErfasseArztAbbruchFeierabend(env.StartDate);
                daten.LogEvent(nowMinutes, "bricht_ab_wegen_feierabend_arzt", patientId, arztId: arztId);
            }
            else
            {
                daten.ErfasseArztAbbruchWartezeit(env.StartDate);
                daten.LogEvent(nowMinutes, "bricht_ab_und_verlaesst_klinik_wegen_wartezeit", patientId);
            }

            // Nach dem Abbruch verlaesst der Patient die Klinik ueber den normalen Ausgangspfad.
            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
