namespace simSharpSimulation
{
    // Iterator-Phasen melden hier einen Klinikabgang, damit der Hauptprozess keine weitere Station startet.
    public sealed class BehandlungsPhaseErgebnis
    {
        public bool PatientHatKlinikVerlassen { get; private set; }

        public void MarkiereKlinikVerlassen()
        {
            PatientHatKlinikVerlassen = true;
        }
    }
}
