using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;
using SimSharp;

namespace simSharpSimulation
{
    // Diese Datei enthält gemeinsam genutzte Hilfsmethoden des Patientenprozesses.
    // Sie ist aktuell wichtig, weil Ressourcenwahl, PatientenTyp-Wahl und Belegungsprüfung
    // aus dem Patientenablauf hierher ausgelagert wurden.
    internal sealed partial class PatientenProzess
    {
        // Hilfsmethode zum Auswählen einer Ressource
        // Strategie:
        // - wenn sofort freie Ressourcen existieren, wähle zufällig unter den freien
        // - sonst wähle zufällig aus allen Ressourcen und warte dort
        // Dadurch vermeiden wir eine starre Bevorzugung immer derselben Ressource.
        private (PriorityResource res, int id) WaehleRessource(List<PriorityResource> ressourcen)
        {
            // Freie Ressourcen werden bevorzugt, sonst wartet der Patient bei einer zufaelligen Ressource.
            var freieRessourcen = ressourcen
                .Select((res, index) => (res, index))
                .Where(eintrag => eintrag.res.Remaining > 0)
                .ToList();

            if (freieRessourcen.Count > 0)
            {
                var eintrag = freieRessourcen[rnd.Next(freieRessourcen.Count)];
                return (eintrag.res, eintrag.index + 1);
            }

            int index = rnd.Next(ressourcen.Count);
            return (ressourcen[index], index + 1);
        }

        // Phase P-C: Delegation an ausgelagerte Phasenklassen.
        // Schritt P8: Interne Hilfsmethode, um Patienten-Typ zu wählen.
        // Es wird kumulativ über die konfigurierten Wahrscheinlichkeiten gelaufen,
        // bis die Zufallszahl in eines der Gewichtsintervalle fällt.
        private static PatientenTyp WaehlePatientenTyp(System.Random rnd)
        {
            double rand = rnd.NextDouble();
            double cumulative = 0.0;
            foreach (var (typ, wahrsch, _, _, _, _, _) in PatientenKonfiguration.TYPEN_VERTEILUNG)
            {
                cumulative += wahrsch;
                if (rand <= cumulative)
                    return typ;
            }
            return PatientenTyp.Mittel; // Fallback
        }

        // Schritt P9: Interne Hilfsmethode, um aktuelle Belegung der Ressource zu prüfen.
        // SimSharp exponiert diese Information nicht direkt in der gewünschten Form,
        // deshalb lesen wir die internen Users per Reflection aus.
        // Der Rückgabewert ist die Zahl gerade belegter Ressourceninstanzen.
        // Zieht konkrete Behandlungsdauern vorab, damit Prognose und Simulation dieselben
        // Lognormal-Ausreisser verwenden.
        private static double ZieheLogNormalDauer(double mittelwert, double variationskoeffizient, Random rnd)
        {
            double varianz = Math.Pow(variationskoeffizient * mittelwert, 2);
            double mu = Math.Log(mittelwert) - 0.5 * Math.Log(1 + varianz / Math.Pow(mittelwert, 2));
            double sigma = Math.Sqrt(Math.Log(1 + varianz / Math.Pow(mittelwert, 2)));

            return MathNet.Numerics.Distributions.LogNormal.Sample(rnd, mu, sigma);
        }

        private static double ZieheRezeptionsdauer(Random rnd)
        {
            return ZieheLogNormalDauer(
                RezeptionKonfiguration.MITTELREZEPTIONSZEIT,
                RezeptionKonfiguration.VARIATIONSKOEFFIZIENT_REZEPTION,
                rnd);
        }

        private static double ZieheSchwesterBehandlungsdauer(PatientenTyp patientenTyp, Random rnd)
        {
            var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
            return ZieheLogNormalDauer(
                typInfo.BehandlungszeitSchwester,
                typInfo.VariationskoeffizientSchwester,
                rnd);
        }

        private static double ZieheArztBehandlungsdauer(PatientenTyp patientenTyp, Random rnd)
        {
            var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
            return ZieheLogNormalDauer(
                typInfo.BehandlungszeitArzt,
                typInfo.VariationskoeffizientArzt,
                rnd);
        }

        private static double ZieheSchwesterWartezimmerdauer(bool hatTermin, Random rnd)
        {
            double faktor = hatTermin
                ? PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER
                : PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER;

            return MathNet.Numerics.Distributions.Exponential.Sample(
                rnd,
                1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER * faktor));
        }

        private static double ZieheArztWartezimmerdauer(bool hatTermin, Random rnd)
        {
            double faktor = hatTermin
                ? PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT
                : PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT;

            return MathNet.Numerics.Distributions.Exponential.Sample(
                rnd,
                1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_ARZT * faktor));
        }

        private static int ErmittleAktiveNutzer<T>(List<T> ressourcen)
        {
            return ressourcen.Sum(r => {
                var usersProperty = r?.GetType().GetProperty("Users", BindingFlags.NonPublic | BindingFlags.Instance);
                var usersCollection = usersProperty?.GetValue(r) as IReadOnlyCollection<Request>;
                return usersCollection?.Count ?? 0;
            });
        }
    }
}
