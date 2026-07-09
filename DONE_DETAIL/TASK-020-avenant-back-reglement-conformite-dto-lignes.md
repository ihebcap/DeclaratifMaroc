# TASK-020 — Avenant back : n° de règlement + conformité IF/ICE dans le DTO des lignes (Gap A de TASK-019)

## Contexte
TASK-019 (poste de travail piloté par le règlement, 4 interrogations) doit se brancher **directement sur
l'API réelle**. Vérification du code (08/07/2026) : le socle **affectation** existe déjà dans la
sélection — `SelectionnerAffectationsService` lit `A.AF_Montant` (montant affecté partiel),
`M.MV_Numero` (n° de règlement / OM), `E.DO_Numero` (facture), `M.MV_Point = Oui` (rapproché).

**Le manque (Gap A) :** l'entité `LigneCandidate` (`Declaration.Application/Entities/WorkflowEntities.cs`)
**ne conserve pas** le n° de règlement au moment d'aplatir les affectations en lignes — elle ne garde
que `NumeroFacture`. Conséquence : le front **ne peut pas regrouper les lignes par règlement**, ce qui
rend infaisables les interrogations *Rapprochement* et *Affectation* de TASK-019. De plus les valeurs
brutes IF/ICE (`TiersIdentifiantFiscal`, `TiersICE`) sont présentes mais **aucun état de conformité**
n'est exposé pour l'interrogation *Conformité IF/ICE*.

> Périmètre volontairement **étroit** : c'est un **enrichissement** de snapshot + DTO, **pas** un
> reshaping du modèle ni une modif de la logique d'éligibilité/sélection. La face « reportées »
> (surfacer les non-rapprochées) est le **Gap B**, hors de cette task (Track B, à cadrer séparément).

## Périmètre STRICT
- **Uniquement le back** `Declaration.*` : entité `LigneCandidate`, mapping affectation→ligne, persistance
  et lecture repository, exposition dans le DTO `GET /declarations/{id}/lignes`.
- **Exclu** : toute modif de la **sélection** / éligibilité (SQL `MV_Point`, périmètre des candidates),
  du calcul de ventilation/montants (worker OM, orchestrateur), du front. Aucun changement du sens des
  autres champs. Pas de nouvelle table.

## Objectif
Exposer, **par ligne**, de quoi regrouper par règlement et juger la conformité fournisseur — sans rien
changer d'autre.

### 1. N° de règlement sur la ligne
- Ajouter `NumeroRapprochement` (string) à `LigneCandidate` — le n° de règlement / OM (`M.MV_Numero`),
  déjà porté par l'affectation en amont (`AffectationRow.NumeroRapprochement`).
- Le **renseigner dans les 3 branches** de construction des lignes de
  `DeclarationWorkflowService` (éligibles, non éligibles, taxes) à partir de l'affectation source.
- Le **persister** (`SaveLignesCandidatesAsync`) et le **relire** (`GetLignesAsync` + projection).
- L'**exposer** dans le DTO `GET /lignes` (il l'est automatiquement, `Items = lignes`).

### 2. État de conformité IF/ICE — ⏸️ 2ᵉ VAGUE (reporté, décision PO 08/07/2026)
> Le premier résultat n'inclut PAS l'interrogation Conformité (décision 2 de TASK-019). Cette section
> reste spécifiée mais **n'est pas livrée dans ce premier tour** : le livrable prioritaire de TASK-020
> est le **§1 (n° de règlement)**, qui débloque le regroupement. Le flag IF/ICE ci-dessous est traité
> quand on attaque la 2ᵉ vague.
- Ajouter un champ **calculé** d'état de conformité fournisseur (ex. `EtatConformite` :
  `Conforme` / `IfManquant` / `IceManquant` / `Format Invalide`), dérivé de `TiersIdentifiantFiscal` /
  `TiersICE` selon les règles DGI **déjà appliquées à l'export XML** (TASK-011 — réutiliser la même
  validation IF/ICE, ne pas en réinventer une).
- Exposé dans le DTO des lignes ; aucune écriture (lecture seule, cohérent avec l'interrogation
  *Conformité* de TASK-019).

## Contraintes techniques
- **Réutiliser** la validation IF/ICE de TASK-011 (Export XML) — source unique de vérité pour la
  conformité, pas de règle divergente.
- Migration/compat : les déclarations déjà figées sans `NumeroRapprochement` → valeur vide tolérée
  (pas de crash ; le front gère l'absence). Pas de recalcul rétroactif imposé.
- Aucune régression du contrat existant : champs ajoutés, aucun champ retiré/renommé.
- `net10.0`, conventions Clean Architecture GRC_WEB existantes ; tests unitaires du mapping.
- **Transparence (TASK-013) intacte** : on n'enlève rien, on n'agrège rien silencieusement.

## Étapes
1. `LigneCandidate` : ajouter `NumeroRapprochement` (+ champ conformité IF/ICE).
2. `DeclarationWorkflowService` : renseigner `NumeroRapprochement` depuis l'affectation dans les
   **3 branches** de création de lignes.
3. Repository : persistance + lecture + projection incluant le nouveau champ.
4. Conformité IF/ICE : brancher la validation TASK-011 en champ calculé exposé au DTO.
5. Tests : mapping affectation→ligne conserve le n° de règlement ; conformité cohérente avec l'XML ;
   plusieurs lignes d'un même règlement partagent le même `NumeroRapprochement` (regroupable).

## Livrables
- Back : `LigneCandidate` + mapping + persistance/lecture + DTO enrichis.
- Tests unitaires (mapping règlement, conformité IF/ICE).
- `VERIFY/TASK-020_verify.md` : preuve sur données réelles (`GR_EMA_DISTRIBUTION` / `SO_Id=1`) qu'un
  même règlement `MV_Numero` regroupe bien N lignes-factures, et que l'état de conformité correspond à
  ce que l'export XML accepte/rejette.

## Critères de validation
- `GET /lignes` renvoie `NumeroRapprochement` par ligne ; **plusieurs lignes d'un même règlement**
  partagent la même valeur (regroupement possible côté front).
- Le montant par ligne reste le **montant affecté partiel** (aucune régression sur `HT/TVA`).
- État de conformité IF/ICE exposé, **cohérent avec la validation XML TASK-011**.
- Aucun champ existant modifié dans son sens ; aucune régression sur les endpoints/tests actuels.
- Build + tests verts.

## Risques / dépendances
- **Débloque TASK-019 (front)** : cette task est un **prérequis dur** des interrogations Rappro/Affect/
  IF-ICE et de la face « je déclare ».
- **Ne débloque PAS la face « reportées »** = **Gap B** (surfacer les non-rapprochées, couche sélection,
  Track B) — à cadrer séparément avec le PO (profondeur de report, sort d'une reportée jamais rapprochée).
- **Vérifier que l'affectation transporte bien `NumeroRapprochement` jusqu'au workflow** : présent dans
  `AffectationRow` (sélection) ; confirmer qu'il n'est pas perdu dans le modèle intermédiaire
  `Affectation` avant le mapping en `LigneCandidate`. Si perdu, l'ajouter au modèle (reste léger).
- **Données figées existantes** : `NumeroRapprochement` vide pour l'historique — comportement toléré, à
  documenter dans le VERIFY.
