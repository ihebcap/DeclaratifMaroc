# TASK-048 — Sourcer l'ICE / l'IF fournisseur dynamiquement depuis `P_SOCIETE` (config par colonne)

> **Origine :** run valorisation `RafraichirValorisationAsync` → `[VALO] === Fin : 824 traitée(s),
> 1648 en erreur ===`. Les 1648 = **2 × 824** : chaque facture lève **ICE + IF** dans
> `ValiderAffectation`. Ce n'est **ni** le blocage OM (levé, TASK-045) **ni** le cache TASK-024
> (fix récent). C'est un **mauvais point de lecture de l'identité fiscale du tiers**.
> **Décision PO :** l'ICE et l'IF du fournisseur doivent être lus **dynamiquement**, via des
> **noms de colonnes configurés dans `P_SOCIETE`** — jamais figés en dur (chez un autre client
> les colonnes ERP diffèrent). N'a **rien à voir** avec la lecture OM des factures.

## Contexte (faits mesurés)

**Source actuelle (fausse).** Les 4 requêtes de sélection lisent l'identité fiscale sur le
**mouvement** (snapshot souvent vide) :
```sql
M.MV_Identifiant AS TiersIF,
M.MV_Ice         AS TiersICE,
```
- `Declaration.Selection/SelectionExpliqueeService.cs` (3 requêtes : ~l.101-102, 143-144, 185-186)
- `Declaration.Selection/SelectionnerAffectationsService.cs` (~l.121-122, 152-153, 182-183, 212-213)

Mappées ensuite dans `TiersInfo` (`SelectionExpliqueeEvaluator.cs:110-111`,
`SelectionnerAffectationsService.cs:100-101`), puis validées par `ValiderAffectation`
(`Declaration.Core/ConstructeurDeclaration.cs:228-244`) → `TIERS_SANS_ICE`/`ICE_INVALIDE`
+ `TIERS_SANS_IF`/`IF_INVALIDE`, **2 erreurs par facture**.

**Mécanique legacy (à répliquer).** `LigneDeclarationTvaEncaissementImportService`
(décompilé, `scratch/decompiled/.../ImportService/`) :
```csharp
// 1) Contrôle : la config DOIT être renseignée (sinon blocage explicite)
if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur))
    throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'ICE du tiers.");
if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur))
    throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'identifiant du tiers.");

// 2) Lecture ERP dynamique par NOMS de colonnes configurés dans P_SOCIETE
var iceTiers = _erpService.GetAllIceTiersToMaroc(
    societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur,          // nom colonne ICE
    societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur,  // nom colonne IF
    societe.ErpColumnNameNatureFournisseur,
    societe.ErpColumnNameCodeActiviteMarroc,
    societe.ErpColumnValueNumRegistreCommerceFournisseur);
```
`IErpTiersIce` porte `TiersIce` / `TiersIdentifiant`, rattaché **par code tiers** (jointure
`F_COMPTET` côté ERP Sage). L'ICE/IF vit sur le **maître tiers**, pas sur le mouvement.

**Séparation :** `P_SOCIETE.Ice` / `P_SOCIETE.Identifiant` (décompilé `Societe.cs` l.561 / l.167)
= identité **de la société déclarante** (en-tête déclaration). À ne pas confondre avec l'ICE/IF
**fournisseur** (par ligne), objet de cette TASK.

## Objectif
```
Entrée  : société (SO_Id) + tiers fournisseur (CT_Code) des affectations sélectionnées
Étape 1 : lire dans P_SOCIETE les NOMS de colonnes ICE/IF fournisseur (config, valeurs dynamiques)
Étape 2 : construire dynamiquement la lecture de l'ICE/IF depuis le maître tiers ERP (F_COMPTET)
          en injectant ces noms de colonnes, jointure par code tiers
Étape 3 : alimenter TiersInfo.Ice / .IdentifiantFiscal depuis cette source (plus MV_Ice/MV_Identifiant)
Sortie  : ValiderAffectation ne lève plus ICE/IF quand la donnée maître existe ;
          si la config P_SOCIETE est absente → blocage explicite (pas d'erreur silencieuse par facture)
```

## Contraintes
- **Dynamique obligatoire** : les noms de colonnes ICE/IF proviennent **exclusivement** de
  `P_SOCIETE` à l'exécution. Aucun nom de colonne ERP figé en dur (multi-clients).
- **Injection SQL** : les noms de colonnes venant de config sont interpolés dans le SQL →
  **valider/quoter** l'identifiant (whitelist `[A-Za-z0-9_]`, crochets `[ ]`) avant interpolation.
  Les **valeurs** (codes tiers) restent paramétrées.
- **Lecture seule stricte** : aucune écriture GRFN ni Sage. Jointure cross-base tolérée en
  lecture (même schéma que la sélection actuelle).
- **Config absente** : reproduire le blocage explicite legacy (message clair « colonne ICE/IF
  du tiers non configurée dans P_SOCIETE ») plutôt que 2 alertes/facture.
- **Découverte à résoudre** : le **nom SQL réel** des colonnes de config dans `P_SOCIETE`
  (le mapping ORM legacy `DeclarationTvaEncaissementErpColumnName*` → colonne physique) — à
  confirmer via le repository legacy décompilé ou `INFORMATION_SCHEMA` (schéma produit, identique
  chez tous les clients ; seule la **valeur** varie).

## Livrables
- Lecture de la config `P_SOCIETE` (noms de colonnes ICE/IF fournisseur) — 1 requête par société.
- Sourcing dynamique de l'ICE/IF tiers depuis `F_COMPTET` (helper dédié), remplaçant
  `MV_Ice`/`MV_Identifiant` dans les 4 requêtes de sélection.
- Garde-fou anti-injection sur les noms de colonnes (whitelist).
- Blocage explicite si config manquante.
- `VERIFY/TASK-048_verify.md` : preuve réelle sur `.\sql2022`/`GR_EMA_DISTRIBUTION` — config
  P_SOCIETE lue, ICE/IF fournisseur peuplés, chute du nombre d'alertes ICE/IF, cas config-absente.

## Critères de validation
- ICE/IF fournisseur proviennent des colonnes **nommées par `P_SOCIETE`**, jamais figées.
- Sur données réelles : alertes `TIERS_SANS_ICE`/`_IF` reflètent la **vraie** absence de donnée
  maître (plus le faux 2×824 dû à `MV_*` vides).
- Config P_SOCIETE absente → blocage explicite, pas d'erreur par facture.
- Anti-injection sur les noms de colonnes prouvé (identifiant invalide rejeté).
- Lecture seule stricte ; build + tests verts.

## Dépendances / séquencement
- **Indépendant** de la lecture OM (TASK-045/046/047) et du cache TASK-024.
- N'impacte pas le verrou TASK-028 (`DT_Id` lu, jamais écrit).
