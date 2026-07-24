# TASK-179 Verify — Retrait du niveau « défaut par tiers » de la cascade code activité

## Périmètre livré

Cascade `CodeActiviteResolver.Resoudre` réduite de 4 à 3 niveaux : surcharge manuelle par ligne →
`F_COMPTET.CT_APE` (paramètre `codeActiviteSage`) → `""`. Le niveau 2 (mapping
`P_SOCIETECODEACTIVITETIERS`, TASK-161) est entièrement retiré — plus aucune lecture de cette table
côté GRF, plus aucun paramètre `tiersNumero`/`tiersNom`/`mappingParNumero`/`mappingParNom` sur
`Resoudre`. Ceci supprime la cause du crash 500 en production (`SqlException: Nom de colonne non
valide : 'SCAT_NumeroTiers'`) en éliminant l'appel fautif (`GetMappingCodeActiviteTiersAsync`), pas
en le rendant résilient.

## Fichiers modifiés

- `Declaration.Core/CodeActiviteResolver.cs` — signature réduite à `Resoudre(surchargeManuelle,
  codeActiviteSage)`, doc mise à jour (3 niveaux).
- `Declaration.Application/Services/DeclarationWorkflowService.cs` — suppression de
  `ChargerMappingCodeActiviteTiersAsync` (2 sites d'appel : `ConstruireLignesFigeesAsync` et
  `ReintegrerReglementsLiberesAsync`/ligne ~576) ; `MapLignesCandidates` n'accepte plus les 2
  paramètres de mapping et appelle `Resoudre` avec seulement `surchargeManuelle`/`codeActiviteSage`.
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` — suppression de
  `GetMappingCodeActiviteTiersAsync` (SQL joignant `P_SOCIETECODEACTIVITETIERS`→`P_DECTVAACTIVITE`,
  source directe du crash).
- `Declaration.Application/Interfaces/IDeclarationRepository.cs` — suppression de la signature
  correspondante.
- `Declaration.Application/Entities/WorkflowEntities.cs` — suppression de la classe
  `CodeActiviteTiersMappingRow` (plus aucun consommateur après le retrait ci-dessus).
- `Declaration.Selection/SelectionExpliqueeEvaluator.cs` et
  `Declaration.Selection/SelectionnerAffectationsService.cs` — appels à `Resoudre` adaptés à la
  nouvelle signature (ces deux évaluateurs appelaient déjà `tiersNumero: null, tiersNom: null` —
  confirmé qu'aucun argument retiré n'était réellement utilisé ici, aucun changement de
  comportement).
- `Declaration.Core.Tests/CodeActiviteResolverTests.cs` — réécrit pour la cascade à 3 niveaux (7
  tests → 4 tests, retrait des cas de mapping par tiers, conservation surcharge/CT_APE/vide/blanc).
- `Declaration.Orchestration.Tests/Task161CodeActiviteCascadeTests.cs` — retrait du test
  `MapLignesCandidates_MappingParNumeroTiers_ResoutLeCodeActivite`, adaptation des 2 autres cas
  `MapLignesCandidates` (nouvelle signature sans mapping), retrait de `MappingParNumero`/
  `GetMappingCodeActiviteTiersAsync` du fake dédié, adaptation du test de persistance après figeage
  (la clause « malgré un changement du mapping » n'a plus de sens, le mapping n'existe plus —
  l'assertion de stabilité du `CodeActivite` déjà figé est conservée).
- **Répercussion build non listée dans FILES mais nécessaire** : 15 fakes `IDeclarationRepository`
  dans `Declaration.Orchestration.Tests/*.cs` (Task071/077/080/081/082/094/100/102/103/108/112/
  155/156/160/175) implémentaient un stub `GetMappingCodeActiviteTiersAsync` pour satisfaire
  l'interface — retiré de chacun (sinon `CS0535`, même pattern d'échec que documenté sous TASK-147).
  Aucun de ces fichiers n'avait de logique métier liée au mapping tiers, uniquement le stub
  obligatoire.
- `DONE_DETAIL/TASK-161-code-activite-defaut-tiers-surcharge-ligne.md` et
  `DONE_DETAIL/TASK-161_verify.md` — bandeau de correction ajouté en tête de chaque fichier,
  renvoyant vers TASK-179 et précisant que le niveau 2 décrit dans le reste du document n'existe
  plus. Contenu historique conservé tel quel en dessous (pas de réécriture).

## Non touché (conforme aux contraintes)

- `apbs-gr_winform` : aucun fichier lu ni modifié sous ce chemin dans cette session.
- `P_SOCIETECODEACTIVITETIERS` : aucune requête SQL ne la référence plus nulle part dans le code
  GRF (`Declaration.*`) — confirmé par grep, seule occurrence restante est le commentaire explicatif
  du retrait dans `CodeActiviteResolver.cs`.
- Niveau final `""` inchangé : aucune valeur de repli fabriquée.

## Checklist VALIDATION (reprise de la TASK)

- [x] **Build OK** — `dotnet build DeclarationTVA.slnx` (solution complète) → 0 erreur, warnings
      préexistants uniquement (`NU1510`, nullabilité `CS8xxx`, `CS0105` — sans rapport).
- [x] **Tests verts** — `dotnet test Declaration.Core.Tests` → 51/51 ; `dotnet test
      Declaration.Orchestration.Tests` → 181/181. Aucun échec, aucun test ignoré.
- [x] **Aucune référence restante** à `P_SOCIETECODEACTIVITETIERS`/`SCAT_NumeroTiers`/
      `SCAT_ErpIntitule` dans le code C# GRF (grep de contrôle, 0 résultat en `*.cs`).
- [x] **Documentation TASK-161 corrigée** — bandeau de correction en tête des 2 fichiers
      `DONE_DETAIL/TASK-161*.md`, renvoie vers TASK-179 et TASK-171.
- [ ] **Test réel écran ③ sur société sans `SCAT_NumeroTiers`** — non reproduit littéralement :
      la base de dev (`GR_EMA_DISTRIBUTION`, `DESKTOP-5BFKKEP`) possède en réalité la colonne
      `SCAT_NumeroTiers` (vérifié par `sqlcmd` sur `INFORMATION_SCHEMA.COLUMNS`), donc impossible de
      reproduire ici la condition exacte manquante chez le client. **Preuve retenue à la place** :
      la requête fautive (`GetMappingCodeActiviteTiersAsync`, seul point du code qui référençait
      `SCAT_NumeroTiers`) est supprimée entièrement — le crash ne peut structurellement plus se
      produire, indépendamment de la présence/absence de la colonne, sur aucun client. Confirmé par
      grep (0 référence restante) + les 232 tests verts (dont la cascade `CodeActiviteResolver`
      exercée isolément et via `MapLignesCandidates`).

## Notes

- Portée strictement limitée aux fichiers listés par la TASK, à l'exception du nettoyage en cascade
  documenté ci-dessus (15 fakes de test + `WorkflowEntities.cs`), rendu obligatoire par le retrait
  de la méthode d'interface — sans cela le build échoue (`CS0535`), précédent déjà rencontré et
  documenté sous TASK-147.
- Pas de dette technique silencieuse introduite : la classe `CodeActiviteTiersMappingRow` devenue
  orpheline a été supprimée plutôt que laissée en code mort.
- 1 commit prévu, aucun push.
