# VERIFY — TASK-166 : montants mal formatés dans les avertissements de l'écran ④ Déclaration

## Contexte
Signalement PO (capture écran ④, `TVA1-2026-06`) : montants affichés en échelle brute à 6
décimales dans les messages d'avertissement (`0,140000 MAD`, `4973,380000 MAD`) alors que le
reste de l'écran affiche 2 décimales via `formatMoney()`. Correction de bug pure, isolée aux
6 templates de message de `DeclarationWorkflowService.cs` (aucun autre champ/écran concerné,
confirmé par recherche).

## Ce qui est livré
- `DeclarationWorkflowService.cs` : nouvelle méthode privée `FormatMontantMessage(decimal)` →
  `montant.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"))` — 2 décimales, virgule décimale,
  cohérent avec `formatMoney()` (`Intl.NumberFormat('fr-FR')`) côté front (recommandation
  architecte de la task, aucun arbitrage PO requis sur le principe).
- Les 6 templates de message (`MotifRejet.EcTypeHorsPerimetre` ×2, `Impaye`, `Annule`,
  `NonComptabilise`, `NonAffecte`, cas générique) utilisent désormais
  `{FormatMontantMessage(c.Affectation.MontantAffecte)} MAD` au lieu de l'interpolation brute
  `{c.Affectation.MontantAffecte}`.
- Aucun autre champ du message modifié (motif, tiers, numéro de règlement inchangés) —
  recherche du même motif dans tout le repo confirmant l'isolement à ce seul bloc, aucune autre
  occurrence.

## Vérification indépendante des critères de validation

- [x] **Tous les montants dans les messages affichent 2 décimales, cohérentes avec le reste de
      l'écran** — preuve réelle sur `TVA1-2026-01` (soId=1, instance de dev port 5299, jamais
      :5280), `GET /declarations/{id}/checkup` : 5 alertes `REGLEMENT_EXCLU` réelles, ex.
      `"Règlement hors périmètre (Autre (90)) — non déclarable : RC26040017 (tiers FAMOYO,
      0,01 MAD)"` et `"... (tiers LA BUVETTE DU MAROC, 63,80 MAD)"` — 2 décimales, virgule,
      plus aucune trace d'échelle brute à 6 décimales.
- [x] **Aucune régression sur le texte des messages au-delà du format du montant** — comparaison
      directe des templates : seul le fragment `{c.Affectation.MontantAffecte}` a été remplacé
      par `{FormatMontantMessage(c.Affectation.MontantAffecte)}`, motif/tiers/numéro de règlement
      strictement identiques dans les 6 templates.
- [x] **Build + tests `Declaration.Orchestration.Tests` rejoués verts** —
      `dotnet build DeclarationTVA.slnx` : 0 erreur. `dotnet test DeclarationTVA.slnx` :
      **177/177** sur `Declaration.Orchestration.Tests`, y compris
      `Task100ReglementEctypeInconnuTests` (le seul test dépendant du texte exact du montant dans
      le message — assertion ajustée, voir « Incident détecté et corrigé »). Seul échec restant :
      `Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification`, préexistant et sans
      rapport (cf. VERIFY-168/169/167).

## Incident détecté et corrigé pendant la vérification
`Task100ReglementEctypeInconnuTests.GetCheckupAsync_ReglementImpayeEctype1_LeveAlerteCorrecte`
vérifiait la sous-chaîne littérale `"13053,66"` dans le message. Le nouveau format `N2` fr-FR
insère un séparateur de milliers (`"13 053,66"`, caractère espace insécable/fine selon l'ICU de la
machine) — la sous-chaîne exacte sans espace ne matchait donc plus. Anticipé explicitement par la
task (§ Étapes 2, § Risques). Corrigé en construisant l'assertion avec la MÊME expression de
formatage (`13053.66m.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"))`) plutôt qu'un littéral
figé — insensible au caractère de séparateur exact utilisé par l'ICU (espace normale/insécable/
fine selon la version .NET/OS), tout en vérifiant réellement le format attendu. Aucun autre test
du repo ne référençait un montant brut dans une assertion de message (recherche du pattern
`REGLEMENT_EXCLU`/montant décimal littéral dans `Declaration.Orchestration.Tests`).

## Réserves non bloquantes
- Le séparateur de milliers introduit par `N2` (ex. `13 053,66 MAD`) n'a pas été comparé
  pixel-à-pixel avec `formatMoney()` du front dans un navigateur réel (aucun outil d'automatisation
  navigateur disponible dans cette session) — les deux utilisent la même culture `fr-FR` et le
  même nombre de décimales, mais `formatMoney()` ajoute en plus un symbole devise `MAD` positionné
  par `Intl.NumberFormat` alors que le back concatène `" MAD"` littéralement en fin de chaîne ; une
  différence de rendu strictement typographique (position/espace du symbole), pas de valeur,
  jugée hors du défaut confirmé par cette task (échelle à 6 décimales) mais signalée pour
  transparence.
- Observation secondaire signalée par la task (regroupement par motif / tri par montant décroissant
  des avertissements) explicitement laissée hors périmètre — non traitée ici, comme demandé.
