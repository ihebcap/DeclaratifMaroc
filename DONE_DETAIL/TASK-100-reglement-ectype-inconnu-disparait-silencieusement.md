# TASK-100 — Règlement d'échéance EC_Type hors liste blanche (0/4/111) disparaît silencieusement du tunnel

Status: DONE
Priority: HIGH
Risk: HIGH (transparence — contredit un principe projet explicite)
Module: Declaration.Selection / Declaration.Application / declaration-tva-web (écran ①)

## OBJECTIF
Faire en sorte qu'un règlement rapproché, non encore déclaré, dont l'échéance porte un `EC_Type`
absent de la liste blanche « vraie facture » (0=Sage, 4=SoldeInitial, 111=FGR — cf.
`GrfEnums.EstEcTypeFacture`) soit **visible et expliqué**, au lieu de disparaître intégralement et
sans trace du tunnel de déclaration une fois sélectionné et figé.

## BUSINESS VALUE
Découvert en testant le nouvel environnement client (`DESKTOP-5BFKKEP`, base réelle
`GR_EMA_DISTRIBUTION`) sur la période 01/2026 : le règlement `RC26040045` (tiers BH CATERING,
13 053,66 MAD, `MV_Point=1` rapproché banque, `DT_Id IS NULL` non déclaré) apparaît normalement
dans la liste de l'écran ① Sélection (rien ne le distingue des règlements réellement déclarables).
L'utilisateur le coche naturellement. Après « Passer au calcul », il **disparaît purement et
simplement** de `DM_LGTVA` — aucune ligne, aucune alerte au checkup, aucune trace dans l'API
(`GET .../lignes` retourne `totalCount=0` pour son domaine). Cause : son échéance (`RT_ECHEANCE`)
porte `EC_Type=1`, une valeur non gérée par le routing TASK-022 (`SelectionExpliqueeEvaluator.cs:75`
lui assigne le motif `MotifRejet.EcTypeHorsPerimetre`), combiné à `DeclarationWorkflowService.cs`
lignes 627-631 (introduit par TASK-097) qui `continue` sur **tout** candidat non éligible sans
distinction de motif :
```csharp
if (!c.EstEligible)
{
    // TASK-097 : Tout ce qui n'est pas déclarable ne produit aucune ligne du tout
    continue;
}
```
Ceci contredit le principe explicitement posé par TASK-097 elle-même (« aucune ligne silencieuse »,
DONE_DETAIL/TASK-097 §NOTES/BUSINESS VALUE) : la décision PO du 14/07/2026 supposait qu'« après
TASK-099, il ne reste que des problèmes de qualité de donnée facture » — càd que les seuls motifs de
rejet règlement (`HorsPeriode`/`DejaDeclare`/`NonRapproche`) restent, déjà filtrés en amont par le
SQL de TASK-099. **Ce n'est pas le cas de `EcTypeHorsPerimetre`** (ni de `Impaye`/`Annule`/
`NonComptabilise`/`NonAffecte`) : rien, ni au niveau SQL (TASK-099), ni sur l'écran ①
(`ReglementRapprochementDto` n'expose aucun indicateur de déclarabilité, seulement `declare` =
déjà-déclaré), ne signale à l'utilisateur qu'un règlement qu'il vient de cocher ne produira
finalement rien.

## CONTRAINTES
- Ne pas réintroduire de ligne `Exclue` en base si la décision PO reste « pas de ligne Exclue pour
  motif règlement » (TASK-097) — la correction porte donc plutôt sur la **visibilité en amont**
  (écran ①) et/ou une **alerte de synthèse** (checkup), pas nécessairement sur `MapLignesCandidates`.
- Deux pistes possibles, à trancher avec le PO (hors périmètre de cette analyse) :
  1. Exposer un indicateur de non-déclarabilité (avec motif résumé) sur `GET /api/rapprochement`
     (écran ①), pour empêcher/déconseiller la sélection en amont — même logique que le masquage
     des non-rapprochés (TASK-099).
  2. Conserver la sélection possible, mais faire remonter une alerte explicite au checkup
     (`GetCheckupAsync`) listant les règlements sélectionnés mais exclus du calcul, avec motif —
     symétrique à l'alerte `LIGNE_FIGEE_A_REVERIFIER` existante.
- Vérifier si d'autres motifs évaluateur (`Impaye`, `Annule`, `NonComptabilise`, `NonAffecte`)
  souffrent du même angle mort (probable, même mécanisme de `continue` silencieux).
- Aucune régression sur le comportement voulu pour `HorsPeriode`/`DejaDeclare`/`NonRapproche`
  (ceux-là sont bien filtrés en amont par TASK-099, donc jamais sélectionnables — à ne pas casser).

## FILES
- Declaration.Selection/SelectionExpliqueeEvaluator.cs (motif `EcTypeHorsPerimetre` et voisins)
- Declaration.Application/Services/DeclarationWorkflowService.cs (`MapLignesCandidates` l.627-631,
  `GetCheckupAsync`)
- Declaration.API/Dtos/ReglementRapprochementDto.cs / RapprochementController.cs (indicateur de
  déclarabilité éventuel sur l'écran ①)
- declaration-tva-web/src/ReglementsSelection.tsx (affichage front si option 1 retenue)

## VALIDATION
- [ ] Build OK
- [ ] Tests passés
- [ ] Règlement rapproché/non déclaré avec `EC_Type` hors 0/4/111 sélectionné → soit non
      sélectionnable à l'écran ① avec motif visible, soit alerte explicite au checkup nommant le
      règlement — jamais une disparition muette (preuve base réelle, cas `RC26040045`).
- [ ] Confirmation qu'aucun autre motif évaluateur (`Impaye`/`Annule`/`NonComptabilise`/
      `NonAffecte`) ne partage le même angle mort, ou traitement symétrique si c'est le cas.
- [ ] Aucune régression sur `HorsPeriode`/`DejaDeclare`/`NonRapproche` (toujours filtrés en amont,
      toujours aucune ligne `Exclue`).

## ARCHITECTURE RULES APPLICABLES
- Principe de transparence du projet : « aucune ligne/rejet silencieux » (posé par TASK-097 elle-même).
- Pas de dette technique silencieuse : si la correction ne couvre qu'`EcTypeHorsPerimetre` dans un
  premier temps (sans traiter `Impaye`/`Annule`/`NonComptabilise`/`NonAffecte`), le documenter
  explicitement en NOTES du VERIFY.

## DÉCISION PO (17/07/2026) — nature du cas EC_Type=1 tranchée
`EC_Type = 1` = **impayé client**. Règle métier confirmée par le PO :
- L'impayé doit être **affiché** dans la déclaration (transparence — jamais de disparition muette).
- Il est **NON déclarable** : hors du total déclaré, **jamais tamponné `DT_Id`**.
- Le **traitement fiscal réel des impayés est reporté en PHASE 2** (à spécifier ultérieurement).

**Périmètre phase 1 (validé)** : la seule exigence est que l'impayé remonte **visiblement** avec un
statut/motif explicite « impayé — non déclarable (à traiter phase 2) », au lieu de disparaître
silencieusement. Aucune logique de calcul/valorisation de l'impayé en phase 1. Les pistes 1/2 des
CONTRAINTES restent valables (visibilité écran ① et/ou alerte checkup). Le principe général demeure :
tout `EC_Type` hors liste blanche doit remonter visiblement, jamais de silence.

## NOTES
Découvert par l'architecte (16/07/2026) en testant le tunnel de bout en bout via l'API réelle sur
le nouvel environnement client `DESKTOP-5BFKKEP` (base `GR_EMA_DISTRIBUTION`), période 01/2026,
règlement réel `RC26040045` (tiers BH CATERING, 13 053,66 MAD). Décision PO initiale : documenter
sans corriger dans l'immédiat ; nature métier (impayé client) tranchée le 17/07/2026 (cf. section
DÉCISION PO ci-dessus).
