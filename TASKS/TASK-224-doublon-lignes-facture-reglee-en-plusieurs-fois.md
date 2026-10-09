# TASK-224 — Lignes dupliquées pour une facture réglée en plusieurs fois (TVA surdéclarée)

Status: 🆕 à faire
Priority: CRITICAL (surdéclaration fiscale réelle — 64 908,46 MAD sur la déclaration observée)
Module: Declaration.Application / Declaration.Infrastructure / Declaration.Core.Tests
RISK: HIGH — impact fiscal direct + impact UX (le comptable valide un total faux sans le voir)

> **Origine :** signalement PO du 09/10/2026 avec capture du drill « Détail des affectations —
> TVA1-2026-03-T » (société FIVE KEYS MAROC). 21 lignes affichées pour 15 règlements, total TVA
> `133 189,91 MAD`.

## Constat chiffré (capture PO)

3 factures réglées en 2 fois (50 % + 50 %) apparaissent chacune avec **2 lignes par règlement**
(mêmes taux 20 %, mêmes montants), soit 4 lignes au lieu de 2 :

| Facture | Tiers | TTC | Règlements | TVA attendue (1×) |
|---|---|---|---|---|
| FF260029 | LOGINFO INGENIERIE | 160 486,80 | RF26100006 + RF26100007 (2 × 80 243,40) | 26 747,80 |
| FF260027 | UNION MAGHREB DES TE | 148 549,20 | RF26070005 + RF26090005 (2 × 74 274,60) | 24 758,20 |
| FF260028 | SNSS TECHNOLOGIES | 80 414,77 | RF26090006 + RF26090007 (40 207,38 + 40 207,39) | 13 402,46 |

- Les 9 règlements à ligne unique totalisent `3 373,00` ; les 3 factures comptées 1× = `64 908,46` ;
  total correct = `68 281,46`. Le total affiché (`133 189,91`) = `3 373,00 + 2 × 64 908,46` (à 0,01 près)
  → **les 3 factures sont comptées deux fois**. Surdéclaration = **64 908,46 MAD**.
- Les factures à règlement unique (MAROC TELECOM, MAJAL INVEST…) ne sont PAS dupliquées.
- Les règlements de ces 3 factures sont de mois différents (RF2607, RF2609, RF2610) mais tous dans la
  même déclaration — à confirmer si c'est normal (règle de période) en Phase 1.

## Ce qui est déjà établi (ne pas refaire)

- **Calcul sain.** Simulation scratch du 09/10/2026 : facture du 10/07 (160 486,80 TTC, TVA 26 747,80
  à 20 %) réglée 80 243,40 le 15/08 puis 80 243,40 le 15/09 → `ConstructeurDeclaration.ConstruireDeclaration`
  produit exactement **2 lignes** (13 373,90 + 13 373,90 = 26 747,80). `Ventilateur`/`ConstructeurDeclaration`
  ne dupliquent pas.
- **Cache écarté.** `DM_VENTILATION_SAGE_CACHE` a pour PK `(SO_Id, EC_Id, Taux)` : impossible d'y avoir
  2 lignes au même taux pour une facture.
- **Requête de sélection écartée.** `GetDecaissementFournisseurSql`
  (`Declaration.Selection/SelectionnerAffectationsService.cs`) = 1 ligne par `RT_AFFECTATION`.
- **Doublon de tiers écarté par le PO** (à ne pas ré-investiguer sauf si Phase 1 y mène).

## Hypothèses restantes (à départager en Phase 1)

1. **Lignes figées `DM_LGTVA` écrites deux fois** : `ReintegrerReglementsLiberesAsync` /
   `ChargerCandidatesSiNecessaireAsync` / `SaveLignesCandidatesAsync` (`DeclarationWorkflowService.cs`
   ~l.202-250 et ~l.532-590). La réintégration identifie une ligne par `(NumeroFacture,
   NumeroRapprochement)` **sans identifiant d'affectation** (`AF_Id`/`EC_Id`+`MV_Id`) ; un
   `DeleteLignesAsync` + `SaveLignesCandidatesAsync` peut réinsérer sans purger l'existant selon le chemin.
2. **`RT_AFFECTATION` avec plusieurs lignes par couple (MV_Id, EC_Id)** (affectation découpée côté ERP).
3. **Duplication à l'affichage seulement** (projection du drill / endpoint `GET .../lignes`) — la source
   du drill est introuvable dans les sources front actuelles (`Détail des affectations` n'existe que dans
   le bundle compilé `wwwroot/assets/index-*.js`) : le worker doit la retrouver.

## Périmètre STRICT

- **Inclus :**
  1. **Phase 1 — Diagnostic SANS accès base de données.** Le PO n'a aucun accès SQL : toute étape
     doit être faisable par tests automatisés et par l'application elle-même.
     - **Données de simulation** : créer les fixtures à partir de
       [`TASKS/assets/TASK-224-donnees-simulation.md`](assets/TASK-224-donnees-simulation.md)
       (12 factures, 15 règlements reconstitués de la capture, résultats attendus inclus :
       15 lignes, total `68 281,46`). Données de test uniquement, jamais écrites en base réelle.
     - **Reproduction par tests automatisés** (faux dépôts, comme `Task077RevalidationLignesFigeesTests.cs`
       / `Task081PremierFigeageBandeauTests.cs` dans `Declaration.Orchestration.Tests`) : rejouer sur
       `DeclarationWorkflowService` le jeu de simulation en enchaînant les chemins qui écrivent des lignes
       figées (`ChargerCandidatesSiNecessaireAsync`, `RevaliderLignesFigeesAsync`,
       `ReintegrerReglementsLiberesAsync`, resynchronisation, changement de sélection), et compter après
       chaque étape les lignes par `(facture, règlement, taux)`. Le chemin qui produit 2 lignes
       identiques est la cause ; le test rouge (commit de référence) est la preuve.
     - **Outil de diagnostic dans l'application (lecture seule)** : action « Détecter les doublons » sur
       « Vérifier & Intégrer », listant les groupes `(facture, règlement, taux)` présents plusieurs fois
       dans la déclaration courante (n° facture, n° règlement, taux, nombre, TVA cumulée, TVA facture).
       Le PO l'utilise sur `TVA1-2026-03-T` sans SQL et fournit une capture. Aucune modification de données.
     - **Si aucune reproduction ne tient : STOP et signaler** (pas de correctif improvisé) ; la capture de
       l'outil devient alors la donnée de départ de l'investigation.
  2. **Phase 2 — Correction de la cause confirmée**, plus **idempotence** : écrire une ligne figée
     est impossible si une ligne de même clé existe déjà. Clé = `(DeclarationId, Domaine, EC_Id, MV_Id
     [ou AF_Id], Taux, CodeTaxe)` — à ajuster selon le schéma réel de `DM_LGTVA` (colonnes `EC_Id` /
     `MV_Id` présentes ?). Pas de MERGE silencieux qui masque un second insert : un doublon détecté doit
     être **loggé** (`_log`) et ignoré, jamais sommé.
  3. **Garde-fou bloquant** (alerte `Error`, code `TVA_FACTURE_SURDECLAREE`) dans la construction/revalidation
     de la déclaration : pour chaque facture (EC_Id) d'une déclaration, `Σ TVA déclarée ≤ TVA de la
     facture` (tolérance d'arrondi 0,05 × nb de lignes, cohérente avec `ConstructeurDeclaration` l.229)
     ET `Σ MontantAffecte ≤ TTC facture`. Violation → alerte bloquante visible dans « Vérifier & Intégrer »
     et clôture bloquée tant qu'elle n'est pas levée. Ne pas bypasser pour les soldes initiaux (EC_Type=4)
     ni les FGR (EC_Type=111) : appliquer le contrôle sur la TVA connue ; si TVA inconnue, ne pas contrôler
     (documenter).
  4. **Nettoyage des déclarations existantes** : l'outil de détection de la Phase 1 doit s'appliquer à chaque
     déclaration non clôturée, sans SQL manuel ; captures jointes au VERIFY. **Aucune suppression automatique** : le correctif des
     données de `TVA1-2026-03-T` est soumis au PO (arbitrage explicite) avant exécution. Les déclarations
     déjà clôturées/déposées avec doublon sont à signaler au PO, jamais modifiées.
- **Exclu :**
  - Aucun changement des formules de `Ventilateur` / prorata (sains, prouvés).
  - Aucune modification de tables `apbs-gr_winform` (cf. mémoire projet : pas de changement de schéma
    sur les tables winform, même coordonné/manuel). Une éventuelle contrainte d'unicité ne porte que sur
    les tables GRF/persistance `DM_*`.
  - Pas de refonte du drill ni de l'écran Vérifier & Intégrer.

## Scénarios de test formalisés (RISK HIGH — obligatoire)

Document `TASKS/assets/TASK-224-scenarios-test.md` (ou section du VERIFY) listant, comme script manuel :
1. **Nominal** : facture 10/07 réglée 50 % le 15/08 puis 50 % le 15/09 → 1 ligne par règlement et par taux ;
   TVA facture 26 747,80 comptée **une fois** ; relancer « Resynchroniser » 3× → toujours 2 lignes.
2. **Ordre inverse** : sélectionner d'abord le 2e règlement, puis le 1er → pas de doublon.
3. **Réintégration** : libérer puis reprendre un règlement (chemin `ReintegrerReglementsLiberesAsync`) → pas de doublon.
4. **Facture multi-taux (10/20 %) réglée en 2 fois** : 2 règlements × 2 taux = 4 lignes, pas 8.
5. **Facture réglée en 3 fois dont 1 hors période** : seule la part dans la période est déclarée.
6. **Cas limite arrondi** : 80 414,77 réglé 40 207,38 + 40 207,39 → Σ TVA = TVA facture à 0,01 près, sans alerte.
7. **Garde-fou** : forcer artificiellement 2 lignes identiques en base de test → alerte bloquante
   `TVA_FACTURE_SURDECLAREE`, clôture refusée.

## Livrables

- Diagnostic Phase 1 (test de reproduction rouge + hypothèse retenue + capture de l'outil de détection) dans `VERIFY/TASK-224_verify.md`.
- Correctif de la cause confirmée + idempotence d'écriture des lignes figées.
- Garde-fou `TVA_FACTURE_SURDECLAREE` + clôture bloquée.
- Tests automatisés :
  - `Declaration.Core.Tests` : régression « 1 facture, 2 règlements (août/septembre) → 2 lignes, ΣTVA = TVA
    facture » (reprendre la simulation scratch : facture `FF260029`, TTC 160 486,80, HT 133 739,00,
    TVA 26 747,80 à 20 %, 2 × 80 243,40) ;
  - test d'intégration / Application reproduisant le chemin de réécriture des lignes figées (cause
    confirmée) : appliquer 2× la même sélection → nombre de lignes inchangé ;
  - test du garde-fou (violation et cas sain).
- Outil de détection de doublons (lecture seule) opérationnel dans l'application, résultat sur les déclarations non clôturées.

## Critères de validation (preuve par critère, datée — cf. CLAUDE.md « Discipline de preuve »)

- [ ] `dotnet build DeclarationTVA.slnx` → 0 erreur (log + date).
- [ ] `npm run lint` + `npm run build` dans `declaration-tva-web/` si le front est touché → 0 erreur.
- [ ] Phase 1 : cause prouvée par un test de reproduction rouge sur le jeu de simulation (aucun accès SQL requis du PO), pas supposée.
- [ ] Test de régression rouge AVANT correctif, vert APRÈS (commits de référence).
- [ ] Sur `TVA1-2026-03-T` (copie de test ou après arbitrage PO) : total TVA attendu = `68 281,46`
      (et non `133 189,91`) ; FF260029/FF260027/FF260028 = 2 lignes chacune.
- [ ] Scénarios 1 à 7 exécutés, résultat noté (OK/KO + méthode + date).
- [ ] Garde-fou : alerte visible dans « Vérifier & Intégrer » + clôture refusée (capture).
- [ ] Aucun bypass de règle de sécurité ; aucune écriture sur tables winform.
- [ ] Toute case non cochée documente explicitement pourquoi.

## Risques / dépendances

- Si le correctif retire des lignes déjà figées dans des déclarations ouvertes, le total change pour
  l'utilisateur : arbitrage PO avant toute purge de données (cf. périmètre 4).
- Des déclarations déjà **déposées** avec surdéclaration sont possibles : à signaler au PO (hors
  périmètre de correction automatique).
- Dépendance éventuelle avec `TASK-006`/`TASK-007`/`TASK-024` en VERIFY (lignes figées / cache) : vérifier
  l'absence de conflit d'édition sur `DeclarationWorkflowService.cs` et `DeclarationRepository.cs`.
- Règle de période à confirmer : un règlement de juillet peut-il figurer dans une déclaration de mars
  (`TVA1-2026-03-T`) ? (`RegleDatePeriode` / TASK-099 « rattrapage, plus de borne basse »). Si c'est le
  comportement voulu, le documenter ; sinon, ouvrir une TASK distincte.

## Files

- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`ChargerCandidatesSiNecessaireAsync` ~l.202, `ReintegrerReglementsLiberesAsync` ~l.532, `RevaliderLignesFigeesAsync` ~l.360).
- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs) (`SaveLignesCandidatesAsync`, `DeleteLignesAsync`, `GetLignesAsync`).
- [Declaration.Core/ConstructeurDeclaration.cs](../Declaration.Core/ConstructeurDeclaration.cs) (point d'insertion du garde-fou, contrôle d'équilibre l.220-239).
- [Declaration.Selection/SelectionnerAffectationsService.cs](../Declaration.Selection/SelectionnerAffectationsService.cs) (lecture seule — déjà vérifié sain).
- [Declaration.Core.Tests/ConstructeurDeclarationTests.cs](../Declaration.Core.Tests/ConstructeurDeclarationTests.cs) (test de régression).
