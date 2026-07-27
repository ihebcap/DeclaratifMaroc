# TASK-177 — Une incohérence validée par le PO reste signalée comme anomalie BLOQUANTE au contrôle ④

Status: 🆕 à faire
Priority: HIGH (contredit directement l'objet même de TASK-078 : une validation tracée doit arrêter le
signalement — ici elle ne l'arrête qu'à moitié)
Risk: LOW à corriger (ajout d'une condition déjà utilisée ailleurs dans le même fichier)
Module: Declaration.Application

> **Origine :** signalement PO (24/07/2026), sur la base de **production** : *« j'ai validé deux
> factures mais toujours il apparaisse comme anomalie »* — cite précisément
> `FC2501717`/`RF26040040` et `FC2501667`/`RF26030075`, avec le message
> **« Ligne en anomalie de recalcul (...) »**, sous le contrôle **« Absence d'anomalies bloquantes »**
> (`status: error`, visible dans les logs console fournis).

## Constat (preuve de code)

Ces deux pièces sont **exactement** les cas déjà connus et documentés (TASK-082, `DOCS/AUDIT-TASK-143/
01-TVA1-2026-01.md`) : incohérence Sage `Σ(HT+TVA+Parafiscale) ≠ TTC`, motif porté par
`OrchestrateurDeclaration.cs`. Le mécanisme de validation explicite d'une incohérence existe déjà
(**TASK-078**, colonnes `IncoherenceValidee`/`Par`/`Le` sur `DM_LGTVA`, migration
`007_DM_LGTVA_Incoherence_Validee.sql`) — mais il n'est respecté que par **une partie** des chemins qui
signalent une anomalie :

1. `RevaliderLignesFigeesAsync` (`DeclarationWorkflowService.cs:356-515`, alimente `GET {id}/lignes` et
   l'écran ② Affectations) — **respecte** bien la validation : ligne 465,
   `if (l.IncoherenceValidee) continue;` avant de générer l'alerte `LIGNE_FIGEE_A_REVERIFIER`.
2. `GetCheckupAsync` (`DeclarationWorkflowService.cs:985-1129`, alimente les contrôles « Cohérence des
   totaux déclarés » / **« Absence d'anomalies bloquantes »** de l'écran ③ Vérifier & Intégrer — les
   deux contrôles visibles dans les logs PO) — à la ligne 1116,
   `foreach (var l in integrees.Where(l => !string.IsNullOrEmpty(l.MotifRejet)))` génère l'alerte
   **`FACTURE_NON_VENTILEE`** (niveau **Error**, donc bloquante) pour toute ligne portant un
   `MotifRejet` non vide — **sans jamais tester `l.IncoherenceValidee`**. C'est exactement ce chemin qui
   produit le message `"Ligne en anomalie de recalcul (facture {NumeroFacture}, règlement
   {NumeroRapprochement}) : {MotifRejet}"` cité mot pour mot par le PO.
3. Seul point d'entrée existant vers `ValiderIncoherenceLigneAsync` (qui pose `IncoherenceValidee=1`) :
   le bouton de `AffectationsDrill.tsx:443` (écran ② Affectations, `POST {id}/lignes/valider-incoherence`).
   Le PO a donc bien validé les deux lignes **à l'endroit prévu pour ça** — la validation est
   correctement tracée en base (`IncoherenceValidee=1`, `Par`, `Le`), mais `GetCheckupAsync` l'ignore
   totalement et continue de bloquer la clôture pour ces deux mêmes pièces.

**Conséquence exacte du signalement PO** : la validation TASK-078 fonctionne à moitié — elle fait bien
taire l'avertissement de l'écran ②, mais le contrôle bloquant de l'écran ③/④ reste rouge indéfiniment
pour toute ligne ainsi validée, rendant la fonctionnalité de validation **inutile pour son objectif
premier** (débloquer la clôture après acceptation explicite de l'incohérence).

## Objectif

Dans `GetCheckupAsync` (`DeclarationWorkflowService.cs:1116`), exclure du déclenchement de l'alerte
`FACTURE_NON_VENTILEE` toute ligne où `IncoherenceValidee == true` — même garde que celle déjà en place
dans `RevaliderLignesFigeesAsync` (ligne 465), pour que les deux chemins de signalement restent
cohérents entre eux. Une ligne validée doit :
- rester non valorisée (aucune valeur fabriquée, aucun changement de `HT`/`TTC`/totaux — inchangé) ;
- ne plus être comptée comme anomalie **bloquante** pour la clôture ;
- idéalement, rester visible quelque part sous une forme non bloquante (Info/Warning, traçant qui/quand
  a validé) plutôt que de disparaître silencieusement du contrôle — à trancher avec le PO si cette
  visibilité de traçabilité est jugée nécessaire à cet écran (elle existe déjà côté ② via
  `IncoherenceValideePar`/`Le`, potentiellement suffisante).

## Garde-fous

- Ne toucher que la condition de déclenchement de l'alerte — **aucun changement de valeur** (`HT`,
  `TTC`, `MotifRejet` lui-même) ni de `Etat` de la ligne.
- Ne pas retirer la revalidation par `RevaliderLignesFigeesAsync` déjà appelée en fin de
  `GetCheckupAsync` (lignes 1138-1139) — elle reste la source des alertes Warning de niveau ②, ce
  correctif ne concerne que le bloc `integrees`/`FACTURE_NON_VENTILEE` (Error) directement au-dessus.
- Vérifier s'il existe d'autres emplacements générant une alerte à partir de `MotifRejet` sans tester
  `IncoherenceValidee` (recherche `MotifRejet` sur tout `DeclarationWorkflowService.cs`/
  `OrchestrateurDeclaration.cs`) — corriger tous les emplacements trouvés, pas seulement celui
  identifié ici, pour éviter de laisser un 3ᵉ chemin incohérent.

## Files

- [Declaration.Application/Services/DeclarationWorkflowService.cs:1116-1129](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`GetCheckupAsync`, boucle `FACTURE_NON_VENTILEE` — correctif principal).
- [Declaration.Application/Services/DeclarationWorkflowService.cs:460-465](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`RevaliderLignesFigeesAsync` — référence du pattern déjà correct, à répliquer).
- [declaration-tva-web/src/AffectationsDrill.tsx:443](../declaration-tva-web/src/AffectationsDrill.tsx) (bouton de validation existant, aucune modification attendue).
- [Declaration.Infrastructure/SQL/007_DM_LGTVA_Incoherence_Validee.sql](../Declaration.Infrastructure/SQL/007_DM_LGTVA_Incoherence_Validee.sql) (schéma existant, aucune modification attendue).

## Validation

- [ ] Build back OK, `dotnet test Declaration.Orchestration.Tests` (couvre déjà TASK-078/082/102 sur ces
      mêmes pièces `FC2501717`/`FC2501667`, cf. `Task102NumeroReglementAnomaliesFactureTests.cs`,
      `Task082LigneExclueDesLeFigeageTests.cs`) — non-régression + nouveau cas validé.
- [ ] Nouveau test : une ligne avec `MotifRejet` non vide ET `IncoherenceValidee=true` → `GetCheckupAsync`
      ne génère PAS d'alerte `FACTURE_NON_VENTILEE` pour cette ligne ; une ligne identique avec
      `IncoherenceValidee=false` continue de la générer (non-régression du cas non validé).
- [ ] Test réel (base de test, pas la prod) : valider une incohérence via l'écran ② → le contrôle
      « Absence d'anomalies bloquantes » de l'écran ③ passe au vert pour cette ligne, sans changement de
      montant/total observé.
- [ ] Confirmation PO sur les deux cas réels cités (`FC2501717`/`RF26040040`,
      `FC2501667`/`RF26030075`) : après déploiement du correctif, revalider si nécessaire (la validation
      déjà posée en base doit suffire, sans qu'il ait à recliquer) — à vérifier, une validation déjà
      tracée avant le correctif doit rester valable après.

## Dépendances / risques

- Aucune dépendance technique. Risque de régression faible : réplique une condition déjà éprouvée
  ailleurs dans le même service.
- Point à trancher par le PO (§Objectif, dernier point) : faut-il une trace visible de la validation à
  l'écran ③/④, ou la traçabilité déjà présente à l'écran ② (`IncoherenceValideePar`/`Le`) suffit-elle ?
