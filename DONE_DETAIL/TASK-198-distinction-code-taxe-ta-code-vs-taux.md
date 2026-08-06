# TASK-198 — Distinguer les taux de TVA par code (TA_Code), pas seulement par pourcentage

Status: ✅ **Terminée** (05/08/2026) — périmètre tranché par le PO (05/08/2026) : **interne uniquement**, XML non
concerné
Priority: MEDIUM (traçabilité/contrôle interne, aucun enjeu de conformité du dépôt légal)
Risk: MEDIUM (changement transverse — modèle, ventilation, écrans, export Excel)
Module: Declaration.Core / Declaration.Orchestration / Declaration.Application

> **Origine :** règle métier communiquée par le PO (05/08/2026) : Sage peut avoir **deux codes
> taxe distincts au même taux** (ex. deux lignes `F_TAXE` à 20 %, l'une pour la TVA sur achats
> courants, l'autre pour la TVA sur immobilisations) — `TA_Code` différent, `TA_Taux` identique.
> Le PO indique que cette distinction est importante et souhaite que le code taxe soit repris
> partout, pas seulement le taux.

## Constat (preuve de code)

Le système ne capture et ne propage **que le taux numérique** (`decimal Taux`), jamais le code
taxe (`F_TAXE.TA_Code`) :

- `Declaration.Selection/SelectionExpliqueeService.cs:137-139` (résolution frais bancaire) ne lit
  que `TA_No, TA_Taux` — `TA_Code` n'est même pas sélectionné en SQL.
- `Declaration.Orchestration/LecteurTvaFgr.cs:131-138` (résolution FGR) lit `TA_Code, TA_Taux`
  mais **n'utilise `TA_Code` que comme clé de recherche interne** (`dict[row.TA_Code.ToString()]`),
  jamais reporté en sortie — seul le `TA_Taux` numérique ressort.
- Tous les modèles de ligne (`Declaration.Core/Model.cs:66,88` `LigneDeclarationEnrichie`,
  `Declaration.Application/Entities/WorkflowEntities.cs:108`,
  `Declaration.Application/Entities/DiagnosticLigne.cs:82`) n'ont qu'un champ `decimal Taux` — pas
  de champ code taxe.
- Le regroupement pour les récaps (`DeclarationWorkflowService.cs`, `RecapsParTaux`) groupe
  strictement par `l.Taux` (valeur numérique) — deux lignes à 20 % mais codes taxe différents sont
  **fusionnées dans le même bucket**, la distinction achat/immobilisation est perdue dès cette
  étape.
- L'export XML officiel (`DeclarationXmlExporter.cs:78`) n'écrit que `<tx>{Taux/100}</tx>` — le
  schéma « relevé de déductions » (CDC) ne semble exposer, par ligne, que le taux, pas de champ
  code/nature. **Point à vérifier avec le PO/comptable**, pas à assumer ici.
- Fait notable et potentiellement lié : `<des>` (désignation, ligne 65-68) est actuellement un
  **placeholder fixe** `"Achat marchandise"` pour **toutes** les lignes, quel que soit leur code
  taxe réel (TASK-181, connu et déjà signalé comme provisoire dans `TODO.md`). Si la distinction
  achat/immobilisation doit être visible dans l'export, c'est potentiellement **ce champ** qui
  devrait varier selon `TA_Code`, plutôt qu'un nouveau champ XML à inventer.

## Décision PO (05/08/2026)

**Interne uniquement** — le dépôt XML n'a besoin que du taux (`<tx>`, inchangé), la distinction
`TA_Code` sert uniquement au contrôle/traçabilité côté GRF (écrans, export Excel). `<des>` reste
hors périmètre (TASK-181 non rouverte par cette TASK).

## Objectif

- Propager `TA_Code` (pas seulement `TA_Taux`) depuis la résolution Sage (`LecteurTvaFgr.cs`,
  `SelectionExpliqueeService.cs` frais bancaire, et tout autre point de résolution taux à
  recenser) jusqu'aux modèles de ligne (`LigneDeclarationEnrichie` et équivalents).
- Adapter le regroupement `RecapsParTaux` pour distinguer par `(Taux, TA_Code)` plutôt que `Taux`
  seul — sans casser l'affichage existant qui n'a jamais connu cette distinction (à valider avec
  le PO : nouvelle colonne ? sous-groupe ? libellé ?).
- Répercuter dans l'export Excel « Lignes à déclarer »/récap par taux (`Declaration.Export.Excel`)
  la même distinction.

## Garde-fous

- **Ne pas toucher à `DeclarationXmlExporter.cs`** — hors périmètre par décision PO.
- Recenser **tous** les points de résolution de taux avant de committer un périmètre fermé (celui
  documenté ici — FGR, frais bancaire — n'est peut-être pas exhaustif, à vérifier aussi pour le
  chemin OM/Sage classique si un point de résolution taux distinct existe côté worker).
- Non-régression stricte sur le montant total de TVA déclarée (le changement est une distinction
  supplémentaire, jamais un recalcul de montant).

## Files

- [Declaration.Orchestration/LecteurTvaFgr.cs:125-140](../Declaration.Orchestration/LecteurTvaFgr.cs#L125-L140)
- [Declaration.Selection/SelectionExpliqueeService.cs:130-147](../Declaration.Selection/SelectionExpliqueeService.cs#L130-L147)
- [Declaration.Core/Model.cs:58-90](../Declaration.Core/Model.cs#L58-L90) (`LigneDeclarationEnrichie`)
- [Declaration.Application/Entities/WorkflowEntities.cs:108](../Declaration.Application/Entities/WorkflowEntities.cs#L108)
- [Declaration.Export.Xml/DeclarationXmlExporter.cs:65-78](../Declaration.Export.Xml/DeclarationXmlExporter.cs#L65-L78) (`<des>` placeholder TASK-181, `<tx>`)

## Validation

- [x] Deux lignes au même taux mais `TA_Code` différent restent distinguables de bout en bout
      (au minimum dans les écrans/export internes ; dans l'XML si la réponse PO l'exige).
- [x] Non-régression sur le total TVA déclarée (montants inchangés, seule la granularité de
      regroupement change).
- [x] Tests unitaires couvrant le cas « même taux, codes différents » sur le recap par taux.

## Dépendances / risques

- Aucune dépendance bloquante — périmètre tranché par le PO, prête pour développement.
- Risque de périmètre sous-estimé si d'autres points de résolution taux existent (chemin OM/Sage
  classique non recensé ici) — à vérifier en amont du développement.
