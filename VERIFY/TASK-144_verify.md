# TASK-144 Verify — Diagnostic explicatif en ligne pour les lignes en anomalie

> Implémentation réalisée en tant que worker (exceptionnellement). LECTURE SEULE STRICTE respectée.
> Les deux builds (back .NET + front tsc/vite) passent. **Deux critères de la checklist restent
> ouverts et nécessitent l'environnement réel / le PO** (voir § « Reste à valider ») — ce VERIFY
> ne les déclare PAS satisfaits.

## Périmètre livré

Diagnostic à la demande (une ligne à la fois, jamais en masse) déclenché par un bouton
« Diagnostiquer » sur les lignes non valorisées de l'étape ② Vérifier/Intégrer. Trois blocs
**séparés** conformes à l'objectif :

1. **Identité de l'échéance Sage** : `EC_Id`, `EC_No`, numéro de pièce (`DO_Numero`), tiers issu de
   `RT_ECHEANCE` (`CT_Code`/`CT_Intitule` — pas `RT_MOUVEMENT`, piège `grf-do-numero-collision-multi-tiers`),
   origine (`EC_Type`), montant devise.
2. **Pourquoi la ligne n'est pas valorisée** : motif d'échec OM **relu depuis le cache**
   (`DM_VENTILATION_SAGE_CACHE.MotifErreur`) — aucune nouvelle lecture OM Sage — + **traduction en
   français métier + action recommandée**, avec repli honnête si le motif n'est pas catalogué.
3. **Contrôle collision `DO_Numero`** : nouveau, calculé à la demande. Verdict `F_DOCREGL` (lecture
   seule base Sage résolue par `SO_Id`, pattern TASK-118) — quel tiers a un document réel, lequel est
   orphelin. Présenté **explicitement comme indépendant** de l'échec OM (jamais comme sa cause).

## Fichiers modifiés / créés

Back (nouveaux) :
- `Declaration.Application/Entities/DiagnosticLigne.cs` — structures lecture seule.
- `Declaration.Application/Services/DiagnosticMotifMetier.cs` — traduction métier PURE (⚠ à faire relire PO).
- `Declaration.API/Dtos/DiagnosticLigneDto.cs` — contrat JSON.

Back (modifiés) :
- `Declaration.Application/Interfaces/IDeclarationRepository.cs` — 4 méthodes lecture seule.
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` — implémentations (RT_ECHEANCE GRF,
  cache persistance, F_DOCREGL Sage). Aucune requête existante modifiée.
- `Declaration.Application/Services/DeclarationWorkflowService.cs` — `DiagnostiquerLigneAsync` (orchestration
  lecture seule, ne réutilise PAS l'OrchestrateurDeclaration, aucune relecture OM).
- `Declaration.API/Controllers/DeclarationsController.cs` — `GET {id}/lignes/diagnostic/{ecId}`.

Front (nouveaux) :
- `declaration-tva-web/src/DiagnosticModal.tsx` — panneau 3 blocs séparés.

Front (modifiés) :
- `declaration-tva-web/src/api.ts` — `getDiagnosticLigne` + types.
- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` — bouton « Diagnostiquer » sur les lignes non
  valorisées, `ecId` propagé dans `LigneValorisation`/`RowAggr`, rendu du modal.

`ProofModal.tsx` / `AffectationsDrill.tsx` : **non modifiés** (garde-fou non-régression TASK-078).

## Checklist

- [x] Build back (`dotnet build Declaration.API`) OK — 0 erreur (10 warnings préexistants).
- [x] Build front (`tsc -b` OK, `vite build` OK).
- [x] Aucune écriture en base : toutes les nouvelles requêtes sont des `SELECT`
      (RT_ECHEANCE, DM_VENTILATION_SAGE_CACHE, F_DOCREGL). Aucun INSERT/UPDATE/DELETE ajouté.
- [x] Pas de nouvelle lecture OM Sage : le motif d'échec est lu depuis le cache déjà persisté ;
      le seul accès Sage est le `SELECT F_DOCREGL` (contrôle collision), base résolue par `SO_Id`.
- [x] Connexion Sage résolue dynamiquement via `GetSageConnectionInfoAsync(soId)` — jamais codée en dur.
- [x] Non-régression `ProofModal`/`AffectationsDrill` : fichiers non touchés.
- [x] Les 3 informations restent séparées ; le commentaire collision n'affirme jamais un lien de
      causalité avec l'échec OM (message explicite quand l'échéance consultée a bien un document).

## Reste à valider (NON couvert par ce VERIFY — bloquant clôture)

1. ✅ **RÉSOLU par l'architecte (20/07/2026)** — Rejeu du cas réel `FA2600106` (EC_Id=21849,
   `TVA1-2026-02`) vérifié directement contre `GR_EMA_DISTRIBUTION`/`DESKTOP-5BFKKEP` (accès réel
   disponible, cf. mémoire `grf-acces-db-prod-disponible`) : `EC_Id=21849` (tiers CT_No=166) →
   `F_DOCREGL.DR_No=4947` → document réel `FA2600106` présent. `EC_Id=18608` (tiers CT_No=188) →
   `F_DOCREGL.DR_No=1172` → aucune ligne (orphelin). Conforme au verdict attendu.
2. ✅ **RÉSOLU par l'architecte (20/07/2026)** — Schéma `F_DOCREGL` confirmé : colonnes
   `DR_No`/`DO_Piece`/`DR_Date` existent. Précision importante : `F_DOCREGL.EC_No` existe mais vaut
   **0 sur les 4251 lignes de la base** — inutilisable. La vraie clé de jointure, vérifiée
   empiriquement sur 20+ échéances, est **`RT_ECHEANCE.EC_No = F_DOCREGL.DR_No`** (déjà celle
   implémentée dans `DeclarationRepository.cs:1413-1417`, malgré un commentaire ambigu). Aucun
   changement de code requis sur ce point.
3. ⏳ **TOUJOURS OUVERT — ne peut pas être auto-approuvé par un worker.** Relecture + validation
   explicite PO des libellés métier (`DiagnosticMotifMetier.cs`) — critère de compréhensibilité,
   pas d'exhaustivité technique. Nécessite une lecture humaine du PO. **Ne pas clôturer TASK-144 sur
   ce seul point sans réponse explicite du PO** — si un worker autonome atteint cette étape sans le
   PO disponible, laisser la TASK en attente sur ce point précis plutôt que de l'auto-valider.
4. Rappel : TASK-144 = **visibilité**, pas correctif de données. Un lot de TASKs de suite a été
   rédigé le 20/07/2026 (TASK-145 à TASK-152, voir `TODO.md`) couvrant : le bug Achat/Vente
   découvert en session (TASK-145), le drill imprécis (TASK-146), le recalcul de ligne périmée
   (TASK-147, extension directe de TASK-144), et les 3 correctifs de données de TASK-143
   (TASK-149/150/151/152).

## Verdict

Points 1 et 2 résolus par vérification directe de l'architecte contre les données réelles (20/07/2026).
**Point 3 (validation PO des libellés métier) reste bloquant pour la clôture finale** — tout le reste
de l'implémentation (build, lecture seule, non-régression) est confirmé correct.
