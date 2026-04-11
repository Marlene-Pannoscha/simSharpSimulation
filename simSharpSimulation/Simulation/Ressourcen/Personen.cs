namespace simSharpSimulation
{
    /// <summary>
    /// Basis-Konfigurationsklasse für alle Personen-Ressourcen in der Klinik.
    /// Definiert die gemeinsamen Eigenschaften für Ärzte, Schwestern und Patienten.
    /// </summary>
    internal abstract class PersonenKonfiguration
    {
        /// <summary>
        /// Anzahl der verfügbaren Ressourcen (Personen).
        /// </summary>
        public abstract int Anzahl { get; }

        /// <summary>
        /// Durchschnittliche Servicezeit in Minuten.
        /// </summary>
        public abstract double MittlereServicezeit { get; }

        /// <summary>
        /// Beschreibung der Ressource.
        /// </summary>
        public abstract string Beschreibung { get; }
    }
}
