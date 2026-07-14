# TASK-082 — Bandeau incohérence absent en ② Affectations pour une ligne Exclue dès le figeage

Status: ✅ APPROUVÉE (2026-07-14)
Priority: CRITICAL
Risk: LOW (lecture seule stricte — signalement uniquement, aucune ligne/total modifié)
Module: `Declaration.Application/Services/DeclarationWorkflowService.cs` (`RevaliderLignesFigeesAsync`)

## OBJECTIF
Après le correctif TASK-081 (rebuild + redémarrage de l'API confirmés par le PO), le bandeau
d'incohérence restait **toujours absent** en écran ② Affectations sur `FC2501717`/`EC_Id=21473`,
alors qu'un message équivalent apparaissait déjà en écran Synthèse/Contrôle pour la même
déclaration.

## CAUSE RACINE
Sur ce cas précis, l'incohérence Sage (Σ(HT+TVA)≠TTC) est détectée **pendant** la lecture du cache
au moment même du figeage (`OrchestrateurDeclaration.cs:427-449`, `IncoherenceHtTvaTtc.EstIncoherent`),
**avant** que la ligne devienne `Proposee` — elle est créée directement en `Etat=Exclue` avec un
motif inline (« Non valorisé — Incohérence Sage détectée rétroactivement en cache : ... »). C'est un
chemin de détection différent de celui couvert par TASK-072/077/078/081 (qui concerne une ligne
**déjà figée avec succès** puis trouvée incohérente après coup).

`RevaliderLignesFigeesAsync` filtrait `lignes.Where(l => Etat == Proposee || Etat == Integree)` —
les lignes `Etat=Exclue` en étaient donc **totalement exclues du contrôle d'incohérence**, jamais
revalidées, jamais signalées en ②. L'écran Synthèse/Contrôle (`GetCheckupAsync`, alerte
`LIGNE_EXCLUE`) n'a jamais eu ce filtre — d'où l'écart observé entre écrans. Les tests existants
(TASK-077/081) ne couvraient que le cas `Etat=Proposee`, jamais celui-ci.

## CORRECTIF IMPLÉMENTÉ
`RevaliderLignesFigeesAsync` :
- Nouvelle liste `lignesExcluesIncoherence` = lignes `Etat=Exclue` avec `EC_Id > 0` et non déjà
  `IncoherenceValidee` (parité avec le contrôle TASK-078).
- Retour anticipé assoupli (`lignes.Count == 0 && lignesExcluesIncoherence.Count == 0`) pour ne pas
  court-circuiter une déclaration qui n'a QUE des lignes `Exclue`.
- `ecIds` (requête `GetEcIdsEnErreurAsync`) inclut désormais l'union des deux listes.
- Boucle dédiée émettant `LIGNE_FIGEE_A_REVERIFIER` (même code, `RefLigne = NumeroFacture`) pour ces
  lignes si leur `EC_Id` est dans `ecIdsEnErreur` — **sans** contrôle de dépointage MV (non
  pertinent : une ligne `Exclue` n'a jamais été déclarée/valorisée).
- Aucune ligne/valeur figée modifiée — signalement pur, conforme à l'invariant lecture seule du
  projet.

## VALIDATION
- Nouveau test `Declaration.Orchestration.Tests/Task082LigneExclueDesLeFigeageTests.cs` (3 tests,
  reproduisant exactement `FC2501717`/`EC_Id=21473`) :
  - l'alerte sort bien pour une ligne `Etat=Exclue` avec sentinelle d'erreur en cache ;
  - pas d'alerte fabriquée à tort pour une ligne `Exclue` sans sentinelle d'erreur ;
  - pas de resignalement si `IncoherenceValidee=true` (extension TASK-078).
- Build back 0 erreur.
- `Declaration.Orchestration.Tests` : **108/108** verts (105 existants + 3 nouveaux), 0 régression.
- **Confirmé en réel par le PO** (2026-07-14) après rebuild/redémarrage complet de l'API : bandeau
  visible dès le premier chargement de l'écran ②, sans passer par Synthèse/Contrôle.

## NOTES
Découverte et corrigée dans le prolongement direct de TASK-081 (même signalement PO initial,
bandeau ② toujours silencieux après le correctif 081 seul). Process : investigation + correction en
rôle worker exceptionnel (demande explicite PO, urgence signalée). Aucun fichier hors du périmètre
strict (`DeclarationWorkflowService.cs` + nouveau fichier de test) n'a été touché.
