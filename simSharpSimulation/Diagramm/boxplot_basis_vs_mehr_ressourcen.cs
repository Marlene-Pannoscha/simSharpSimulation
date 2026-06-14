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
            RessourcenSzenario basis = new("Basis", 1, 1, 2, 2, 1);
            RessourcenSzenario mehr = new("Mehr", 1, 2, 3, 3, 2);

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
                "Basis: 1 Rezeption, 1 Schwester, 2 Aerzte, 2 Arztzimmer, 1 Schwesterzimmer | mehr: 2 Schwestern, 3 Aerzte, passende Zimmer",
                basisDaten,
                mehrDaten,
                "boxplot_basis_vs_mehr_ressourcen.png",
                17);
        }
    }
}
