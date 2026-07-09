# TODO — Module Déclaration TVA (GRF)

## Ordre d'exécution consolidé (re-séquencé 08/07/2026)

Vérifié dans le code : les Gap A/B/C décrits par les tasks sont **confirmés**. Ordre retenu :

**Vague 1 — correctness + perf (débloque le reste)**
1. **TASK-022** routing `EC_Type` — correction (FGR ne doit jamais passer par l'OM) + gros gain perf. Touche le SELECT sélection + dispatcher orchestrateur + lecteur FGR SQL. Accélère aussi le recalcul de TASK-009.
2. **TASK-023** session Sage réutilisée — **en parallèle de 022** (assembly séparé `SageTaxReader`, aucune collision).

**Vague 2 — valeur front (Track A)**
3. **TASK-020 §1** n° règlement sur `LigneCandidate` — ✅ **livré** (Gap A comblé).
3bis. **TASK-027** (2ᵉ vague de TASK-020, §2) conformité IF/ICE calculée au DTO via extraction du validateur TASK-011 — ✅ **livré** (source unique `ValidationIdentiteFiscale`, `Conformite` exposé au DTO).
4. **TASK-021** reportées — ✅ **livré** (Gap B comblé, motif honnête, volume réel 304 mesuré).
5. **TASK-019** poste de travail 4 interrogations — ✅ **livré** (front API réelle ; bug TVA 0% = correctif back Gap A extrait → TASK-030).

**Vague 3 — décisions & finalisation**
6. ~~**TASK-025** cadrage `EC_Type=4`~~ — ✅ **livré et approuvé** (2026-07-09) : inventaire réel (34 lignes, 0 TVA déclarable), décision PO = maintenir l'alerte (statu quo). Voir DONE.md.
7. **TASK-009** mode contrôle — ✅ **livré et approuvé** (matrice d'écarts réelle sur EMA `DT_Id=66` ; type-4 non polluant sur ce jeu, 14 `ManquantRecalcul` tous causés au bug `DT_Id` partiel GRFN).
8. **TASK-028** verrou d'intégration déclaration (tampon `RT_AFFECTATION.DT_Id` + triggers immuabilité `RT_MOUVEMENT`/`RT_AFFECTATION`, portée globale GRFN inclus) — 🎯 **prêt** (décisions PO figées). Débloque l'immuabilité exigée par TASK-024.
9. ~~**TASK-024** cache Sage~~ — ✅ **livré et approuvé** (2026-07-09) : cache SQL Server exclusif `GRC_VENTILATION_SAGE_CACHE`, validation du token de paiement local à la lecture, aucun code GRF touché. Voir DONE.md.

> ✅ **Collision couche sélection résolue** : TASK-009 a modifié `SelectionnerAffectationsService` (`JOIN RT_ECHEANCE` pour `DO_Numero`, filtre date conditionné à `dtId`) ; TASK-021 (`SelectionExpliqueeService`, fichier distinct) et TASK-022 touchaient le même périmètre SELECT/JOIN. Les trois sont désormais livrées et approuvées — edits réconciliés, build vert.
>
> **Chemin critique front** : 020 §1 ✅ + 027 ✅ → 021 → 019. **Chemin critique fiabilité/démo** : 022 (+023) → 025 → 009.

## Ordre d'exécution

### ✅ Fait
| Task | Objet | Note |
|---|---|---|
| [TASK-002](TASKS/TASK-002-worker-lecture-facture-taxes-om.md) | Worker OM : lecture facture + taxes | ✅ prouvé sur `DISTRI_DEMO` |
| [TASK-004](TASKS/TASK-004-ventilation-proration-coeur-calcul.md) | Ventilation / proration (calcul pur) | ✅ 9/9 tests — corrections via TASK-006 |
| [TASK-005](DONE_DETAIL/TASK-005-modele-declaration-controle.md) | Modèle déclaration + contrôle | ✅ VERIFY approuvé — contrôle réel (affecté vs ventilé) porté par TASK-006 |
| [TASK-006](DONE_DETAIL/TASK-006-corrections-controle-alertes.md) | Corrections revue (contrôle réel, alerte IF, prorata/désignation) | ✅ VERIFY approuvé — 9/9 tests |
| [TASK-001](DONE_DETAIL/TASK-001-verif-rapprochement.md) | Vérifier synchro rapprochement (source locale fiable ?) | ✅ prouvé sur `GR_EMA_DISTRIBUTION` — local suffisant (352/352, 1008/1008 hors espèces) |
| [TASK-007](DONE_DETAIL/TASK-007-orchestration-cache-pipeline.md) | Orchestration + cache (worker OM ↔ ventilation ↔ modèle) | ✅ VERIFY approuvé — 5/5 tests, cache prouvé, timeout worker robuste |
| [TASK-008](DONE_DETAIL/TASK-008-selection-sql-eligibilite-locale.md) | Sélection SQL : éligibilité locale → `AffectationADeclarer` | ✅ VERIFY approuvé |
| [TASK-015](DONE_DETAIL/TASK-015-selection-expliquee-eligibilite-motifs.md) | Sélection expliquée (surensemble + motifs) | ✅ VERIFY approuvé — test d'intégration formel vs TASK-008 |
| [TASK-013](DONE_DETAIL/TASK-013-front-web-declaration-tva.md) | Front web workflow par domaine (stepper, grilles serveur, checkup, génération) | ✅ VERIFY approuvé — build tsc+vite OK, e2e Playwright vert, statut `Écartée`+motif prouvé |
| [TASK-010](DONE_DETAIL/TASK-010-export-excel.md) | Export Excel (artefact dépôt/archive) — 2 feuilles Détail+Récap | ✅ VERIFY approuvé — `net10.0` pur ClosedXML, 1/1 test vert, .xlsx conforme |
| [TASK-011](DONE_DETAIL/TASK-011-export-xml-simpl-tva.md) | Export XML Simpl-TVA + zip (fichier de dépôt DGI) | ✅ VERIFY approuvé — `net10.0` pur, tests verts, validation IF/ICE et format conforme |
| [TASK-016](DONE_DETAIL/TASK-016-ajustements-ui-drilldown-bulk-filtre.md) | Ajustements UI : drill-down anomalie→grille filtrée + action de masse sur filtre | ✅ VERIFY approuvé — e2e Playwright vert, screenshots fournis |
| [TASK-014](DONE_DETAIL/TASK-014-mapping-domaines-schemas-xml-dgi.md) | Cadrage mapping domaines → formulaires/schémas XML DGI | ✅ VERIFY approuvé — décaissement regroupé Relevé de déductions, schéma CA escaladé PO |
| [TASK-012](DONE_DETAIL/TASK-012-api-tva-clean-architecture.md) | API TVA (ASP.NET Core, Clean Arch GRC_WEB, JWT) — workflow : persistance + endpoints paginés + état/ligne + checkup + clôture gardée | ✅ VERIFY approuvé — captures réelles vérifiées vs source ; transparence prouvée (fixture 2 éligibles + 3 écartées-avec-motif), clôture bloquante réelle, injection SQL corrigée, génération/contrôle délégués 501 → TASK-009/010/011 |
| [TASK-017](DONE_DETAIL/TASK-017-cablage-selection-prod-montants-reels.md) | Câblage sélection prod réelle + montants réels dans le figeage API (avenant TASK-012) | ✅ VERIFY approuvé — sélection réelle `GR_EMA_DISTRIBUTION` (écartées+motif), forfait 20 % retiré (montants worker OM, cas 10 % prouvé), comparaison source↔figé identique, checkup 0/clôture OK, isolation `RT_*` |
| [TASK-018](DONE_DETAIL/TASK-018-refonte-checkup-hub-tour-controle.md) | Refonte Checkup en hub « tour de contrôle » (remplace le tunnel) — front, mock-first | ✅ VERIFY approuvé sur mock — hub, ventilation 5 segments (écart 0), tuiles, clôture verrouillée, build/e2e OK. **⛔ Concept ABANDONNÉ (PO 08/07) → superseded par TASK-019** (héros d'agrégats jugé « n'importe quoi » pour un comptable) |
| [TASK-022](DONE_DETAIL/TASK-022-routing-ectype-lecteur-fgr-sql.md) | Routing `EC_Type` + lecteur FGR SQL (111→`RT_HISTCOMPTA`, 0→OM, 4→alerte) | ✅ VERIFY approuvé — taux via `F_TAXE`/Sage (2 connexions, sans parsing), Σ(HT+TVA)=TTC, build 0 erreur + Orchestration.Tests 12/12 (rejoués par l'architecte). **⚠️ Réserves : preuve réelle `FF260070` + perf non fournies (tests synthétiques) → à valider sur base avant prod. Dette Core.Tests → TASK-026** |
| [TASK-027](DONE_DETAIL/TASK-027-conformite-if-ice-dto-lignes.md) | Conformité IF/ICE calculée au DTO + extraction du validateur TASK-011 (source unique) | ✅ VERIFY approuvé — `ValidationIdentiteFiscale` (source unique), exporter rebranché sans régression, `Conformite` exposé au DTO ; build 0 erreur + Core.Tests 25/25 + Export.Xml.Tests 4/4 (rejoués par l'architecte). Réserves non bloquantes : warnings xUnit1012/CS8602, Core.Tests hors `.slnx` (TASK-026) |
| [TASK-021](DONE_DETAIL/TASK-021-selection-reportees-non-rapprochees.md) | Avenant back (Gap B) : sélection des reportées (non-rapprochées `MV_Point≠Oui`) marquées `Reportee` + motif | ✅ VERIFY approuvé — SQL miroir (3 domaines), garde-fous universels priment sur `NonRapproche` (motif honnête, règle n°1), mapping `Reportee`, volume réel 304/1309 réconcilié ; Selection.Tests 13/13 hors DB (rejoués par l'architecte). **⚠️ Réserves : test d'intégration + volume 304 sur base prod live non re-jouable côté architecte ; H1 non bornée / H2 sortie manuelle (assumés 1er tour)** |
| [TASK-019](DONE_DETAIL/TASK-019-poste-travail-declaration-4-interrogations.md) | Poste de travail 4 interrogations (Rapprochement · Affectation · Conformité IF/ICE · Factures 2 faces) — front, API réelle ; supersède TASK-018 | ✅ VERIFY approuvé — 5 captures e2e Playwright (dont actions de masse + clôture verrouillée grisée), réconciliation 5 segments `total = candidates` (TASK-013), 3 vues de preuves différenciées, build tsc+vite + oxlint OK. Bug **TVA 0%** diagnostiqué = correctif back Gap A (`Ventilateur.cs`/`LecteurTvaFgr.cs`) **extrait → TASK-030** (preuve données réelles `GR_EMA_DISTRIBUTION`). |
| [TASK-009](DONE_DETAIL/TASK-009-mode-controle-vs-grfn.md) | Mode Contrôle : recalcul corrigé vs `RT_LigneDeclarationTva` (écarts GRFN) — `Declaration.Controle` | ✅ VERIFY approuvé — comparaison réelle EMA `DT_Id=66` (06/2026) : 3 173 concordants (99,6 %), 14 `ManquantRecalcul` (bug `DT_Id` partiel GRFN, causés), 95 `ManquantGRFN` (sauts silencieux, Σ\|AF\|=475 811,82 MAD). Corrections `SelectionnerAffectationsService` (JOIN `RT_ECHEANCE`→`DO_Numero` + filtre date conditionné `dtId`) vérifiées en source ; repo `SELECT` seul (lecture seule stricte) ; build 0 erreur. Réconciliation montants §2/§3.2/§5 corrigée avant approbation. |

### 🎯 Track A — front poste de travail
✅ **Terminé** — TASK-019 livrée et approuvée (voir table ✅ Fait). Correctif back associé Gap A TVA : **TASK-030**.

### ⚡ Track C — Perf & sources de TVA par `EC_Type` (retour worker TASK-009)
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-023](TASKS/TASK-023-lecture-sage-session-reutilisee.md) | **Session Sage réutilisée** (Type 0) : 1 `Open()` → boucle `ReadPiece` → 1 `Close()` + cache devise/codes taxe ; **pas** de `Parallel.ForEach` | 🎯 **prêt** — indépendant de 022 (dev parallèle), optimise le résiduel OM. |
| 2 | [TASK-028](TASKS/TASK-028-verrou-integration-declaration-dt-id.md) | **Verrou d'intégration déclaration** : tampon `RT_AFFECTATION.DT_Id` (déjà lu, pas encore écrit) + triggers immuabilité `RT_MOUVEMENT`/`RT_AFFECTATION` (portée globale, GRFN inclus ; réouverture = `DT_Id→NULL`) | 🎯 **prêt** — décisions PO figées (08-09/07). Miroir du blocage legacy GRFN décompilé. |
| 3 | [TASK-024](DONE_DETAIL/TASK-024-cache-ventilations-sage-check-delta.md) | **Cache ventilations Sage (gel par le paiement, validé à la lecture)** : fraîcheur = facture clôturée Sage au règlement (pas la déclaration) ; matérialisée à la 1re lecture, relue 100 % SQL ; validation du token de paiement local avant de servir (aucun code GRF ni trigger). `cbModification` abandonné (PO) | ✅ **livré et approuvé** (2026-07-09) — cache **SQL Server exclusif** (0 SQLite), IT1-IT6 sur `GR_EMA_DISTRIBUTION`. |
| 4 | [TASK-025](DONE_DETAIL/TASK-025-cadrage-tva-solde-initial.md) | **Cadrage TVA solde initial** (`EC_Type=4`) : inventaire réel + décision PO | ✅ **livré et approuvé** (2026-07-09) — 34 lignes / 854 793 MAD, 0 TVA déclarable ; **décision PO = maintenir l'alerte** `SOLDE_INITIAL_NON_GERE` (statu quo, aucune implémentation). Voir DONE.md. |

### ⏸️ Backlog différé — domaines non gérés par le client (PO 09/07/2026)
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-031](TASKS/TASK-031-domaine-operation-bancaire-tva.md) | **Opération bancaire (frais bancaire) avec TVA** : domaine de déduction entièrement absent de notre code (legacy `GetDeclarationCommissionBancaire`, `<mp><id>=3`) → sélection + valorisation directe + intégration workflow/exports | ⏸️ **différé** — le client ne gère pas ce cas aujourd'hui ; hors chemin critique. |
| 2 | [TASK-032](TASKS/TASK-032-revue-depense-avec-tva.md) | **Revue dépense avec TVA** : (a) absence de filtre `WithTva` dans `GetDepenseSql`, (b) divergence de calcul (notre ventilation/prorata vs legacy direct `depense.MontantTva`) → vérifier + aligner sur base réelle | ⏸️ **différé** — à traiter avec TASK-031. |

> **Origine** : analyse d'écart ancien GRF ↔ notre code (09/07/2026). Le legacy a 3 sources de déduction (décaissement, dépense, **frais bancaire**) ; notre module en couvre 2 (frais bancaire absent) et traite la dépense différemment. Différé car non prioritaire pour le client.

### 🧹 Dette technique
✅ **Terminé** — TASK-026 livrée et approuvée (2026-07-09) : `Declaration.Core.Tests` compilable et vert (28/28, dont 3 tests chemin FGR), 6 projets de tests intégrés au `.slnx` (`Controle.Tests` filtrable sans DB). Build/test solution exhaustif rejoué par l'architecte (0 erreur, 75 réussites hors DB). Voir DONE.md.

### 📘 Documentation
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-029](TASKS/TASK-029-guide-fonctionnel-accessible-app.md) | **Guide fonctionnel accessible depuis l'app** : servir `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html` comme asset statique (`public/guide-fonctionnel-tva.html`) + entrée « Guide » dans la sidebar `App.tsx` (ouverture nouvel onglet) ; source unique = `DOCS/`, `public/` en miroir | 🎯 **prêt** — front-only, découplé des chemins critiques. Guide client v1 livré (design v0). |

> **Origine Track C** : le worker TASK-009 a mesuré ~0,5-1 s/facture (session COM Sage ouverte **par facture**) → ~35 min/2085. Cause réelle = pas de routing par origine (`EC_Type`) : tout partait à l'OM, même les FGR (détail dispo en SQL `RT_HISTOCOMPTA`). Modèle figé en mémoire (`grf-echeance-ectype-mapping`, `grf-tva-perf-lecture-sage`). **Découplé de TASK-009** (désormais livrée : le recalcul du contrôle vit dans `Declaration.Controle`).

### ✅ Track B — recalcul vs GRFN (base prod `GR_EMA_DISTRIBUTION`)
✅ **Terminé** — TASK-009 livrée et approuvée (voir table ✅ Fait). Comparaison réelle sur EMA `DT_Id=66`.

> **Débloqué** : accès base prod obtenu et validé via TASK-001 (`GR_EMA_DISTRIBUTION` mono-société `SO_Id=1` / Sage `[NEW_EMA DISTRIBUTION]`, nom à espace confirmé). Source de rapprochement retenue = **locale** `RT_MOUVEMENT.MV_Point`/`MV_PointDate` (option B).
> Track A et Track B sont **parallèles** : le pur avance pendant que la prod se débloque.
> **Contrôle = grilles interactives du front** (TASK-013), pas l'Excel. Les exports Excel/XML (010/011) sont des **artefacts de dépôt/archive** → repoussés **après le front**.

> Références : `CAHIER_DES_CHARGES.md` (contrat validé), `MODULE_DECLARATION_TVA.md` (analyse technique + plan §5).
> Doc OM = source unique : `D:\_vibe\objetmetiers\*.pdf`. Aucune réutilisation de code/DLL GRFN ou GOCOM.
