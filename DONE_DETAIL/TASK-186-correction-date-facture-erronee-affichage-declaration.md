# TASK-186 — Correction : « Date Facture » affichée dans la Déclaration (grilles + export Excel) = date d'affectation, pas la date réelle de la facture

## Contexte
Signalement PO (27/07/2026), déclenché par un test comptable : « il manque les factures de 2025 » sur
la Déclaration, alors que la règle validée est bien que la sélection se fait par **date de rapprochement**
(§1quater/1quinquies `MODULE_DECLARATION_TVA.md`, TASK-062). Cas concret fourni par le PO : facture
**FC2501193**, feuille « Factures à déclarer » de l'export Excel, colonne « Date Facture » affiche
**15/04/2026**.

Diagnostic architecte (lecture code + vérification base réelle `GR_EMA_DISTRIBUTION`, lecture seule) :
- **La sélection elle-même est correcte** : `FC2501193` (`EC_Id=21466`) a bien un règlement rapproché
  (`RT_MOUVEMENT.MV_PointDate = 30/01/2026`) et n'est pas encore déclarée — elle apparaît donc à juste
  titre dans une déclaration 2026, conformément à la règle « date de rapprochement ».
- **Le champ affiché est faux** : `RT_ECHEANCE.DO_Date` (date réelle de la facture) = **18/07/2025**.
  La valeur affichée (15/04/2026 11:58:31) correspond exactement à `RT_AFFECTATION.AF_Date` (date
  d'enregistrement de l'affectation en base, pas la date de la facture) pour `AF_Id=20522` lié à cette
  échéance — vérifié directement en base par requête `JOIN RT_ECHEANCE/RT_AFFECTATION` sur ce cas précis.
- **Portée du bug** : confirmée sur un échantillon de 4 autres cas (`EC_Id=20224/20433/20503/20669`,
  factures datées 2025) — même écart systématique entre `DO_Date` réel et « Date Facture » affichée.
  Sur les 402 factures fournisseur datées 2025 de la base réelle, 352 ont un règlement rapproché
  (uniquement en 2026, aucun rapprochement 2025 n'existe dans cette base) et ne sont pas encore
  déclarées — potentiellement toutes affectées par ce même défaut d'affichage dès qu'elles apparaîtront
  dans une déclaration.
- **Conséquence côté testeur** : en cherchant/filtrant les factures par année réelle (2025), le
  comptable ne trouve aucune ligne portant une date 2025, car la colonne ne montre jamais `DO_Date` —
  d'où l'impression que « les factures de 2025 manquent », alors qu'elles sont bien sélectionnées, juste
  mal étiquetées.

## Cause racine (code)
`Declaration.Selection/SelectionExpliqueeService.cs` — les 3 requêtes du chemin **règlement-first**
(`GetSurensembleFournisseurSql`, `GetSurensembleDepenseSql`, `GetSurensembleClientSql`) aliasent
**`A.AF_Date AS DateFacture`** dans leur `SELECT`, alors que `RT_ECHEANCE` est déjà jointe (`LEFT JOIN
RT_ECHEANCE E ON A.EC_Id = E.EC_Id`) et expose la vraie date de facture `E.DO_Date`. Cette valeur mal
nommée traverse ensuite tout le pipeline sans transformation :
`AffectationCandidateRow.DateFacture` → `SelectionExpliqueeEvaluator` → `AffectationCandidate.DateFacture`
(`Declaration.Core/Model.cs`) → `LigneDeclarationEnrichie.DateFacture` (`ConstructeurDeclaration.cs`) →
`Exporter.cs` (colonne 15 « Date Facture », feuilles « Factures à déclarer » ET « Détail TVA » — les deux
`CreerFeuille...` partagent le même mapping, cf. lignes 63 et 300 de `Exporter.cs`).

**Différence avec le chemin facture-first** (`LireFacturesDepuisPeriodeAsync` / `GetFactureFirstSql`,
TASK-050) : celui-ci alias déjà correctement `E.DO_Date AS DateFacture` — **seul le chemin
règlement-first est affecté**. Les lignes `Source=Encaissement/Decaissement/Depense` (règlement-first)
sont impactées ; à vérifier si des lignes de la déclaration proviennent aussi du chemin facture-first
(auquel cas ces lignes-là ont déjà la bonne date).

## Objectif
```
Corriger les 3 requêtes règlement-first (GetSurensembleFournisseurSql/Depense/Client) :
  remplacer A.AF_Date AS DateFacture
  par       E.DO_Date AS DateFacture   (déjà disponible via le LEFT JOIN RT_ECHEANCE E existant)

Aucune INTERFACE ne change (même nom de colonne DateFacture en sortie) — seul le SQL source change.
```

## Périmètre STRICT
- **Inclus** : les 3 fragments SQL cités (`SelectionExpliqueeService.cs`), lecture seule, aucun schéma
  touché. Vérification que la même correction ne casse rien côté chemin facture-first (déjà correct,
  à ne pas toucher).
- **Exclu** : ajout de colonne (traité séparément, TASK-187) ; toute logique de sélection/éligibilité
  (`RegleDatePeriode`/`EstDeclarable`, non concernée — seul un champ d'AFFICHAGE est faux, la sélection
  elle-même est correcte) ; les déclarations déjà générées/déposées ne sont pas retraitées
  rétroactivement (hors périmètre, à arbitrer séparément par le PO si nécessaire).

## Étapes
1. Corriger l'alias dans les 3 requêtes `GetSurensembleFournisseurSql`/`GetSurensembleDepenseSql`/
   `GetSurensembleClientSql` (`Declaration.Selection/SelectionExpliqueeService.cs`).
2. Vérifier qu'aucun test existant ne fixait une fixture sur l'ancien comportement (`AF_Date`) par erreur
   — corriger les fixtures/tests concernés pour refléter `DO_Date`.
3. Test de non-régression : cas `FC2501193` (ou équivalent fixture) — `DateFacture` produite = `DO_Date`
   réel (18/07/2025 dans le cas réel), plus jamais `AF_Date`.
4. Rejeu réel (lecture seule) sur `GR_EMA_DISTRIBUTION` : régénérer le contrôle Excel d'une déclaration
   existante (ex. `TVA1-2026-01`, `EnCours`/rejouable sans écriture) et vérifier que la colonne
   « Date Facture » de `FC2501193` (et des autres cas de l'échantillon) affiche bien 2025.

## Livrables
- SQL corrigé (3 fragments), tests unitaires/fixtures ajustés.
- Preuve réelle (rejeu Excel) montrant la date corrigée sur au moins le cas `FC2501193`.

## Critères de validation
- `DateFacture` produite par le chemin règlement-first = `RT_ECHEANCE.DO_Date`, jamais `RT_AFFECTATION.AF_Date`.
- Aucune régression sur le chemin facture-first (déjà correct).
- Build back 0 erreur, tests verts.

## Risques / dépendances
- Aucune dépendance. Correction isolée à 3 lignes SQL, sans impact sur l'éligibilité/la sélection
  (uniquement un champ d'affichage aval).
- **Point à trancher par le PO, hors périmètre de cette TASK** : les déclarations déjà clôturées/déposées
  avec la mauvaise date (si applicable) doivent-elles être régénérées/recontrôlées ? Aucune donnée
  `DM_LGTVA` n'est modifiée par cette correction (elle ne s'applique qu'aux nouvelles sélections/exports) ;
  un ré-export d'une déclaration existante rejouerait le calcul avec la donnée corrigée si le modèle est
  reconstruit à la demande (à confirmer selon le mécanisme réel de régénération).
