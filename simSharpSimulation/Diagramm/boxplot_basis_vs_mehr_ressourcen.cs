using System.Collections.Generic;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 17 - Boxplot-Vergleich Basis-Szenario gegen mehr Ressourcen.
    internal static partial class GenerateDiagramme
    {
        private static void ErzeugeBoxplotBasisVsMehrRessourcen(
            IReadOnlyList<string> traceData,
            IReadOnlyList<double> rezeptionsWartezeiten,
            IReadOnlyList<double> schwesternWartezeiten,
            IReadOnlyList<double> arztWartezeiten,
            int behandeltePatientenGesamt)
        {
            RessourcenSzenario basis = new("Basis 1R/1S/2A", 1, 1, 2);
            RessourcenSzenario mehr = new("Mehr 1R/2S/3A", 1, 2, 3);

            SzenarioBoxplotDaten basisDaten = ErzeugeBoxplotDatenAusVorhandenerSimulation(
                basis,
                traceData,
                rezeptionsWartezeiten,
                schwesternWartezeiten,
                arztWartezeiten,
                behandeltePatientenGesamt);
            SzenarioBoxplotDaten mehrDaten = SimuliereBoxplotSzenario(mehr);

            ErzeugeSzenarioBoxplotDiagramm(
                "Diagramm 17: Boxplot Basis-Szenario vs. mehr Ressourcen",
                "Normale Ressourcen: 1 Rezeption, 1 Schwester, 2 Aerzte | mehr Ressourcen: 1 Rezeption, 2 Schwestern, 3 Aerzte",
                basisDaten,
                mehrDaten,
                "boxplot_basis_vs_mehr_ressourcen.png",
                17);
        }
    }
}
