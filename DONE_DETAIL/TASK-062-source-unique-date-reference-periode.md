# TASK-062 — Source unique de la date de période (`DateReference`) : espèce/rapproché cohérents entre interrogation Rapprochement et déclaration

## Contexte
Retour PO (12/07/2026) déclenché par un cas réel : le règlement **`RF26060064`**
(`MV_Type=3`, `MV_Point=1`, `MV_PointDate=30/01/2026`, `MV_Date=15/06/2026`, décaissement)
est **rapproché le 30/01/2026** donc attendu **déclarable en janvier 2026**. Or il n'apparaît
**pas** en janvier sur l'écran **Rapprochement bancaire** (interrogation) : il y sort en **juin**.

Diagnostic (preuve base `GR_EMA_DISTRIBUTION`) : la règle de période « quelle date situe un
règlement dans une période » est **dupliquée dans 3 fragments SQL divergents** :

| Chemin | Filtre de période actuel | Conforme règle PO ? |
|---|---|---|
| Sélection règlement-first (`SelectionnerAffectationsService`) | non-espèce : `MV_Point=1 AND MV_PointDate ∈ P` · espèce : `MV_Date ∈ P` (`<= @finInclude`) | ✅ oui (mais borne haute `<= finInclude` vs `< finExclude` ailleurs) |
| Déclaration facture-first (`SelectionExpliqueeEvaluator`) | gate éligibilité sur `MV_Point=1 AND MV_PointDate ∈ P` | ✅ oui (mais espèce → `MV_Point=0` → `NonRapproche`, cf. Risques) |
| **Écran Rapprochement (interrogation)** `DeclarationRepository.RapprochementFromWhere` l.295 | `MV_Date >= @debut AND MV_Date < @finExclude` **inconditionnel** | ❌ **non** — cause du bug `RF26060064` |

Règle métier confirmée par le PO (12/07/2026) :
- **Espèce** (`MV_Type = 0`) : jamais rapproché en base (vérifié : 88/88 à `MV_Point=0`) → axe **`MV_Date`**.
- **Chèque / traite / virement** (`MV_Type ∈ {1,2,3}`) rapproché (`MV_Point=1`) → axe **`MV_PointDate`**.
- Non-espèce **non rapproché** (`MV_Point=0`) : sur l'écran d'interrogation, **conservé** (axe `MV_Date`) —
  « c'est juste un écran pour vérifier la liste des règlements » (PO). Dans la **déclaration**, il
  reste **non déclarable** (gate `MV_Point=1`).

Exigence PO structurante : **« une seule source de données »**. Une seule *règle* de date, pas une
seule *requête* (interrogation = pivot règlement montrant tout ; déclaration = pivot facture gaté sur
le déclarable — deux requêtes légitimement distinctes). Miroir du pattern existant
`ReglementRapprochementRow.EstRapprocheBanque` (expression unique répliquée à l'identique + tests).

## Périmètre STRICT
- **Inclus** :
  1. **Définir une source unique** de la date de période, en 2 briques réutilisables :
     - **`DateReference`** (dans quelle période tombe un règlement), une seule expression couvrant les 3 cas :
       ```sql
       CASE WHEN M.MV_Type <> 0 AND M.MV_Point = 1 THEN M.MV_PointDate ELSE M.MV_Date END
       ```
       espèce → `MV_Date` ; non-espèce rapproché → `MV_PointDate` ; non-espèce non rapproché → `MV_Date`.
     - **`EstDeclarable`** (gate déclaration uniquement) : `MV_Type = 0 OR MV_Point = 1`.
  2. **Corriger l'écran Rapprochement** (`RapprochementFromWhere` l.295) : remplacer
     `M.MV_Date >= @debut AND M.MV_Date < @finExclude` par `DateReference ∈ [@debut, @finExclude)`.
     **Aucun gate** ajouté (le non-rapproché reste visible — décision PO).
  3. **Unifier** : `SelectionnerAffectationsService` et `SelectionExpliqueeService` référencent la
     **même** expression `DateReference` (période) ; la déclaration ajoute `EstDeclarable`.
  4. **Source de dérivation côté domaine** : exposer `DateReference` comme propriété calculée unique
     (ex. sur `ReglementRapprochementRow` / entité candidate) ; le SQL réplique cette expression à
     l'identique (comme `EstRapprocheBanque`).
  5. **Tests anti-divergence** : un test garantit que l'expression SQL et la dérivation C# rendent la
     **même** `DateReference` sur les 3 cas (espèce, non-espèce rapproché, non-espèce non rapproché).
- **Exclu** :
  - **Aucune écriture** (lecture seule stricte, `DT_Id` intouché — TASK-028).
  - Pas de refonte du pivot facture-first (TASK-050) : on aligne la **date**, on ne change pas l'axe facture.
  - Pas de recalcul TVA (TASK-043 / TASK-047 indépendants).
  - Pas de changement des autres filtres (origine, domaine, tiers, rapproché) — hors sujet.

## Objectif
```
Source unique (répliquée à l'identique SQL ⇄ domaine) :
  DateReference = CASE WHEN MV_Type <> 0 AND MV_Point = 1 THEN MV_PointDate ELSE MV_Date END
  EstDeclarable = (MV_Type = 0 OR MV_Point = 1)

Interrogation Rapprochement : WHERE … AND DateReference >= @debut AND DateReference < @finExclude
Déclaration                 : WHERE … AND DateReference >= @debut AND DateReference < @finExclude
                                        AND EstDeclarable            -- gate en plus

Résultat attendu : RF26060064 (MV_Type=3, MV_Point=1, MV_PointDate=30/01) visible en janvier
sur l'écran Rapprochement ET déclarable en janvier (déjà le cas côté déclaration).
```

## Étapes
1. **Domaine** : factoriser `DateReference` (et `EstDeclarable` pour le chemin déclaration) en une
   source unique documentée ; supprimer toute logique de date dupliquée qui pourrait diverger.
2. **Interrogation** `DeclarationRepository.RapprochementFromWhere` l.295 : remplacer le prédicat de
   période par `DateReference ∈ [@debut, @finExclude)` (appliqué à l'IDENTIQUE liste + `COUNT` via la
   constante partagée). Aucun gate ajouté.
3. **Sélection règlement-first** `SelectionnerAffectationsService` : remplacer les prédicats
   ad hoc (non-espèce `MV_PointDate` / espèce `MV_Date <= @finInclude`) par `DateReference` +
   `EstDeclarable`. **Homogénéiser la borne haute** sur `< @finExclude` (aujourd'hui l'espèce utilise
   `<= @finInclude` → risque de double comptage du dernier jour).
4. **Déclaration facture-first** `SelectionExpliqueeService` / `SelectionExpliqueeEvaluator` :
   aligner le filtre de période sur `DateReference` (remplace le filet large 3 branches l.120-126) ;
   conserver le gate `EstDeclarable`. **Statuer** sur le cas espèce (cf. Risques).
5. **Tests** : `Task036RapprochementProjectionTests` + tests de sélection —
   - `RF26060064`-like (`MV_Type=3, MV_Point=1, MV_PointDate` ∈ P, `MV_Date` hors P) → **présent** dans P
     (interrogation) et **déclarable** dans P ;
   - espèce (`MV_Type=0`, `MV_PointDate` NULL, `MV_Date` ∈ P) → présent dans P via `MV_Date` ;
   - non-espèce non rapproché (`MV_Point=0`) → **présent** dans l'interrogation (axe `MV_Date`),
     **non déclarable** ;
   - test anti-divergence SQL ⇄ C# sur `DateReference`.

## Livrables
- Écran Rapprochement : `RF26060064` (et tout règlement rapproché) apparaît dans le mois de sa
  **date de rapprochement**, pas de sa date de règlement ; non-rapprochés toujours visibles.
- Une **seule** expression `DateReference` référencée par les 3 chemins ; borne haute homogène
  (`< @finExclude`) partout ; `COUNT` interrogation cohérent avec la liste.
- Tests verts, dont l'anti-divergence SQL ⇄ domaine.
- `VERIFY/TASK-062_verify.md` : preuve réelle sur `GR_EMA_DISTRIBUTION` —
  - `RF26060064` visible en janvier 2026 sur l'écran Rapprochement (capture requête période janvier) ;
  - comptage des règlements dont `MV_Date` et `MV_PointDate` tombent dans des mois différents
    (mesure d'impact du changement d'axe) ;
  - un cas de chaque famille (espèce / non-espèce rapproché / non-espèce non rapproché) ;
  - contrôle lecture seule : aucun `UPDATE`, `DT_Id` inchangé.

## Critères de validation
- `DateReference` unique, répliquée à l'identique SQL ⇄ C# (aucune logique de date dupliquée divergente).
- Interrogation : rapproché situé par `MV_PointDate`, espèce et non-rapproché par `MV_Date` ;
  non-rapprochés conservés.
- Déclaration : même `DateReference` + gate `EstDeclarable` ; `RF26060064` déclarable en janvier.
- Borne haute de période homogène (`< @finExclude`) sur tous les chemins.
- Aucune écriture en base.

## Risques / dépendances
- **Séquencement (édition concurrente)** : touche `RapprochementFromWhere` (partagé liste+`COUNT`),
  `SelectionnerAffectationsService`, `SelectionExpliqueeService`/`Evaluator`. Ces fichiers sont aussi
  visés par **TASK-042** (colonnes/espèce, apparemment déjà implémentée) et **TASK-043** (TVA par
  règlement). **Séquencer après TASK-042** ; coordonner avec TASK-043 (mêmes `SELECT`/fragment).
- **⏸️ DÉCISION PO EN ATTENTE — espèce dans la déclaration facture-first (NE PAS IMPLÉMENTER cette
  branche sans arbitrage)** : aujourd'hui l'évaluateur `SelectionExpliqueeEvaluator` l.86-89 marque
  `NonRapproche` dès que `MV_Point != 1` → une espèce (`MV_Point=0`) serait **non éligible**. La règle
  PO dit « espèce = déclarable via `MV_Date` ». **Question ouverte, PO ne peut pas trancher pour
  l'instant (12/07/2026)** : l'espèce doit-elle être déclarable dans le chemin facture-first ?
  - Si **oui** : `EstDeclarable` (`MV_Type=0 OR MV_Point=1`) remplace le test `MV_Point=1` dans l'évaluateur.
  - Si **non** : laisser tel quel et documenter l'écart.
  → **Périmètre de cette TASK gelé sur l'interrogation + `DateReference`** ; le comportement espèce du
  facture-first reste **inchangé** tant que le PO n'a pas arbitré. Ne pas coder cette branche.
- **Changement d'axe = déplacement de lignes entre périodes** : des règlements rapprochés vont
  quitter le mois de leur `MV_Date` pour celui de leur `MV_PointDate`. Impact à **quantifier** au
  VERIFY (cf. Livrables) avant bascule ; vérifier qu'aucune déclaration déjà figée (`DT_Id`) n'est
  affectée (elle ne l'est pas : `DT_Id` gèle l'appartenance, TASK-028).
- **`MV_PointDate` NULL pour un `MV_Point=1`** (donnée incohérente) : `DateReference` retomberait sur
  `MV_Date`. Vérifier au VERIFY qu'aucun `MV_Point=1` n'a `MV_PointDate` NULL (sinon prévoir un
  `COALESCE(MV_PointDate, MV_Date)` explicite et le documenter).
