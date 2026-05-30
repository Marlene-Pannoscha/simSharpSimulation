namespace simSharpSimulation
{
    public sealed class BehandlungsPhaseErgebnis
    {
        public bool PatientHatKlinikVerlassen { get; private set; }

        public void MarkiereKlinikVerlassen()
        {
            PatientHatKlinikVerlassen = true;
        }
    }
}
