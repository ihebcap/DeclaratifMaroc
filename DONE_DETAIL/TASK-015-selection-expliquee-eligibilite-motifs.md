# TASK-015 — Sélection expliquée : éligibilité avec motifs (aucune ligne silencieuse)

## Contexte
La règle n°1 du module (confiance client, cf. TASK-013 « Principe directeur ») : **aucune ligne ne
disparaît en silence**. Or la sélection actuelle (TASK-008, ✅ faite et vérifiée) filtre **tout dans le
`WHERE` SQL** — les lignes non éligibles **ne sortent jamais de la base**, donc sans motif. C'est
exactement le « saut silencieux » qui a fait perdre confiance dans l'ancienne appli (GRFN, §1bis/§2).

> ⚠️ **Ce n'est pas un défaut de TASK-008** : elle est correcte pour sa spécification (renvoyer les
> affectations **éligibles**). La transparence est une **exigence nouvelle** que son contrat ne couvre
> pas. Cette task l'ajoute **sans toucher** au code vérifié de TASK-008.

## Périmètre STRICT
- **Uniquement** : produire, par domaine et par période, l'**ensemble des affectations candidates**
  classées **Éligible** vs **Écartée + motif en clair**.
- **Exclu** : le calcul/ventilation (TASK-004, déjà fait), la persistance (TASK-012), le front
  (TASK-013). Ne pas modifier TASK-007/008.

## Objectif
Une brique (`Declaration.Selection`, en complément de l'existant) qui :
1. Sélectionne le **surensemble** des mouvements/affectations de la période **sans** les filtres
   d'éligibilité (garde uniquement le cadrage société + période + domaine).
2. **Évalue chaque critère en code** (plus dans le `WHERE`) et **tague** chaque ligne :
   - `Eligible`, **ou**
   - `Ecartee` + **motif** typé, en clair.
3. Motifs cibles (repris des filtres TASK-008 / algo GRFN §2) :

   | Motif | Origine | Message utilisateur (exemple) |
   |---|---|---|
   | `NonRapproche` | `MV_Point != 1` | « Règlement non rapproché » |
   | `HorsPeriode` | date rappro/règlement hors bornes | « Rapproché hors de la période » |
   | `DejaDeclare` | `AF.DT_Id IS NOT NULL` | « Déjà déclaré (période précédente) » |
   | `NonComptabilise` | `MV_Compta` ≠ comptabilisé | « Règlement non comptabilisé » |
   | `Annule` / `Impaye` | `MV_Annule` / `MV_Impaye` | « Règlement annulé / impayé » |
   | `NonAffecte` | pas d'affectation sur facture | « Rapproché mais non affecté à une facture » |
   | `FactureIntrouvable` | facture absente (worker OM) | « Facture introuvable dans Sage » |
   | `TaxeNonATaux` | `TypeTaux != Taux` | « Ligne de taxe non éligible (type ≠ taux) » |

## Contraintes techniques
- **Non-régression prouvée** : le sous-ensemble taggé `Eligible` doit **coïncider exactement** avec le
  résultat de la sélection TASK-008 (mêmes affectations) sur les données déjà validées → test de
  régression `Eligible(TASK-015) == Selection(TASK-008)`.
- **Motif = donnée structurée** (enum) **+ libellé clair** (pas un code brut à l'écran).
- `net10.0` (couche sélection existante) ; Dapper/SQL lecture seule GRF ; aucune écriture.
- **Attention volume** : le surensemble peut être large → requête bornée par période/domaine, et
  classification en flux (pas de matérialisation inutile). Compatible pagination serveur (TASK-012).
- Réutilise le mapping enums `GrfEnums` déjà en place.

## Étapes
1. Requêtes « surensemble » par domaine (société+période+domaine, sans filtres d'éligibilité).
2. Évaluateur de critères en code → `Eligible` / `Ecartee(motif)`.
3. Test de régression contre TASK-008 (l'éligible doit être identique).
4. Tests des motifs (fixtures couvrant chaque cas d'écart).
5. Exposer une sortie unique (éligibles + écartées) consommable par TASK-012.

## Livrables
- Brique de sélection expliquée (éligibles + écartées-avec-motif) dans `Declaration.Selection`.
- Tests : régression vs TASK-008 + un test par motif.
- `VERIFY/TASK-015_verify.md` : preuve `Eligible == TASK-008`, échantillon d'écartées avec motifs.

## Critères de validation
- Toute ligne de la période ressort **soit** éligible **soit** écartée-avec-motif ; **aucune** perdue.
- **Régression verte** : l'éligible reproduit exactement TASK-008.
- Chaque motif du tableau est produit et testé, avec un libellé clair.
- Lecture seule (aucune écriture base) ; TASK-007/008 inchangées.

## Risques / dépendances
- **Dépend de** : TASK-008 (✅, référence de non-régression) ; base prod `GR_EMA_DISTRIBUTION` pour
  valider sur données réelles (la logique + tests fixtures avancent sans base).
- **Bloque** : l'endpoint `GET .../lignes` de TASK-012 (qui doit exposer les écartées) et la vue
  « Écartées par le système » de TASK-013. ⇒ à faire **avant** de figer ces deux points.
- Motif `FactureIntrouvable` nécessite le worker OM (TASK-002, ✅) pour savoir si la facture existe côté
  Sage — cohérent avec le pipeline TASK-007.
