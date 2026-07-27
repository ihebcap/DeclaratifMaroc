# TASK-179 — Retirer le niveau « défaut par tiers » de la cascade code activité (crash 500 en production)

Status: TODO
Priority: HIGH
Risk: MEDIUM
Module: Declaration.Core / Declaration.Application / Declaration.Infrastructure / Declaration.Selection

## OBJECTIF
Retirer complètement le niveau 2 « défaut par tiers » (`P_SOCIETECODEACTIVITETIERS`, TASK-161) de la
cascade `CodeActiviteResolver.Resoudre` — décision PO tranchée (24/07/2026, arbitrage TASK-171 §1,
option B). La cascade passe de 4 à 3 niveaux : surcharge manuelle par ligne → `F_COMPTET.CT_APE` → "".
Ce retrait élimine aussi la cause du crash 500 en production (`GetLignes`/figeage plante sur
`SqlException: Nom de colonne non valide : 'SCAT_NumeroTiers'`) en supprimant l'appel fautif, plutôt
qu'en le rendant seulement résilient.

## BUSINESS VALUE
Preuve en production (log fourni par le PO, `DeclaratifMaroc.out.log`, 24/07/2026) : l'écran
③ Vérifier & Intégrer est entièrement inutilisable chez ce client — `GetMappingCodeActiviteTiersAsync`
(`DeclarationRepository.cs:1544`) lève une `SqlException` non catchée (colonne `SCAT_NumeroTiers`
absente), remontant en 500 générique à travers `ExecuterAvecVerrouOMAsync`/`GetLignes`. Analyse
architecte (confirmée par le PO) : ce niveau 2 n'a jamais fonctionné chez aucun client réel depuis sa
livraison (TASK-171 : mauvais champ comparé, `SCAT_ErpIntitule` n'étant pas le nom du tiers), dépend
d'une table possédée par `apbs-gr_winform` que le PO refuse de faire modifier, et fait doublon avec le
niveau 3 (`CT_APE`), déjà fonctionnel et sans dépendance externe. Le retirer simplifie la cascade et
supprime définitivement le risque de crash (pas seulement chez ce client — toute société sans la
colonne `SCAT_NumeroTiers` y était exposée).

## CONTRAINTES
- Ne conserver que 3 niveaux dans `CodeActiviteResolver.Resoudre` : surcharge manuelle → `CT_APE`
  (paramètre `codeActiviteSage`) → "". Signature simplifiée : retirer les paramètres
  `mappingParNumero`/`mappingParNom` (et `tiersNumero`/`tiersNom` si plus utilisés par aucun niveau
  restant — vérifier avant de les retirer, ne pas casser un appelant qui les utiliserait encore).
- Supprimer entièrement `ChargerMappingCodeActiviteTiersAsync` (`DeclarationWorkflowService.cs:960-973`)
  et `GetMappingCodeActiviteTiersAsync` (`DeclarationRepository.cs:1544-1554`) — plus aucun appelant
  après le retrait des 2 sites d'appel (lignes ~311 et ~576).
- Ne jamais toucher `apbs-gr_winform`/`P_SOCIETECODEACTIVITETIERS` (rappel PO, TASK-171) — cette task
  ne fait que cesser de LIRE cette table côté GRF, aucune modification côté winform.
- Aucune valeur de code activité fabriquée si la résolution échoue (niveau final "" inchangé — décision
  PO TASK-161 point 3, le code activité n'entre pas dans le XML de dépôt DGI).
- Documentation : corriger `DONE_DETAIL/TASK-161-code-activite-defaut-tiers-surcharge-ligne.md` et
  `DONE_DETAIL/TASK-161_verify.md` pour ne plus décrire un niveau 2 qui n'existe plus (déjà signalé
  comme non avéré par TASK-171, à corriger définitivement ici).

## FILES
- [Declaration.Core/CodeActiviteResolver.cs](../Declaration.Core/CodeActiviteResolver.cs) — cascade simplifiée à 3 niveaux.
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) — retirer `ChargerMappingCodeActiviteTiersAsync` (960-973) et ses 2 appels (311-313, 576-577) ; adapter `MapLignesCandidates` en conséquence.
- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs) — retirer `GetMappingCodeActiviteTiersAsync` (1544-1554) et `CodeActiviteTiersMappingRow` si plus utilisé ailleurs.
- [Declaration.Application/Interfaces/IDeclarationRepository.cs](../Declaration.Application/Interfaces/IDeclarationRepository.cs) — retirer la signature correspondante.
- [Declaration.Selection/SelectionExpliqueeEvaluator.cs:135](../Declaration.Selection/SelectionExpliqueeEvaluator.cs) et [Declaration.Selection/SelectionnerAffectationsService.cs:115](../Declaration.Selection/SelectionnerAffectationsService.cs) — appels à `Resoudre` à adapter à la nouvelle signature (niveaux 3/4 uniquement, déjà le cas en pratique — vérifier qu'aucun argument retiré n'était utilisé ici).
- [Declaration.Core.Tests/CodeActiviteResolverTests.cs](../Declaration.Core.Tests/CodeActiviteResolverTests.cs) — retirer les tests du niveau 2 (mapping par tiers), garder/adapter ceux de la surcharge manuelle et de `CT_APE`.
- [Declaration.Orchestration.Tests/Task161CodeActiviteCascadeTests.cs](../Declaration.Orchestration.Tests/Task161CodeActiviteCascadeTests.cs) — retirer le(s) cas de test exerçant le mapping tiers (ex. ligne 64-67), garder ceux couvrant la surcharge manuelle et `CT_APE`.
- `DONE_DETAIL/TASK-161-code-activite-defaut-tiers-surcharge-ligne.md`, `DONE_DETAIL/TASK-161_verify.md` — correction documentaire (cascade réellement livrée = 3 niveaux, pas 4).

## VALIDATION
- [ ] Build OK (solution complète `DeclarationTVA.slnx`, pas juste `Declaration.API.csproj`).
- [ ] `Declaration.Core.Tests`, `Declaration.Orchestration.Tests` verts après adaptation.
- [ ] Test réel : chargement de l'écran ③ Vérifier & Intégrer sur la base concernée
      (`GR_EMA_DISTRIBUTION`, société sans `SCAT_NumeroTiers`) → plus de 500, lignes chargées
      normalement, code activité résolu via `CT_APE` ou "" (jamais via un mapping tiers).
- [ ] Aucune référence restante à `P_SOCIETECODEACTIVITETIERS`/`SCAT_NumeroTiers`/`SCAT_ErpIntitule`
      dans le code GRF (grep de contrôle).
- [ ] `DONE_DETAIL/TASK-161*.md` corrigés pour refléter la cascade réellement livrée (3 niveaux).

## ARCHITECTURE RULES APPLICABLES
- ARCHITECTURE.md §5 : pas de dette technique silencieuse — documentation TASK-161 corrigée en même
  temps que le code, pas laissée à décrire une fonctionnalité qui n'existe plus.
- Pas de duplication de logique existante — le niveau 3 (`CT_APE`) couvre déjà le besoin, confirmé
  fonctionnel, pas de mécanisme de repli à réintroduire ailleurs.

## NOTES
- Arbitrage PO tranché le 24/07/2026 (option B de TASK-171 §1) — voir la note correspondante dans
  [TASK-171](TASK-171-cascade-code-activite-mapping-tiers-jamais-fonctionnel.md).
- Stack trace de l'incident production ayant motivé l'urgence :
  `SqlException (0x80131904): Nom de colonne non valide : 'SCAT_NumeroTiers'` →
  `DeclarationRepository.GetMappingCodeActiviteTiersAsync:1552` →
  `DeclarationWorkflowService.ChargerMappingCodeActiviteTiersAsync:963` →
  `ConstruireLignesFigeesAsync` → `DeclarationsController.GetLignes:123` (log complet :
  `C:\Users\Iheb\Downloads\ema erreur\DeclaratifMaroc.out.log`, fourni par le PO).
- Second site d'appel identique à corriger : `DeclarationWorkflowService.cs:576` (réintégration des
  lignes libérées, `RevaliderLignesFigeesAsync`) — même cause, même correctif.
- Si un doute apparaît en cours d'implémentation sur un appelant utilisant encore `tiersNumero`/
  `tiersNom` pour un autre usage que le mapping tiers retiré, s'arrêter et signaler plutôt que de
  deviner — ne pas retirer un paramètre encore utile ailleurs.
