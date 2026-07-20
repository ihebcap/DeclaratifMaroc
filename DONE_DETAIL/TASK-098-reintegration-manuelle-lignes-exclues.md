# TASK-098 — Aucun mécanisme de réintégration manuelle d'une ligne `Exclue` (motif métier)

Status: ⏸️ EN SUSPENS — probablement caduque après TASK-097, voir NOTES
Priority: HIGH
Risk: MEDIUM
Module: Declaration.Application / Declaration.API / declaration-tva-web (DomainGrid)

> ⚠️ **Périmètre à réévaluer après TASK-097** (décision PO 14/07/2026 : simplification totale,
> plus de ligne `Exclue` pour un motif facture — non ventilée, ICE/IF manquant). Après TASK-097, le
> seul motif encore capable de produire un `Exclue` est le garde-fou d'exclusivité inter-déclaration
> (TASK-080, `DejaEnCoursAilleurs`) — **déjà réintégré automatiquement** (`ReintegrerReglementsLiberesAsync`).
> Cette task ne garde probablement d'utilité que pour les lignes `Exclue` **déjà présentes en base**
> sur des déclarations antérieures à TASK-097 (dette historique) — à confirmer avec le PO une fois
> TASK-097 livrée avant de démarrer celle-ci.

## OBJECTIF
Fournir un mécanisme réel (UI + API) permettant de réintégrer une ligne `DM_LGTVA` figée `Exclue`
par un motif métier (`HorsPeriode`, `DejaDeclare`, `TiersSansICE`/`IF`, facture non ventilée, etc.),
une fois la cause corrigée à la source — avec revalorisation complète (HT/Taux/TVA/TTC réels), pas
un simple changement d'état à des montants figés à zéro.

## BUSINESS VALUE
Aujourd'hui, seul le cas `DejaEnCoursAilleurs` (conflit inter-déclaration, TASK-080) se réintègre
automatiquement. Tous les autres motifs de rejet laissent la ligne bloquée `Exclue` en base de
façon définitive pour la déclaration — sans action utilisateur possible, y compris après
correction de la donnée source (ex. ICE renseigné en tiers, rapprochement effectué). La grille
capable de forcer un changement d'état (`DomainGrid.tsx`, boutons Intégrer/Exclure/Reporter/
Réinitialiser) existe déjà mais est montée en `readonly={true}` partout où elle est atteignable
dans le tunnel actuel (`VerifierIntegrerPanel.tsx:484`, `DeclarationFinalePanel.tsx:251`).

## CONTRAINTES
- Ne pas simplement déverrouiller le `PATCH /lignes/{id}` existant tel quel : il fait un
  `UPDATE ... SET Etat=@Etat` brut (`DeclarationRepository.cs:534`), sans recalcul — une ligne
  réintégrée ainsi porterait HT/TVA/TTC à zéro (valeurs figées lors de l'exclusion), donc une
  déclaration silencieusement sous-évaluée. La réintégration doit revaloriser, sur le modèle de
  `ReintegrerReglementsLiberesAsync` (rejoue `SelectionnerExpliqueeAsync` → garde-fous →
  orchestrateur → `MapLignesCandidates`), pas juste changer l'`Etat`.
- Respecter le verrou `DT_Id`/TASK-028/064 : aucune réintégration possible sur une ligne déjà
  `Integree`/déclaration `Cloturee`.
- Traçabilité obligatoire (qui/quand a réintégré, motif d'origine conservé pour audit) — cf.
  patron déjà en place pour `ValiderIncoherenceLigneAsync` (TASK-078).
- Dépend de TASK-097 : le périmètre de sélection doit d'abord être fiable (sinon on réintègre une
  ligne dans un scope qui ne reflète de toute façon pas la sélection réelle de l'utilisateur).

## FILES
- declaration-tva-web/src/DomainGrid.tsx (`doBulkAction`, boutons déjà présents mais masqués par
  `readonly`)
- declaration-tva-web/src/VerifierIntegrerPanel.tsx (montage `readonly={true}` du drill anomalie)
- declaration-tva-web/src/DeclarationFinalePanel.tsx (idem)
- Declaration.Application/Services/DeclarationWorkflowService.cs (`ReintegrerReglementsLiberesAsync`
  à généraliser au-delà du seul motif `DejaEnCoursAilleurs`)
- Declaration.API/Controllers/DeclarationsController.cs (`UpdateLigneEtat`, `UpdateLignesBulk`)
- Declaration.Infrastructure/Repositories/DeclarationRepository.cs

## VALIDATION
- [ ] Build OK
- [ ] Tests passés
- [ ] Ligne exclue pour motif `TiersSansICE` → ICE corrigé côté Sage → action de réintégration →
      ligne repasse `Proposee` avec HT/Taux/TVA/TTC réels (non nuls), preuve base réelle.
- [ ] Tentative de réintégration sur une ligne d'une déclaration `Cloturee` → refusée explicitement.
- [ ] Aucune régression sur la réintégration automatique existante (TASK-080,
      `DejaEnCoursAilleurs`).

## ARCHITECTURE RULES APPLICABLES
- Aucun bypass d'une couche de sécurité/verrou (`DT_Id`, TASK-028/064).
- Audit trail obligatoire sur toute action sensible (réintégration = correction manuelle d'un
  rejet, donc sensible).
- Pas de dette technique silencieuse : si le déverrouillage de `DomainGrid` est limité à certains
  motifs de rejet dans un premier temps, le documenter explicitement en NOTES du VERIFY.

## NOTES
Découvert en répondant à une question PO sur la réintégration des lignes `Exclue` (14/07/2026).
Séquencer après TASK-097 (ordre PO à confirmer).
