# TASK-182 — Export Excel : libellés trompeurs du bloc « Contrôle d'équilibre » (feuille « Détail TVA »)

## Contexte
Suite à la revue de TASK-180, le PO a demandé des explications sur le bloc « Contrôle d'équilibre » de
la feuille « Détail TVA » (export de contrôle, capture d'écran fournie en tout début d'échange :
« Total Montant Affecté » 2 524 369,90 / « Total Déclaré TTC » 2 894 410,76 / « Résidu Non TVA »
−370 040,86). Décision PO après explication : **corriger uniquement les libellés**, pas le calcul.

## Constat (lecture code, architecte)
`CreerFeuilleDetailTva` (export de contrôle, `Exporter.cs:293-321`) et `CreerFeuilleRecap` (export de
dépôt, `Exporter.cs:76-155`) affichent le **même jeu de libellés** pour `ControleEquilibre`, mais les
deux chemins de calcul qui remplissent cet objet n'ont pas la même sémantique :

- **Export de dépôt** (`ConstructeurDeclaration.cs:18/107/206`) : `TotalMontantAffecte` = Σ
  `affectation.MontantAffecte` (le **vrai** montant du règlement affecté à la facture) et
  `ResiduExplique` (`:19/111/208`) = une vraie explication calculée (prorata × taxes parafiscales +
  escompte). Les libellés sont **corrects** dans ce chemin.
- **Export de contrôle** (`DeclarationWorkflowService.ConstruireModeleControleAsync`, ~ligne 1418-1423)
  : `TotalMontantAffecte = lignesRecap.Sum(l => l.HT)` — c'est en réalité un **Total HT**, pas un
  montant affecté (le vrai montant réglé n'est même pas sommé ici) — et `ResiduExplique = 0m` **codé en
  dur**, jamais calculé. Conséquence directe : `ResiduNonTva` (= `TotalMontantAffecte − TotalDeclareTtc`
  = `ΣHT − ΣTTC`) vaut systématiquement **−ΣTVA**, et `ResiduInexplique` (= `ResiduNonTva −
  ResiduExplique`) vaut donc toujours exactement `ResiduNonTva` puisque `ResiduExplique` est toujours 0.
  Le nombre affiché est réel et correctement calculé (−370 040,86 = 2 524 369,90 − 2 894 410,76 = bien
  −ΣTVA de la déclaration), mais les libellés hérités du chemin de dépôt (« Montant Affecté », « Résidu
  Non TVA ») laissent croire à un contrôle de cohérence réglement↔déclaration alors qu'il s'agit
  seulement d'une restitution de ΣHT et −ΣTVA. Seules 3 lignes sont d'ailleurs affichées côté contrôle
  (`CreerFeuilleDetailTva` n'écrit pas « Résidu Expliqué »/« Résidu Inexpliqué », contrairement à
  `CreerFeuilleRecap`) — cohérent avec le fait que ces deux valeurs n'auraient aucun sens ici.
- **Aucune alerte générée à partir de cette confusion** : `ConstruireModeleControleAsync` ne pousse
  aucune `Alerte` de type `EQUILIBRE_RESIDU_INEXPLIQUE` (contrairement à `ConstructeurDeclaration.cs`,
  qui la déclenche si le résidu dépasse la tolérance) — donc pas de faux positif fonctionnel, uniquement
  un problème d'affichage/vocabulaire.

## Décision PO
Ne pas recalculer un vrai `TotalMontantAffecte`/`ResiduExplique` côté contrôle (changement plus large,
hors périmètre demandé) — **juste renommer les 2 libellés concernés**, uniquement dans
`CreerFeuilleDetailTva` (export de contrôle). `CreerFeuilleRecap` (export de dépôt) **n'est pas
concerné** : ses valeurs et libellés sont déjà corrects, ne pas y toucher.

## Objectif
Dans `Declaration.Export.Excel/Exporter.cs::CreerFeuilleDetailTva` (lignes ~311 et ~317) uniquement :
- `"Total Montant Affecté"` → `"Total HT"` (reflète ce que la valeur représente réellement dans ce
  chemin : `lignesRecap.Sum(l => l.HT)`).
- `"Résidu Non TVA"` → `"Écart HT − TTC (= −Total TVA)"` (ou libellé équivalent explicite sur le signe —
  la valeur reste négative, ne pas inverser le signe affiché, juste nommer correctement ce qu'elle est).
- `"Total Déclaré TTC"` : **inchangé**, déjà correct (= Σ TTC des lignes retenues).

Aucune autre ligne, aucune autre feuille, aucun calcul touché.

## Périmètre STRICT
- **Inclus** : `Declaration.Export.Excel/Exporter.cs::CreerFeuilleDetailTva` — 2 chaînes de libellé
  uniquement.
- **Exclus** : `CreerFeuilleRecap` (export de dépôt, libellés déjà corrects) ; tout changement de
  `ControleEquilibre`/`ConstruireModeleControleAsync`/`ConstructeurDeclaration.cs` (aucun recalcul,
  décision PO explicite) ; toute modification du front.

## Étapes
1. Renommer les 2 libellés dans `CreerFeuilleDetailTva` (`Exporter.cs`).
2. Mettre à jour les éventuelles assertions de tests qui vérifieraient ces libellés par valeur exacte de
   chaîne (`Declaration.Export.Excel.Tests/ExporterTests.cs`, `Declaration.Orchestration.Tests/
   Task160ExportControleTests.cs` si applicable).
3. Régénérer un `.xlsx` de contrôle réel (même méthode que TASK-180/162 : harness contre
   `GR_EMA_DISTRIBUTION`) montrant les nouveaux libellés.

## Livrables
- `Exporter.cs` modifié (2 chaînes).
- Tests mis à jour si un test référence l'ancien libellé.
- `VERIFY/TASK-182_verify.md` avec le `.xlsx` de preuve.

## Critères de validation
- Feuille « Détail TVA » : libellés « Total HT » et « Écart HT − TTC (= −Total TVA) » (ou équivalent)
  affichés à la place des anciens, valeurs strictement inchangées.
- Feuille « Récap » (export de dépôt) : **non modifiée**, libellés d'origine intacts.
- Build + tests (`Declaration.Export.Excel.Tests`, `Declaration.Orchestration.Tests`) rejoués verts.

## Risques / dépendances
- Risque nul : changement de texte pur, aucune valeur ni aucun calcul modifié, aucune dépendance avec
  TASK-180/162 (mêmes fichiers mais sections disjointes — commit séparé recommandé pour un VERIFY par
  sujet, comme pour les tasks précédentes).
