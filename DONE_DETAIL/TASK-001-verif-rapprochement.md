# TASK-001 — Vérifier la synchronisation du rapprochement (GRF ↔ Sage)

## Contexte
Application GRFN (WinForms, gestion règlement fournisseur → rapprochement → déclaration TVA Maroc). Source inaccessible ; logique reconstituée par décompilation. Analyse complète : `D:\_vibe\GRF\MODULE_DECLARATION_TVA.md`.

On veut construire un **nouveau module de déclaration TVA** qui détermine l'éligibilité d'un règlement **en local GRF** (option B), sans dépendre de l'appariement comptable avec Sage. Avant de basculer, il faut **prouver que les sources locales de rapprochement sont fiables et cohérentes avec Sage**, pour ne pas changer le résultat des clients ayant déjà déclaré avec l'ancien module.

### Comment l'ANCIEN module décide l'éligibilité (décaissement, chèque/traite/virement)
`DeclarationTvaController.GetDeclarationDecaissement` :
1. écriture comptable GRF (table `RT_HISTCOMPTA`) du règlement, via `ErpNo`,
2. appariée à Sage `F_ECRITUREC` (`cbMarq`),
3. considérée « rapprochée » si `ISNULL(EC_TresoPiece,'') <> ''` (date = `EC_DateRappro`),
4. filtre période : `JM_Date` entre début exercice et fin de période.

### Ce que le NOUVEAU module veut utiliser (local)
`RT_MOUVEMENT.MV_Point = 1` (rapproché) + `RT_MOUVEMENT.MV_PointDate` (date de rapprochement). Cf. requête existante `ReglementFournisseurRepository.GetAllDecaisseByRapprochementACompta`.

## Objectif
Quantifier l'écart entre les 3 sources de « rapproché + date de rapprochement » et statuer : peut-on faire confiance au local (`MV_Point`/`MV_PointDate`) comme seul critère ?

## Bases & accès (lecture seule)
- **Base prod client (EMA Distribution)** — client ayant déjà utilisé l'ancien module → contient de vraies déclarations à réconcilier (étape 5).
- Serveur/instance : **⚠️ à fournir** (probablement ≠ `.\sql2022` du dev).
- GRF : `GR_EMA_DISTRIBUTION`
- Sage : `NEW_EMA DISTRIBUTION` *(⚠️ nom exact à confirmer — espace ou underscore ; si espace, référencer en `[NEW_EMA DISTRIBUTION]`)*
- Credentials lecture seule : **⚠️ à fournir** (compte SQL pour GRF + compte Sage/OM).
- **Ne rien modifier** (aucun INSERT/UPDATE/DELETE).

> Config, pas de valeurs en dur : l'appli sera déployée chez d'autres clients (bases/serveur/credentials variables) — principe multi-client CDC.

## Sources à croiser
| Source | Table | Rapproché | Date rappro | Clé |
|---|---|---|---|---|
| GRF mouvement | `RT_MOUVEMENT` | `MV_Point` | `MV_PointDate` | `MV_Id`, `SO_Id`, `MV_Domaine` |
| **Local GRF (fiable)** | `RT_MOUVEMENT` | `MV_Point` | `MV_PointDate` | `MV_Id`, `SO_Id`, `MV_Domaine` |
| Sage compta | `[NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC` | `EC_TresoPiece <> ''` | `EC_DateRappro` | **`cbMarq`** |
| Image Sage dans GRF (= Sage) | `RT_HISTCOMPTA` | `HC_PieceTreso` non vide | `HC_DateRappro` | `MV_Id`, **`HC_No`** |

⚠️ **`RT_HISTCOMPTA.HC_PieceTreso`/`HC_DateRappro` sont une IMAGE de la compta Sage** (recopie), pas une source locale indépendante → même fiabilité que Sage. **La seule source locale fiable du rapprochement = `RT_MOUVEMENT.MV_Point`/`MV_PointDate`** (posée par le pointage sur extrait bancaire `RT_EXTRAITLIGNE`).

**Clé de jointure GRF↔Sage confirmée** (cbMarq 186/188 vs EC_No 145/188, cohérent avec le code) :
```sql
RT_HISTCOMPTA h JOIN [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC f ON f.cbMarq = h.HC_No
```
(sert à comparer l'image GRF avec Sage, pas comme source locale.)

## Étapes
1. **Cadrage société** : identifier le `SO_Id` GRF correspondant à `DISTRI_DEMO` (recouper via identifiant fiscal / `P_DOSSIER` côté Sage et la société GRF). Documenter le mapping.
2. **Clé de jointure GRF↔Sage** : confirmée = `F_ECRITUREC.cbMarq = RT_HISTCOMPTA.HC_No`. Contrôler le taux de correspondance sur le périmètre réel et lister les `HC_No` sans écriture Sage (écritures supprimées/hors exercice).
3. **Cohérence source locale fiable ↔ Sage** : sur les règlements fournisseurs (domaine `ReglementFournisseur`) ayant au moins une affectation (`RT_AFFECTATION`), comparer :
   - **Source locale fiable** : `RT_MOUVEMENT.MV_Point`/`MV_PointDate` (issue des extraits GRF).
   - **Source Sage** : `F_ECRITUREC.EC_TresoPiece`/`EC_DateRappro` (jointure via `RT_HISTCOMPTA.HC_No = cbMarq`).
   Lister les divergences : rapproché en local mais pas côté Sage (ou l'inverse) ; dates différentes.
   Vérifier au passage que l'image `RT_HISTCOMPTA.HC_PieceTreso`/`HC_DateRappro` reflète bien Sage (contrôle de la recopie), mais **ne pas l'utiliser comme source de décision**.
4. **Simulation d'éligibilité** : sur une période test (ex. un mois), produire deux ensembles de règlements éligibles au décaissement :
   - **Ancien** : critère Sage (`EC_TresoPiece<>''`, `EC_DateRappro` dans le mois).
   - **Nouveau** : critère local (`MV_Point=1`, `MV_PointDate` dans le mois).
   Calculer les écarts (présents dans l'un, absents de l'autre) avec exemples chiffrés.
5. **Réconciliation déclarations existantes** (si `RT_DeclarationTva`/`RT_LigneDeclarationTva` contiennent des données) : vérifier que les lignes déjà déclarées correspondent à des règlements rapprochés dans les sources locales.

## Livrables
- `VERIFY/TASK-001_verify.md` : rapport chiffré (nombres/%, exemples), clé de jointure retenue, tableau des divergences par source.
- Les requêtes SQL de contrôle réutilisables (dans le rapport ou un `.sql` joint).
- **Recommandation explicite** : local suffisant OUI/NON ; si NON, quel critère alternatif.

## Critères de validation
- Les 3 sources sont comparées sur un périmètre défini et documenté.
- La clé de jointure GRF↔Sage est justifiée avec son taux de correspondance.
- Écart ancien/nouveau chiffré sur au moins une période.
- Recommandation claire et argumentée.

## Risques / dépendances
- Base **prod réelle** (EMA Distribution) : volumétrie et rapprochements réels → l'étape 5 (réconciliation des déclarations GRFN existantes) devient exploitable (contrairement au dev vide). Attention à ne rien modifier sur une base client.
- Enums à mapper (`MV_Domaine`=ReglementFournisseur, `MV_Impaye`=NonImpaye, `MV_Compta`=EtatComptabilite) depuis `Tresorerie.Core.Enum`.
- Ne bloque pas le développement du module, mais **conditionne** le choix définitif de la source de rapprochement.
