using System;
using System.Collections.Generic;
using System.Linq;

namespace simSharpSimulation
{
    public sealed class PrognoseRessourcenStatus
    {
        private readonly int kapazitaet;
        private readonly Dictionary<int, double> aktiveEndzeiten = new();
        private readonly List<WartenderPatient> wartendePatienten = new();
        private long naechsteReihenfolge;

        public PrognoseRessourcenStatus(int kapazitaet)
        {
            this.kapazitaet = Math.Max(1, kapazitaet);
        }

        public void RegistriereWartend(int patientId, double bearbeitungsdauerMinuten, int prioritaet = 0)
        {
            RegistriereGeplanteAnkunft(patientId, 0.0, bearbeitungsdauerMinuten, prioritaet);
        }

        public void RegistriereGeplanteAnkunft(
            int patientId,
            double bereitAbMinuten,
            double bearbeitungsdauerMinuten,
            int prioritaet = 0)
        {
            EntferneWartend(patientId);
            wartendePatienten.Add(new WartenderPatient(
                patientId,
                Math.Max(0.0, bereitAbMinuten),
                Math.Max(0.0, bearbeitungsdauerMinuten),
                prioritaet,
                naechsteReihenfolge++));
        }

        public void StarteBehandlung(int patientId, double jetztMinuten, double bearbeitungsdauerMinuten)
        {
            EntferneWartend(patientId);
            aktiveEndzeiten[patientId] = jetztMinuten + Math.Max(0.0, bearbeitungsdauerMinuten);
        }

        public void BeendeBehandlung(int patientId)
        {
            aktiveEndzeiten.Remove(patientId);
        }

        public void EntfernePatient(int patientId)
        {
            EntferneWartend(patientId);
            aktiveEndzeiten.Remove(patientId);
        }

        public double SchaetzeWartezeit(
            double jetztMinuten,
            double ankunftszeitMinuten,
            int patientId,
            double eigeneBearbeitungsdauerMinuten,
            int prioritaet = 0)
        {
            BereinigeAbgeschlossene(jetztMinuten);

            List<WartenderPatient> prognoseWartende = wartendePatienten
                .Where(w => w.PatientId != patientId)
                .ToList();
            WartenderPatient? eigenerEintrag = wartendePatienten.FirstOrDefault(w => w.PatientId == patientId);
            prognoseWartende.Add(new WartenderPatient(
                patientId,
                ankunftszeitMinuten,
                Math.Max(0.0, eigeneBearbeitungsdauerMinuten),
                prioritaet,
                eigenerEintrag?.Reihenfolge ?? long.MaxValue));

            List<double> serverFreiAb = aktiveEndzeiten.Values
                .Select(endzeit => Math.Max(jetztMinuten, endzeit))
                .ToList();

            while (serverFreiAb.Count < kapazitaet)
            {
                serverFreiAb.Add(jetztMinuten);
            }

            while (prognoseWartende.Count > 0)
            {
                int index = IndexDesFruehestenServers(serverFreiAb);
                double serverZeit = serverFreiAb[index];
                WartenderPatient wartender = WaehleNaechstenPatienten(prognoseWartende, serverZeit);
                double startzeit = Math.Max(serverZeit, wartender.BereitAbMinuten);

                if (wartender.PatientId == patientId)
                {
                    return Math.Max(0.0, startzeit - ankunftszeitMinuten);
                }

                prognoseWartende.Remove(wartender);
                serverFreiAb[index] = startzeit + wartender.BearbeitungsdauerMinuten;
            }

            return 0.0;
        }

        private void EntferneWartend(int patientId)
        {
            wartendePatienten.RemoveAll(w => w.PatientId == patientId);
        }

        private void BereinigeAbgeschlossene(double jetztMinuten)
        {
            int[] fertig = aktiveEndzeiten
                .Where(e => e.Value <= jetztMinuten + 0.0001)
                .Select(e => e.Key)
                .ToArray();

            foreach (int patientId in fertig)
            {
                aktiveEndzeiten.Remove(patientId);
            }
        }

        private static int IndexDesFruehestenServers(IReadOnlyList<double> serverFreiAb)
        {
            int index = 0;
            double besterWert = serverFreiAb[0];
            for (int i = 1; i < serverFreiAb.Count; i++)
            {
                if (serverFreiAb[i] < besterWert)
                {
                    index = i;
                    besterWert = serverFreiAb[i];
                }
            }

            return index;
        }

        private static WartenderPatient WaehleNaechstenPatienten(
            IReadOnlyList<WartenderPatient> wartende,
            double serverZeit)
        {
            WartenderPatient[] verfuegbar = wartende
                .Where(w => w.BereitAbMinuten <= serverZeit + 0.0001)
                .ToArray();

            if (verfuegbar.Length == 0)
            {
                double naechsteBereitZeit = wartende.Min(w => w.BereitAbMinuten);
                verfuegbar = wartende
                    .Where(w => Math.Abs(w.BereitAbMinuten - naechsteBereitZeit) <= 0.0001)
                    .ToArray();
            }

            return verfuegbar
                .OrderBy(w => w.Prioritaet)
                .ThenBy(w => w.Reihenfolge)
                .First();
        }

        private sealed record WartenderPatient(
            int PatientId,
            double BereitAbMinuten,
            double BearbeitungsdauerMinuten,
            int Prioritaet,
            long Reihenfolge);
    }
}
