# TASK-212 — Code activité : remplacer `F_COMPTET.CT_APE` (en dur) par une colonne configurable par société

Status: 🆕 à faire — **dépend de TASK-211** (table de config, non développée avant)
Priority: MEDIUM
Module: Declaration.Selection / Declaration.Core

> **Origine :** demande PO (09/08/2026), même session que TASK-211/TASK-213.

## Contexte / cause actuelle (preuve de code)

Aujourd'hui, le code activité (niveau 2 de la cascade `CodeActiviteResolver`, cf.
[Declaration.Core/CodeActiviteResolver.cs:9-10](../Declaration.Core/CodeActiviteResolver.cs#L9)) lit
la colonne `F_COMPTET.CT_APE`, **codée en dur** dans
[Declaration.Selection/SelectionExpliqueeService.cs:516-521](../Declaration.Selection/SelectionExpliqueeService.cs#L516) :

```sql
SELECT CT_Num, {identiteConfig.SelectIdentifiantExpression("F_COMPTET")} AS TiersIF,
       {identiteConfig.SelectIceExpression("F_COMPTET")} AS TiersICE,
       CT_APE
FROM F_COMPTET WHERE CT_Num IN @codes
```

> **Rappel (ne pas rouvrir un débat déjà tranché) :** TASK-171 avait exploré un mécanisme différent
> (colonne Sage configurable côté **tiers**, table winform `P_SOCIETECODEACTIVITETIERS`) et le PO avait
> tranché de le **retirer** (dépendance à une table interdite à modifier, faisait doublon avec `CT_APE`).
> Cette TASK-212 est un cas différent : rendre `CT_APE` lui-même configurable (nom de colonne
> `F_COMPTET`), pas réintroduire le mécanisme retiré. Ne pas confondre les deux lors de la revue.

## Périmètre STRICT

- **Inclus** :
  1. Remplacer la constante `CT_APE` par une colonne dont le nom est lu depuis la config posée par
     TASK-211 (`DM_PARAM_FCOMPTET_SOCIETE.ColonneCodeActivite`), avec la même validation whitelist que
     `IdentiteFiscaleFournisseurConfig` (nom de colonne interpolé seulement après validation).
  2. Si la colonne n'est pas configurée pour une société (valeur `NULL`/vide) : comportement de repli
     explicite à trancher — recommandation architecte : retomber sur `""` (niveau 4 de la cascade,
     cohérent avec le principe déjà en place « jamais de valeur inventée », TASK-161 point 3), **pas**
     une exception bloquante (le code activité n'est jamais bloquant pour le dépôt DGI).
  3. Mettre à jour le commentaire de cascade dans `CodeActiviteResolver.cs` (niveau 2 = colonne
     configurable, plus `CT_APE` en dur).
- **Exclu** :
  - Pas de changement de l'ordre de la cascade (surcharge manuelle > colonne configurée > vide).
  - Pas de câblage de la désignation document (TASK-213, séparée).
  - Pas de réintroduction du niveau « défaut par tiers » retiré par TASK-171/TASK-179.

## Livrables

- `SelectionExpliqueeService.EnrichirTiersDepuisSageAsync` (ou équivalent) lit la colonne configurée par
  société au lieu de `CT_APE` en dur.
- Test automatisé couvrant : société avec colonne configurée (résolution correcte), société sans
  configuration (repli `""`, pas de crash).

## Critères de validation

- Sur une société avec `ColonneCodeActivite` configurée à un nom de colonne réel existant côté Sage
  (ex. `CT_APE` lui-même, pour test de non-régression), le code activité résolu est identique à
  l'ancien comportement.
- Société sans configuration : aucune exception, code activité résolu à `""` comme avant l'introduction
  d'un `CT_APE` invalide.
- `Declaration.Core.Tests`/`Declaration.Selection.Tests` rejoués verts (`CodeActiviteResolverTests`,
  `Task161CodeActiviteCascadeTests`).

## Files

- [Declaration.Selection/SelectionExpliqueeService.cs](../Declaration.Selection/SelectionExpliqueeService.cs) (lignes 516-521).
- [Declaration.Core/CodeActiviteResolver.cs](../Declaration.Core/CodeActiviteResolver.cs).
- [Declaration.Selection/IdentiteFiscaleFournisseurConfig.cs](../Declaration.Selection/IdentiteFiscaleFournisseurConfig.cs) — patron de validation à répliquer (ou étendre pour couvrir 3 colonnes au lieu de 2).
