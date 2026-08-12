# TASK-213 — Désignation document : remplacer le placeholder fixe `<des>` par une colonne `F_COMPTET` configurable par société

Status: 🆕 à faire — **dépend de TASK-211** (table de config, non développée avant)
Priority: MEDIUM
Module: Declaration.Export.Xml / Declaration.Selection / Declaration.Application

> **Origine :** demande PO (09/08/2026), même session que TASK-211/TASK-212. Referme le point ouvert
> laissé par TASK-181 (24/07/2026) : *« la vraie source de désignation (`DM_LGTVA`/`LigneCandidate`,
> `Designation` toujours "" en amont) reste à trancher — ce placeholder n'est qu'un pis-aller »*.

## Contexte / cause actuelle (preuve de code)

[Declaration.Export.Xml/DeclarationXmlExporter.cs:65-68](../Declaration.Export.Xml/DeclarationXmlExporter.cs#L65) :

```csharp
// Placeholder fixe assumé (PO 24/07/2026, TASK-181) : Designation reste vide en amont
sb.Append($"<des>{EscapeXml("Achat marchandise")}</des>\r\n");
```

Le champ `Designation` n'est jamais renseigné en amont (`LigneCandidate.Designation` reste `""`
partout) — TASK-181 avait posé un littéral fixe en attendant l'arbitrage PO sur la vraie source.

## Périmètre STRICT

- **Inclus** :
  1. Lire la désignation depuis la colonne `F_COMPTET` configurée par société
     (`DM_PARAM_FCOMPTET_SOCIETE.ColonneDesignation`, posée par TASK-211), avec la même whitelist de
     validation de nom de colonne que TASK-212/`IdentiteFiscaleFournisseurConfig`.
  2. Câbler cette valeur dans `LigneCandidate.Designation` (actuellement toujours `""`) au même endroit
     que le code activité est déjà enrichi depuis Sage (`SelectionExpliqueeService`,
     `MapLignesCandidates`), pour éviter une requête Sage séparée si évitable — vérifier si une seule
     requête peut ramener `CT_APE`(remplacé par TASK-212)/désignation/IF/ICE en un seul aller-retour
     `F_COMPTET`, plutôt que 2 requêtes distinctes.
  3. Remplacer le littéral `"Achat marchandise"` de `DeclarationXmlExporter.cs:68` par la vraie valeur de
     `Designation` de la ligne.
  4. Comportement de repli si la colonne n'est pas configurée ou si la valeur Sage est vide pour un
     tiers donné : **conserver le littéral `"Achat marchandise"` comme repli**, pas une chaîne vide ni
     une exception — le tag `<des>` du dépôt DGI ne doit jamais être vide (contrainte legacy déjà
     assumée par TASK-181, à ne pas régresser).
- **Exclu** :
  - Pas de changement du format/schéma XML au-delà du contenu du tag `<des>`.
  - Pas de câblage du code activité (TASK-212, séparée) — mais si une requête Sage unique est retenue au
    point 2, coordonner l'ordre d'implémentation avec TASK-212 (probablement à livrer ensemble ou
    TASK-212 en premier).

## Livrables

- `LigneCandidate.Designation` porte la vraie désignation lue depuis Sage quand la colonne est
  configurée pour la société.
- `DeclarationXmlExporter` utilise cette valeur, avec repli sur le littéral existant si absente.
- Test automatisé : société avec colonne configurée (désignation réelle dans le XML généré), société
  sans configuration ou tiers sans valeur (repli au littéral, `<des>` jamais vide).

## Critères de validation

- Export XML de dépôt sur une société avec `ColonneDesignation` configurée et des factures réelles :
  `<des>` porte la désignation lue depuis Sage, pas le littéral fixe.
- Société sans configuration : comportement strictement identique à avant cette TASK (littéral
  `"Achat marchandise"`).
- `Declaration.Export.Xml.Tests` rejoués verts, dont le test existant qui vérifie le littéral de repli
  (à adapter pour le cas "sans configuration" plutôt que "toujours").

## Files

- [Declaration.Export.Xml/DeclarationXmlExporter.cs](../Declaration.Export.Xml/DeclarationXmlExporter.cs) (lignes 65-68).
- [Declaration.Selection/SelectionExpliqueeService.cs](../Declaration.Selection/SelectionExpliqueeService.cs).
- `Declaration.Application/Services/DeclarationWorkflowService.cs` (`MapLignesCandidates`, où `LigneCandidate.Designation` est construit).
