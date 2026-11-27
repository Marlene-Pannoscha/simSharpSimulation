using System;
using System.Threading.Tasks;
using SimSharp;

namespace simSharpSimulation
{
    internal sealed class PatientenProzess
    {
        private static readonly DateTime ErsterSimulationstag = new(2000, 1, 3);
        private const int PatientenIdAbstandProTag = 10_000;
        private readonly Random rnd;
        private readonly SimulationsDaten daten;

        public PatientenProzess(int randomSeed, SimulationsDaten daten)
        {
            rnd = new Random(randomSeed);
            this.daten = daten;
        }

        public void FuehreAus(int arbeitstage = 30)
        {
            FuehreAusAsync(arbeitstage, (env, dauer) =>
            {
                env.Run(dauer);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }

        internal async Task FuehreAusAsync(int arbeitstage, Func<Simulation, TimeSpan, Task> fuehreTagAus)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(arbeitstage);
            ArgumentNullException.ThrowIfNull(fuehreTagAus);

            var maximaleTagesdauer = TimeSpan.FromMinutes(
                SimulationKonfiguration.SIMULATIONSDAUER + SimulationKonfiguration.NACHLAUF_MINUTEN);

            for (int tag = 0; tag < arbeitstage; tag++)
            {
                var env = new Simulation(ErsterSimulationstag.AddDays(tag));
                var raeume = KonfigurationJsonExport.Finanzen.Fixkosten;
                var ressourcen = new Tagesressourcen(env,
                    ArztKonfiguration.ANZAHL_AERZTE,
                    SchwesterKonfiguration.ANZAHL_SCHWESTERN,
                    RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN,
                    raeume.AnzahlBehandlungsraeumeArzt,
                    raeume.AnzahlBehandlungsraeumeSchwester);
                var ablauf = new PatientenTagesablauf(rnd, daten, ressourcen);
                env.Process(ablauf.Aufnahme.PlaneAufnahmeprognose(env));
                int erstePatientId = (tag * PatientenIdAbstandProTag) + 1;
                env.Process(PatientenGenerator.Generiere(env, rnd, daten, erstePatientId,
                    patientId => ablauf.Patient(env, patientId, ressourcen)));

                await fuehreTagAus(env, maximaleTagesdauer);
            }
        }
    }
}
