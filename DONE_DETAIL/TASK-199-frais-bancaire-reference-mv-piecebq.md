# TASK-199 — Frais bancaire : colonne Référence vide, utiliser RT_PREVISIONNELLE.MV_PieceBq

Status: ✅ **Terminée** (05/08/2026)
Priority: HIGH (colonne `Référence` = celle déclarée dans le fichier XML de dépôt, `<num>`)
Risk: LOW (ajout d'une colonne SQL + un champ, aucune règle de calcul touchée)
Module: Declaration.Selection

> **Origine :** règle métier communiquée par le PO (05/08/2026) : la colonne Référence des lignes
> Frais bancaire est vide — c'est cette colonne qui alimente le numéro de pièce déclaré dans le
> fichier XML (`<num>`, `DeclarationXmlExporter.cs:64`). Le PO demande d'utiliser
> `RT_PREVISIONNELLE.MV_PieceBq` pour la renseigner.

## Constat (preuve de code)

`SelectionnerFraisBancaireAsync` (`SelectionExpliqueeService.cs:116-189`) construit chaque
`AffectationADeclarer` de type `FraisBancaire` sans jamais renseigner `Reference` (le champ existe
sur le modèle, `Declaration.Core/Model.cs:38` — `public string? Reference { get; set; }` — mais
n'est affecté nulle part dans ce bloc, lignes 164-188).

`GetFraisBancaireSql` (lignes 191-214) ne sélectionne pas non plus `MV_PieceBq` — colonne absente
du `SELECT` et de `FraisBancaireRow` (lignes 94-104).

Conséquence : `ligne.NumeroFacture` (= `cleUnique`, `"{MV_Numero}-{PT_Id}"`, un identifiant
technique interne) sert de `<num>` dans l'export XML pour ces lignes — pas une vraie référence de
pièce bancaire — tandis que `Reference` reste vide partout où elle est affichée séparément
(NumeroFacture et Reference sont deux champs distincts, cf. `DeclarationXmlExporter.cs:64` utilise
`ligne.NumeroFacture`, pas `ligne.Reference`, donc le vrai souci pratique remonté par le PO est
l'écran/export qui affiche `Reference` en colonne dédiée — à vérifier lequel exactement pendant le
développement, probablement l'export Excel « Lignes à déclarer », TASK-187/189 ayant déjà câblé
`Reference` pour les autres sources).

## Objectif

1. Ajouter `P.MV_PieceBq` au `SELECT` de `GetFraisBancaireSql` (`SelectionExpliqueeService.cs:191-214`).
2. Ajouter `public string? MV_PieceBq { get; set; }` à `FraisBancaireRow`.
3. Renseigner `Reference = r.MV_PieceBq ?? ""` sur l'`AffectationADeclarer` construit ligne
   164-188 (même style que les autres champs déjà mappés, ex. `Tiers.Numero = r.BanqueCode ?? ""`).

## Garde-fous

- Ne pas toucher à `NumeroFacture`/`cleUnique` (l'identifiant technique anti-doublon, TASK-080) —
  uniquement ajouter `Reference`, un champ d'affichage/export distinct.
- Vérifier concrètement, pendant le développement, quel écran/export affichait `Reference` vide
  pour confirmer que ce fix couvre bien le cas signalé par le PO (grep `\.Reference\b` dans
  `Declaration.Export.Excel` et le front) — ne pas supposer, constater.

## Files

- [Declaration.Selection/SelectionExpliqueeService.cs:94-104, 191-214](../Declaration.Selection/SelectionExpliqueeService.cs) (`FraisBancaireRow`, `GetFraisBancaireSql`)
- [Declaration.Selection/SelectionExpliqueeService.cs:164-188](../Declaration.Selection/SelectionExpliqueeService.cs#L164-L188) (construction `AffectationADeclarer`)
- [Declaration.Core/Model.cs:38](../Declaration.Core/Model.cs#L38) (`Reference`, champ existant, aucun changement de modèle nécessaire)

## Validation

- [x] La colonne Référence d'une ligne Frais bancaire affiche `MV_PieceBq` (vérifié sur un cas réel
      EMA, écran/export où le PO l'a constatée vide).
- [x] Si `MV_PieceBq` est NULL/vide en base pour une ligne donnée, `Reference` reste `""` (jamais de
      valeur inventée — règle n°1 du projet), pas d'exception.
- [x] Non-régression sur les autres sources (Decaissement/Encaissement/Depense) — leur `Reference`
      reste inchangée.
- [x] Build + tests unitaires OK.

## Dépendances / risques

- Aucune dépendance bloquante. Risque faible, fix localisé à une seule méthode.
