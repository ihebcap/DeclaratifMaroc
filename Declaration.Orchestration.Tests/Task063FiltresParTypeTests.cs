using System.Collections.Generic;
using Xunit;
using Declaration.Application.Entities;

namespace Declaration.Orchestration.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-063 — Parité de filtre par type + fin du « filtre menteur » (mode/declare).
    //
    // Prouve, 100 % in-process (aucune connexion), la NORMALISATION multi-sélection des
    // filtres booléens (source unique RapprochementFilter.ToFlags), que le SQL applique
    // ensuite via « CASE ... IN @flags » — à l'identique dans la liste ET le COUNT.
    //
    // Cœur du bug corrigé : avant, le front ne transmettait que la 1re case cochée
    // (declare[0]) → cocher Oui + Non ne renvoyait que Oui. Désormais Oui + Non ⇒ {1,0}
    // ⇒ IN (0,1) ⇒ l'ensemble. La preuve DB réelle est dans VERIFY/TASK-063_verify.md.
    // ═══════════════════════════════════════════════════════════════════════════════
    public class Task063FiltresParTypeTests
    {
        [Fact]
        public void ToFlags_OuiEtNon_DonneUnionReelle_0Et1()
        {
            // Cocher « Oui » ET « Non » : union réelle des deux états (plus de valeur ignorée).
            var flags = RapprochementFilter.ToFlags(new List<bool> { true, false });

            Assert.Equal(2, flags.Count);
            Assert.Contains(1, flags); // Oui
            Assert.Contains(0, flags); // Non
        }

        [Theory]
        [InlineData(true, 1)]
        [InlineData(false, 0)]
        public void ToFlags_UneSeuleValeur_MappeVersLeDrapeauAttendu(bool coche, int attendu)
        {
            var flags = RapprochementFilter.ToFlags(new List<bool> { coche });

            Assert.Single(flags);
            Assert.Equal(attendu, flags[0]);
        }

        [Fact]
        public void ToFlags_Doublons_SontDedupliques()
        {
            // Deux cases « Oui » (théorique) → un seul drapeau, IN (1) reste correct.
            var flags = RapprochementFilter.ToFlags(new List<bool> { true, true });

            Assert.Single(flags);
            Assert.Equal(1, flags[0]);
        }

        [Fact]
        public void ToFlags_Vide_OuNull_NeContraintPas()
        {
            // Liste vide ou nulle ⇒ aucun drapeau ⇒ garde @hasX = 0 ⇒ pas de filtre (NULL-safe).
            Assert.Empty(RapprochementFilter.ToFlags(null));
            Assert.Empty(RapprochementFilter.ToFlags(new List<bool>()));
        }

        [Fact]
        public void Filtre_PlagesNonRenseignees_RestentNulles_NullSafe()
        {
            // Une plage sans borne ne doit poser AUCUNE contrainte : les propriétés restent nulles,
            // et le SQL « @xMin IS NULL OR col >= @xMin » se court-circuite.
            var f = new RapprochementFilter();

            Assert.Null(f.MontantMin);
            Assert.Null(f.MontantMax);
            Assert.Null(f.ResteMin);
            Assert.Null(f.ResteMax);
            Assert.Null(f.NbFacturesMin);
            Assert.Null(f.NbFacturesMax);
            Assert.Null(f.DateRappMin);
            Assert.Null(f.DateRappMax);
            Assert.Null(f.EcheanceMin);
            Assert.Null(f.EcheanceMax);
        }
    }
}
