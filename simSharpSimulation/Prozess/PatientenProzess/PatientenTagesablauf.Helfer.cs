using System;
using System.Collections.Generic;
using SimSharp;
using static simSharpSimulation.SimulationsHelfer;

namespace simSharpSimulation
{
    internal sealed partial class PatientenTagesablauf
    {
        private IEnumerable<Event> VerlasseKlinikNachAbbruch(
            Simulation env,
            int patientId,
            TimeSpan wegZumAusgang)
        {
            foreach (Event ev in VerlasseKlinik(env, daten, patientId, wegZumAusgang))
                yield return ev;
            EntferneAktivePatientenPrognose(patientId);
        }

    }
}
