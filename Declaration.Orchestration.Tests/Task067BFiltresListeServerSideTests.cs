using System.Collections.Generic;
using Xunit;
using Declaration.Application.Entities;

namespace Declaration.Orchestration.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-067B — Filtres « valeurs disponibles » (Excel-like), back+front server-side
    // (RapprochementInterrogation / FactureInterrogation / DomainGrid).
    //
    // Prouve, 100 % in-process (aucune connexion DB), la logique PURE réutilisée par
    // DeclarationRepository pour construire les WHERE des endpoints `distincts` et des
    // filtres liste étendus :
    //   - RapprochementFilter : les nouveaux champs identifiants (Numeros, NumerosExtrait,
    //     Banques) sont bien des listes (multi-sélection), null par défaut (pas de contrainte) ;
    //   - ReglementRapprochementRow.EcTypesDepuisLibelle : reverse EXACT de LibelleEcType,
    //     utilisé par DomainGrid pour traduire un libellé d'origine coché dans ExcelFilter
    //     vers le(s) EC_Type SQL — sans dupliquer la règle métier.
    //
    // La preuve DB réelle (WHERE identique liste/COUNT/distincts, compteur exact, coût
    // SELECT DISTINCT) est dans VERIFY/TASK-067B_verify.md.
    // ═══════════════════════════════════════════════════════════════════════════════
    public class Task067BFiltresListeServerSideTests
    {
        [Fact]
        public void RapprochementFilter_ColonnesIdentifiantes_SontNullesParDefaut_PasDeContrainte()
        {
            // Liste vide/nulle (aucune case cochée dans ExcelFilter) ⇒ aucune contrainte ajoutée
            // (le SQL du repository se court-circuite via le garde @hasX = 0, cf. RapprochementFromWhere).
            var f = new RapprochementFilter();

            Assert.Null(f.Numeros);
            Assert.Null(f.NumerosExtrait);
            Assert.Null(f.Banques);
            // Tiers reste un texte libre (LIKE), pas une liste — décision documentée (cardinalité
            // non bornée, cf. VERIFY/TASK-067B_verify.md).
            Assert.Null(f.Tiers);
        }

        [Fact]
        public void RapprochementFilter_ColonnesIdentifiantes_MultiSelectionReelle_ToutesRetenues()
        {
            // Cocher plusieurs valeurs dans ExcelFilter (mode list) doit produire la liste COMPLÈTE
            // (pas de troncature à la 1re valeur — le bug corrigé par TASK-063 pour mode/declare).
            var f = new RapprochementFilter
            {
                Numeros = new[] { "REG-001", "REG-002" },
                NumerosExtrait = new[] { "EXT-1" },
                Banques = new[] { "BQ1", "BQ2", "BQ3" }
            };

            Assert.Equal(2, f.Numeros!.Count);
            Assert.Contains("REG-001", f.Numeros);
            Assert.Contains("REG-002", f.Numeros);
            Assert.Equal(3, f.Banques!.Count);
        }

        [Theory]
        [InlineData("Sage", 0)]
        [InlineData("FGR", 111)]
        [InlineData("SoldeInitial", 4)]
        public void EcTypesDepuisLibelle_LibelleConnu_DonneUnSeulEcType(string libelle, int ecTypeAttendu)
        {
            var ecTypes = ReglementRapprochementRow.EcTypesDepuisLibelle(libelle);

            Assert.Single(ecTypes);
            Assert.Equal(ecTypeAttendu, ecTypes[0]);
        }

        [Fact]
        public void EcTypesDepuisLibelle_AutreParenthese_ExtraitLeCodeExact()
        {
            // « Autre (n) » encode directement le EC_Type dans le libellé (cf. LibelleEcType) :
            // le reverse doit retrouver EXACTEMENT le même code, sans l'inventer.
            var ecTypes = ReglementRapprochementRow.EcTypesDepuisLibelle("Autre (77)");

            Assert.Single(ecTypes);
            Assert.Equal(77, ecTypes[0]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("LibelleInconnu")]
        public void EcTypesDepuisLibelle_LibelleInconnuOuVide_NeContraintPas(string? libelle)
        {
            // Aucune valeur inventée : un libellé non reconnu ne doit jamais se traduire en un
            // EC_Type arbitraire — liste vide (pas de contrainte ajoutée au WHERE).
            Assert.Empty(ReglementRapprochementRow.EcTypesDepuisLibelle(libelle));
        }

        [Fact]
        public void LibelleEcType_PuisEcTypesDepuisLibelle_EstUnAllerRetourStable()
        {
            // Aller-retour : LibelleEcType(n) → EcTypesDepuisLibelle(label) doit retrouver n,
            // pour les 3 codes métier connus ET un code arbitraire (branche « Autre »).
            foreach (var ecType in new[] { 0, 111, 4, 999 })
            {
                var libelle = ReglementRapprochementRow.LibelleEcType(ecType);
                var retour = ReglementRapprochementRow.EcTypesDepuisLibelle(libelle);

                Assert.Single(retour);
                Assert.Equal(ecType, retour[0]);
            }
        }

        [Fact]
        public void ReglementRapprochementDistincts_ColonnesTask067B_VidesParDefaut()
        {
            // Contrat de réponse : les nouvelles listes existent et sont vides par défaut
            // (jamais null — évite un crash front sur .map()).
            var d = new ReglementRapprochementDistincts();

            Assert.NotNull(d.NumerosReglement);
            Assert.NotNull(d.NumerosExtrait);
            Assert.NotNull(d.Banques);
            Assert.Empty(d.NumerosReglement);
        }

        [Fact]
        public void FactureInterrogationDistincts_ColonnesTask067B_VidesParDefaut()
        {
            var d = new FactureInterrogationDistincts();

            Assert.NotNull(d.Numeros);
            Assert.NotNull(d.References);
            Assert.Empty(d.Numeros);
            Assert.Empty(d.References);
        }

        [Fact]
        public void FactureInterrogationDistincts_Task168_TronqueFauxParDefaut()
        {
            // Jamais de troncature annoncée à tort avant tout calcul réel (le repository est seul
            // responsable de positionner ces booléens à true, cf. DeclarationRepository).
            var d = new FactureInterrogationDistincts();

            Assert.False(d.NumerosTronque);
            Assert.False(d.ReferencesTronque);
        }
    }
}
