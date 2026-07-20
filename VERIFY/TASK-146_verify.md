# VERIFY — TASK-146

Date: 2026-07-20
Agent: worker nocturne (Claude Code)

## BUILD

- Status: OK
- Erreurs: aucune (`dotnet build Declaration.API/Declaration.API.csproj` → 0 erreur, 10
  avertissements préexistants nullabilité, non liés).

## FICHIERS MODIFIÉS

- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` — `BuildLigneFilterWhere` :
  ajout de la clé JSON `ecId` (`AND EC_Id IN @EcIds`), paramétrée, insérée avant la branche
  `source` existante. Colonne `EC_Id` déjà présente sur `DM_LGTVA` (confirmé par lecture des
  `INSERT`/`SELECT` existants du même fichier, l.175/202/269/284/298).
- `Declaration.API/Controllers/DeclarationsController.cs` — `GetCheckup` (construction des
  `alertes`) : `filtre` construit en priorité avec `{ ecId: [ligne.EC_Id] }` quand
  `ligne.EC_Id > 0`, repli sur `{ numeroRapprochement }` sinon (compat lignes historiques sans
  `EC_Id`, `EC_Id=0`).

## DIFF RÉSUMÉ

Le drill « Voir lignes » (étape ② Vérifier & Intégrer) transmettait toujours
`{ numeroRapprochement }` comme filtre, quel que soit le code d'alerte — un règlement couvrant
plusieurs factures faisait remonter toutes ses lignes dès qu'une seule était en anomalie. Ajout
d'une clé `ecId` au filtre JSON du repo + construction du filtre côté contrôleur avec `EC_Id` en
priorité (clé précise par ligne), `numeroRapprochement` gardé comme repli pour les lignes sans
`EC_Id` connu (jamais retiré, conforme au garde-fou).

## VALIDATION CHECKLIST

- [x] Build back OK.
- [x] Rejeu réel sur données réelles (`GR_EMA_DISTRIBUTION`) : ligne `FC2501667`/`EC_Id=20650`,
      déclaration `DeclarationId=054a3ed1-00e6-42a0-82ff-fd325bc56ac1`, `NumeroRapprochement=RF26030075`.
      - Ancien filtre (`numeroRapprochement=RF26030075`) : **18 lignes** retournées (confirmé par
        requête directe reproduisant `BuildLigneFilterWhere`) — comportement bugué constaté.
      - Nouveau filtre (`ecId=[20650]`) : **1 seule ligne** retournée (`FC2501667`) — comportement
        corrigé confirmé par requête directe reproduisant le nouveau SQL généré.
      Le contrôleur produira désormais `filtre = { ecId: [20650] }` pour cette alerte puisque
      `ligne.EC_Id = 20650 > 0`.
- [x] Non-régression : le drill « Lignes incohérentes » (`handleDrillIncoherence`, filtre
      `incoherente`) intact — branche JSON non touchée dans `BuildLigneFilterWhere`.
- [x] Non-régression : repli `numeroRapprochement` conservé pour `EC_Id` absent/0 — vérifié par
      lecture du code (`hasEcId` gate explicite) ; **aucune ligne `EC_Id=0`/`NULL` trouvée dans
      `DM_LGTVA` actuellement** (0/0), donc ce chemin de repli n'a pas pu être exercé sur données
      réelles dans cet environnement — couverture par lecture de code uniquement pour ce cas précis.
- [x] Front (`declaration-tva-web/src/VerifierIntegrerPanel.tsx`) : aucun changement nécessaire —
      confirmé par lecture, le composant transmet déjà `a.filtre` tel quel au composant `DomainGrid`
      (mode `drillFiltre`), sans validation de forme sur les clés présentes — la nouvelle clé `ecId`
      est acceptée sans changement de contrat. Build front non relancé pour cette TASK (aucun fichier
      front modifié).

## IMPACTS DÉTECTÉS

- Toute alerte référençant une `LigneCandidate` avec `EC_Id > 0` bénéficie immédiatement du filtre
  précis, sans changement de code supplémentaire (le contrôleur applique la règle uniformément, pas
  seulement au cas `FC2501667` cité en session).

## NOTES

- `LigneCandidateDto.EcId` existe déjà côté DTO API (`Declaration.API/Dtos/LigneCandidateDto.cs`)
  mais n'est pas le type utilisé par `GetCheckup` (`lignes` provient de `_repository.GetLignesAsync`
  → `IEnumerable<LigneCandidate>`, propriété `EC_Id` — pas `EcId`) ; aucune confusion introduite,
  vérifié par build.
