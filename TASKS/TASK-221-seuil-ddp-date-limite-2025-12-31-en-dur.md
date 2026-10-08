# TASK-221 — Seuil de montant DDP : date limite portée au 31/12/2025 (en dur) et borne `> 10 000`

RISK : HIGH (périmètre déclaratif légal, effet pour tous les clients) — preuve par critère datée dans le VERIFY.
Point d'arrêt : dépôt de `VERIFY/TASK-221_verify.md`. **Interdit** : déplacer vers `DONE_DETAIL/`, modifier `DONE.md`/`TODO.md`/`CHANGELOG.md`, commit contenant « approuve ». La clôture revient à une review par un agent qui n'a pas implémenté.

## Contexte
Décision PO (08/10/2026), explicite, par simplification volontaire (« on met en dur dans le code, on fait évoluer au fur et à mesure selon le besoin ») :

| Date de facture (`DoDate` = `RT_ECHEANCE.DO_Date`) | Déclarée ? |
|---|---|
| < 01/07/2023 | non (hors loi, inchangé) |
| 01/07/2023 → 31/12/2025 | **uniquement si montant > 10 000** |
| ≥ 01/01/2026 | oui, **tous montants** |

- **Montant** : inchangé pour le moment, c'est `EC_Montant` de l'**échéance** (devise société), comme aujourd'hui (`SelectionDelaiPaiementRepository.cs:100,114` ; `SelectionDelaiPaiementCalculator.cs:351`). Le PO a écarté pour l'instant le total TTC de la facture.
- **Valable pour tous les clients**, constante unique dans le code, aucun réglage par société.

La règle existe déjà dans `SeuilsLegauxDelaiPaiement` (`Declaration.Core/SelectionDelaiPaiementCalculator.cs:14-33`, reproduction du legacy `dateDebDecLoi`/`dateLimiteMontant`, TASK-131). **Deux changements seulement** :
1. `DateLimiteSeuilMontant` : `2024-12-31` -> **`2025-12-31`** (l.20).
2. **Borne** : `montant >= SeuilMontant` -> `montant > SeuilMontant` (C# l.32 ; SQL `EC_Montant >= @SeuilMontant`, `SelectionDelaiPaiementRepository.cs:114`). `SeuilMontant = 10 000` et `DateDebutDeclarationLoi = 2023-07-01` ne changent pas. Cumul inchangé avec TASK-220 (`DoDate < DateMiseEnRouteSociete` => exclue).

### Écart connu avec la DGI (porté par le PO)
La note circulaire DGI n°734 §O-2 p.10 exempte les factures de montant « inférieur ou égal à dix mille (10.000) dirhams, toutes taxes comprises » **émises avant le 1er janvier 2025** seulement ; l'annonce DGI du 21/03/2025 (finances.gov.ma `fiche=7218`) précise que « l'amende pécuniaire s'applique à toutes les factures émises à compter du 1er janvier 2025, y compris celles dont le montant est inférieur ou égal à 10 000 dirhams ». La règle ci-dessus étend donc l'exemption aux factures émises en 2025, **pour tous les clients** : risque d'amende de 5 000 MAD par facture manquante ou inexacte lorsque la déclaration est insuffisante ou incomplète (NC 734 p.7). Le PO a été informé de ces textes (présentés par l'architecte le 08/10/2026) et maintient sa décision. Autres écarts connus, hors périmètre : la NC vise la facture TTC (le code teste l'échéance) ; la date de début de loi dépend du CA de la société dans la NC (§O-1), le code applique 2023-07-01 à tous. Le design détaillé d'une évolution (total TTC de la facture, réglage par société avec historique, bandeau, XML de dépôt), relu en 5 passes le 08/10/2026, est conservé dans le commit `70ca0fe` (`TASKS/TASK-221-borne-seuil-ddp-note-dgi-734.md`, `TASKS/TASK-222-seuil-exclusion-ddp-parametrable-par-societe.md`).

**Décision PO à tracer dans le VERIFY** : [ ] la règle ci-dessus s'applique à **tous** les clients (l'exemption de 2025 s'applique à tous).

## Objectif
```
Entrée  : DateLimiteSeuilMontant = 2024-12-31 ; borne >= ; comparaison de date SQL sensible à l'heure.
Traitement : DateLimiteSeuilMontant = 2025-12-31 ; borne > ; comparaison de date SQL à la journée.
Sortie  : échéances de factures du 01/01/2025 au 31/12/2025 de montant <= 10 000 exclues du contrôle DDP ; échéances de montant
          exactement égal à 10 000 exclues jusqu'au 31/12/2025 ; toutes les échéances de factures >= 01/01/2026 incluses ;
          reste inchangé.
```

## Périmètre STRICT
- **Inclus** :
  1. `SelectionDelaiPaiementCalculator.cs` : `DateLimiteSeuilMontant` (l.20) ; `EstEligibleSeuilLegal` (l.30-32) : `montant > SeuilMontant` ; commentaires XML (l.19-29) alignés (note DGI 734 §O-2 : exemption « <= 10 000 » ; règle PO jusqu'au 31/12/2025, écart signalé).
  2. `SelectionDelaiPaiementRepository.cs` (l.113-114) : `EC_Montant > @SeuilMontant` ; prédicat de date inconditionnellement à la journée : `DO_Date >= @DateApresLimite` avec `DateApresLimite = dateLimiteSeuilMontant.Date.AddDays(1)` (équivaut à `.Date >` du C#, sans dépendre d'une éventuelle heure dans `DO_Date` ; une facture du 31/12/2025 à 14:00 est ainsi traitée comme le jour limite, donc soumise au seuil, en SQL comme en C#). Signature de la méthode inchangée.
  3. Tests : `SelectionDelaiPaiementCalculatorTests.cs` l.534 et l.542 (`10_000m` passe de `True` à `False`, ajouter `10_000,01m` -> `True`) ; scénarios ci-dessous ; **relire tous les tests et fixtures datés de 2025 avec un montant <= 10 000 qui attendent une sélection** (Core, `Task132CycleDeVieDeclarationDelaiPaiementTests.cs`, `Task133GenerationFichierDelaiPaiementTests.cs`, Playwright `task134.spec.ts`/`task136.spec.ts`) et les adapter ; corriger les titres « reproduits à l'identique du legacy » (la règle diverge volontairement).
  4. Textes vivants : grep `2024-12-31`, `31/12/2024`, « 10 000 », « seuil », aides TASK-217, `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html`, `ISelectionDelaiPaiementRepository.cs:27-29` : mettre à jour les textes qui décrivent l'ancienne règle (pas `DONE_DETAIL`).
- **Exclus** : total TTC de la facture / somme des échéances ; colonne « Montant facture TTC » et toute modification d'écran ; fichier XML de dépôt ; réglage par société, nouvelles tables, bandeau, en-tête d'export ; `DateDebutDeclarationLoi` ; TASK-220 ; avoirs/montants négatifs (comportement existant) ; index ou changement de schéma ; lecture de la base Sage.

## Étapes
1. Grep exhaustif (périmètre 4 et fixtures de tests) ; `git log -5` sur les fichiers touchés et rebase sur `main`.
2. Modifier la constante, la borne (C# et SQL), le prédicat de date, les commentaires.
3. Adapter/ajouter les tests (scénarios ci-dessous).
4. **Mesure sur données réelles** (lecture seule, depuis une machine qui accède à la base GRF ; sinon fournir les requêtes dans le VERIFY, non bloquant) : nombre et montant des échéances de 2025 de montant <= 10 000 qui **sortent**, dont combien déjà présentes dans `RT_DECLARATIONDELAISPAIEMENTLG` (déjà déclarées, plus suivies), combien non soldées ; nombre d'échéances de montant exactement 10 000,00 ; `SELECT COUNT(*) FROM RT_ECHEANCE WHERE DO_Date <> CAST(DO_Date AS date)` ; lignes réellement produites pour la période du prochain dépôt, avant/après.
5. `dotnet build DeclarationTVA.slnx` ; `dotnet test` (Core, Orchestration, Export.Excel, Export.Xml) ; `npm run lint` + `npm run build` (dans `declaration-tva-web/`) ; `npx playwright test` (`task134`, `task136`).
6. Déposer `VERIFY/TASK-221_verify.md` et **s'arrêter**.

## Scénarios de test (`EstEligibleSeuilLegal`, `DateMiseEnRoute` non configurée sauf mention ; montant = `EC_Montant` de l'échéance)
| DoDate | Montant | Attendu |
|---|---|---|
| 2023-06-30 | 1 000 000 | non (avant la loi) |
| 2023-07-01 | 10 000,00 | non (borne exclue) |
| 2023-07-01 | 10 000,01 | oui |
| 2024-12-31 | 9 999,99 | non |
| 2025-01-01 | 9 999,99 | non (était oui avant la TASK) |
| 2025-04-23 | 800 | non (cas d'un client ; était oui) |
| 2025-12-31 | 10 000,00 | non |
| 2025-12-31 | 10 000,01 | oui |
| 2025-12-31 à 14:00 | 800 | non, **en C# comme en SQL** |
| 2026-01-01 | 0,01 | oui (tous montants) |
| 2026-01-01 | 800 | oui |
| 2025-06-01 | -10 080 | non (négatif <= seuil ; comportement existant) |
| 2026-02-01 | -10 080 | oui (tous montants ; comportement existant, hors périmètre) |
| 2025-03-15, `DateMiseEnRoute` = 2025-06-01 | 50 000 | non (TASK-220) |
Parité SQL/C# : le prédicat de date SQL et `EstEligibleSeuilLegal` donnent le même résultat sur les 14 cas, preuve par requête de mesure sur la base (cas avec heure inclus) ou, à défaut, par relecture du prédicat consignée dans le VERIFY.

## Livrables
Constante, borne (C# + SQL), prédicat de date, tests, textes ; `VERIFY/TASK-221_verify.md` : réponse du PO à la décision « tous les clients », mesure de l'étape 4 (ou requêtes à lancer), checklist avec preuve datée par critère (méthode + date).

## Critères de validation
- Les 14 scénarios passent ; `grep -rn ">= SeuilMontant" --include=*.cs` et `grep -rn "EC_Montant >=" --include=*.cs` -> 0 résultat ; aucune occurrence résiduelle de `2024-12-31` hors historique et tests historiques justifiés.
- build .NET + `dotnet test` + lint/build front + Playwright : 0 erreur.
- Mesure de l'étape 4 fournie au PO (ou requêtes prêtes à lancer, signalé).

## Risques / dépendances
- **Effet pour tous les clients** : les échéances de factures émises en 2025 de montant <= 10 000 sortent du contrôle partout ; cohérence avec la DGI : voir « Écart connu ». Exposition : 5 000 MAD par facture manquante ou inexacte (NC 734 p.7).
- **Déjà déclarées** : une échéance de 2025 <= 10 000 déjà déclarée (par exemple dans l'annuelle 2025) cesse d'être alimentée : plus aucun incrément de retard pour elle ; les déclarations déjà déposées ne sont pas modifiées. Cas d'un client : la ligne de 800 MAD (facture du 23/04/2025) de l'écran Contrôle de T3 2026 disparaît après déploiement.
- **Montant de l'échéance, pas de la facture** : une facture > 10 000 scindée en échéances <= 10 000 reste exclue (limite connue du legacy, assumée par le PO pour le moment).
- Brouillons de déclaration non déposés déjà générés : ils contiennent l'ancien périmètre ; décision PO à tracer si une régénération est souhaitée (aucune régénération automatique).
- Conflits de merge possibles : `SelectionDelaiPaiementCalculator.cs` (TASK-210) ; `IN_PROGRESS/` était vide le 08/10/2026 : revérifier au démarrage.
