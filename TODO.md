# TODO — Module Déclaration TVA (GRF)

## 🔴 CRITIQUE — bugs bloquants découverts sur cas réel (PO 13/07/2026)
✅ **TASK-076, TASK-077, TASK-078, TASK-080, TASK-081 et TASK-082 livrées et approuvées**
(2026-07-13/14) — voir `DONE.md`. TASK-081 (bandeau absent au premier figeage) et TASK-082
(bandeau absent pour une ligne `Exclue` dès le figeage, cas réel `FC2501717`/`EC_Id=21473`)
découvertes et corrigées ensemble sur le même signalement PO (bandeau ② toujours silencieux
après le correctif 081 seul).

> ⚠️ TASK-072 (incohérence Σ(HT+TVA) vs rapproché, cas PO 68 règlements) et TASK-071 (deadlock d'intégration) ont été découverts ensemble sur cas réel et sont désormais **DONE** — cf. `DONE.md`. TASK-075 (relecture ②③ après intégration) également **DONE** (2026-07-13, implémentée en worker exceptionnel, **non vérifiée en environnement réel** — cf. réserve dans `DONE.md`). TASK-076/077/078 (suivis directs de TASK-072, workflow complet de détection/décision sur une incohérence Sage) également **DONE** (2026-07-13 — TASK-078 vérifiée par relecture de code exhaustive, **non confirmée visuellement en réel**, cf. réserve dans `DONE.md`, « on teste au fur et à mesure »). Ne pas confondre avec TASK-069/070 (mise en page ③, déjà traitées).

## Ordre d'exécution consolidé (re-séquencé 08/07/2026)

Vérifié dans le code : les Gap A/B/C décrits par les tasks sont **confirmés**. Ordre retenu :

**Vague 1 — correctness + perf (débloque le reste)**
1. **TASK-022** routing `EC_Type` — correction (FGR ne doit jamais passer par l'OM) + gros gain perf. Touche le SELECT sélection + dispatcher orchestrateur + lecteur FGR SQL. Accélère aussi le recalcul de TASK-009.
2. **TASK-023** session Sage réutilisée — ✅ **livré et approuvé** (2026-07-09) : 1 `Open()` → boucle `ReadPiece` → 1 `Close()`, timeout par pièce, isolation d'erreur (`EnErreur`/motif) ; ×10,6 mesuré sur base réelle. Voir DONE.md.

**Vague 2 — valeur front (Track A)**
3. **TASK-020 §1** n° règlement sur `LigneCandidate` — ✅ **livré** (Gap A comblé).
3bis. **TASK-027** (2ᵉ vague de TASK-020, §2) conformité IF/ICE calculée au DTO via extraction du validateur TASK-011 — ✅ **livré** (source unique `ValidationIdentiteFiscale`, `Conformite` exposé au DTO).
4. **TASK-021** reportées — ✅ **livré** (Gap B comblé, motif honnête, volume réel 304 mesuré).
5. **TASK-019** poste de travail 4 interrogations — ✅ **livré** (front API réelle ; bug TVA 0% = correctif back Gap A extrait → TASK-030).

**Vague 3 — décisions & finalisation**
6. ~~**TASK-025** cadrage `EC_Type=4`~~ — ✅ **livré et approuvé** (2026-07-09) : inventaire réel (34 lignes, 0 TVA déclarable), décision PO = maintenir l'alerte (statu quo). Voir DONE.md.
7. **TASK-009** mode contrôle — ✅ **livré et approuvé** (matrice d'écarts réelle sur EMA `DT_Id=66` ; type-4 non polluant sur ce jeu, 14 `ManquantRecalcul` tous causés au bug `DT_Id` partiel GRFN).
8. ~~**TASK-028** verrou d'intégration déclaration~~ — ✅ **livré et approuvé** (2026-07-09, re-soumis après rejet PO) : tampon `RT_AFFECTATION.DT_Id` + triggers immuabilité `RT_MOUVEMENT`/`RT_AFFECTATION` (portée globale GRFN inclus). Trou d'intégrité collision `dtId` fermé (détamponnage borné + `& int.MaxValue`). Voir DONE.md.
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
| [TASK-022](DONE_DETAIL/TASK-022-routing-ectype-lecteur-fgr-sql.md) | Routing `EC_Type` + lecteur FGR SQL (111→`RT_HISTCOMPTA`, 0→OM, 4→alerte) | ✅ VERIFY approuvé — taux via `F_TAXE`/Sage (2 connexions, sans parsing), Σ(HT+TVA)=TTC, build 0 erreur + Orchestration.Tests 28/28 (rejoués par l'architecte). **✅ Preuve réelle fournie (2026-07-09, re-jouée en direct par l'architecte sur `.\sql2022`) : `FF260070`/`EC_Id=22297` = 451.80=376.50(D20)+75.30 (spec §47), part hors OM 56/745=7,52 %, perf 56 FGR ≈0,28 s ; multi-taux/exo absents de la base (86 FGR mono-bucket) → tests unitaires. Dette Core.Tests → TASK-026 ✅** |
| [TASK-027](DONE_DETAIL/TASK-027-conformite-if-ice-dto-lignes.md) | Conformité IF/ICE calculée au DTO + extraction du validateur TASK-011 (source unique) | ✅ VERIFY approuvé — `ValidationIdentiteFiscale` (source unique), exporter rebranché sans régression, `Conformite` exposé au DTO ; build 0 erreur + Core.Tests 25/25 + Export.Xml.Tests 4/4 (rejoués par l'architecte). Réserves non bloquantes : warnings xUnit1012/CS8602, Core.Tests hors `.slnx` (TASK-026) |
| [TASK-021](DONE_DETAIL/TASK-021-selection-reportees-non-rapprochees.md) | Avenant back (Gap B) : sélection des reportées (non-rapprochées `MV_Point≠Oui`) marquées `Reportee` + motif | ✅ VERIFY approuvé — SQL miroir (3 domaines), garde-fous universels priment sur `NonRapproche` (motif honnête, règle n°1), mapping `Reportee`, volume réel 304/1309 réconcilié ; Selection.Tests 13/13 hors DB (rejoués par l'architecte). **⚠️ Réserves : test d'intégration + volume 304 sur base prod live non re-jouable côté architecte ; H1 non bornée / H2 sortie manuelle (assumés 1er tour)** |
| [TASK-019](DONE_DETAIL/TASK-019-poste-travail-declaration-4-interrogations.md) | Poste de travail 4 interrogations (Rapprochement · Affectation · Conformité IF/ICE · Factures 2 faces) — front, API réelle ; supersède TASK-018 | ✅ VERIFY approuvé — 5 captures e2e Playwright (dont actions de masse + clôture verrouillée grisée), réconciliation 5 segments `total = candidates` (TASK-013), 3 vues de preuves différenciées, build tsc+vite + oxlint OK. Bug **TVA 0%** diagnostiqué = correctif back Gap A (`Ventilateur.cs`/`LecteurTvaFgr.cs`) **extrait → TASK-030** (preuve données réelles `GR_EMA_DISTRIBUTION`). |
| [TASK-009](DONE_DETAIL/TASK-009-mode-controle-vs-grfn.md) | Mode Contrôle : recalcul corrigé vs `RT_LigneDeclarationTva` (écarts GRFN) — `Declaration.Controle` | ✅ VERIFY approuvé — comparaison réelle EMA `DT_Id=66` (06/2026) : 3 173 concordants (99,6 %), 14 `ManquantRecalcul` (bug `DT_Id` partiel GRFN, causés), 95 `ManquantGRFN` (sauts silencieux, Σ\|AF\|=475 811,82 MAD). Corrections `SelectionnerAffectationsService` (JOIN `RT_ECHEANCE`→`DO_Numero` + filtre date conditionné `dtId`) vérifiées en source ; repo `SELECT` seul (lecture seule stricte) ; build 0 erreur. Réconciliation montants §2/§3.2/§5 corrigée avant approbation. |
| [TASK-023](DONE_DETAIL/TASK-023-lecture-sage-session-reutilisee.md) | Session Sage réutilisée (`EC_Type=0`) : 1 `Open()` → boucle `ReadPiece` → 1 `Close()`, cache devise/codes taxe, **timeout par pièce**, isolation d'erreur | ✅ VERIFY approuvé — thread STA unique (aucun `Parallel.ForEach`), pièce KO → `EnErreur`+motif (alerte `FACTURE_ILLISIBLE_OM`, jamais matérialisée en cache), N traité = N demandé. Preuves réelles `DISTRI_DEMO` : **12/12 montants identiques** (unitaire vs batch), **30,04 s → 2,84 s (×10,6)**. Build 0 erreur (rejoué par l'architecte), 76 tests / 0 échec. Réserves non bloquantes : timeout pièce interrompt le reste du lot (transparent, jamais silencieux) ; perf sur base démo 12 pièces (extrapolation prod argumentée). |

### 🎯 Track A — front poste de travail
✅ **Terminé** — TASK-019 livrée et approuvée (voir table ✅ Fait). Correctif back associé Gap A TVA : **[TASK-030](DONE_DETAIL/TASK-030-correction-gap-a-tva.md)** — ✅ **livré et approuvé** (2026-07-09).

#### 🖥️ Refonte front rappro/TVA — retour PO 09/07/2026 (module déclaration seul, GRC = simple exemple de style)
Décisions PO actées : **menu groupé multi-entrées à valeur ajoutée** (voir 035) — INTERROGATION (Rapprochement bancaire 🟢 *global* + Factures ⚪) / DÉCLARATION (Déclaration TVA 🟢 + Relevé de déductions ⚪) / À VENIR grisé (RAS · Télédéclaration · Tableau de bord 🔒) ; on **montre** toute la portée, on ne **build** que le cœur. Pivot rapprochement = **par règlement** (TVA sur décaissement) ; traçabilité = **colonnes de preuve inline + preuve à la demande**, **pas de panneau latéral**. Priorité cœur = rapprochement + valorisation TVA (la génération est triviale). Décompilation GénéraFi (09/07/2026) confirme l'architecture (cf. mémoire `generafi-reference-produit-tva`).
> **Ordre d'exécution confirmé (PO)** : 034 → 035 → 036 → 037 → 038 (shell menu remonté en #2 pour rendre la valeur visible tôt).

| Ordre | Task | Nature | Objet | État |
|---|---|---|---|---|
| 2 | [TASK-035](DONE_DETAIL/TASK-035-navigation-deux-entrees-densite.md) | front | **Shell menu groupé** (INTERROGATION / DÉCLARATION / À VENIR) + densité « 0 espace perdu ». Entrées cœur actives (Rappro 🟢, Déclaration 🟢), autres ⚪ placeholder / 🔒 grisées. Câble *Déclaration* → flux existant, *Rapprochement bancaire* → écran TASK-037 (placeholder tant que 037 non livré). | ✅ **livré et approuvé** (2026-07-09) — front-only, build tsc+vite / oxlint 0 erreur, aucune dérive vers les écrans « à venir ». Voir DONE.md. |
| 3 | [TASK-036](DONE_DETAIL/TASK-036-endpoint-interrogation-rapprochement-global.md) | **back** | **Endpoint interrogation rapprochement GLOBAL** (règlement-pivot, lecture seule stricte) : `RT_MOUVEMENT`/`RT_AFFECTATION`/`MV_Point`/`EC_Type`/`DT_Id`, filtré/paginé, **reste à affecter visible**. DTO explicite | ✅ **livré et approuvé** (2026-07-09) — `GET /api/rapprochement` SELECT-only (aucun write/DLL, `DT_Id` lu jamais écrit), reste à affecter exposé, preuve réelle `GR_EMA_DISTRIBUTION`, build 0 erreur + 15/15 tests. **Débloque 037.** Voir DONE.md. |
| 4 | [TASK-037](DONE_DETAIL/TASK-037-ecran-rapprochement-bancaire-front.md) | front | **Écran Rapprochement bancaire** (grille règlement-pivot dense : montant/mode/date/rapproché banque/factures affectées/reste/origine/déclaré + drill preuve TVA) | ✅ **livré et approuvé** (2026-07-09) — grille dense pivot règlement, reste à affecter ≠ 0 rendu visible (orange+⚠), drill-down à la demande, filtres/tri/pagination, build tsc+vite / oxlint OK. **Décision PO : drill-down agrégé (nb factures + montant affecté + reste + origine OM/FGR) accepté** comme niveau d'interrogation ; ventilation facture-par-facture = poste Déclaration. Voir DONE.md. |
| 5 | [TASK-038](DONE_DETAIL/TASK-038-tracabilite-tva-inline-declaration.md) | front (+DTO) | **Traçabilité TVA inline** dans « Factures à déclarer » : colonnes origine (`EC_Type` Sage/FGR/Solde)/taux/motif visibles + `ProofModal` à la demande, sans panneau latéral | ✅ **livré et approuvé** (2026-07-09) — colonne `origine` (source unique `ReglementRapprochementRow.LibelleEcType` partagée avec l'écran Rapprochement), snapshot `EcType` entité→SQL→DTO→front, `ProofModal` inchangé, build back/front/lint + 43/43 tests. Voir DONE.md. |
| 6 | [TASK-041](DONE_DETAIL/TASK-041-ecran-factures-interrogation.md) | back+front | **Écran « Factures » (INTERROGATION, filtre date obligatoire)** : grille facture-pivot (N°/date/fournisseur/réf/HT/TVA/autre taxe/écart/escompte/TTC/solde + statut déclaration 3 valeurs Non déclarable·Partiel·Total). Familles A (`RT_ECHEANCE`, gratuit) + C (`RT_AFFECTATION`/`DT_Id`, statut calculé) réelles ; famille B (HT/TVA/…) **depuis cache TASK-024** ou « non valorisé »+motif. **Lecture seule stricte.** Option 2 (sélection/déclaration partielle actionnable) **différée**. | ✅ **livré et approuvé** (2026-07-09) — `GET /api/factures` SELECT-only (période 400, `DT_Id` lu jamais écrit), `FacturesFromWhere` partagé liste+COUNT+distincts, statut 3 valeurs répliqué à l'identique C#↔SQL, famille B au cache TASK-024 sinon « non valorisé »+motif. Preuve réelle `GR_EMA_DISTRIBUTION` (1408 factures, borne Solde=TTC−Réglé, cache vide→100 % non valorisé, logique Partiel/Total prouvée what-if). Voir DONE.md. |

> ⚠️ Aucune fusion avec GOCOM/`gocom-web` (modules distincts, cf. mémoire `grc-vs-declaration-modules-distincts`). GRC = simple référence de **style** (dense, 0 espace perdu).
> **Ordre PO** : 034 → 035 → 036 → 037 → 038. **Chemin critique cœur** : 034 → **036 → 037** (035 shell et 038 traçabilité peuvent avancer en parallèle après 034, mais 035 est priorisé en #2 pour la visibilité de la valeur).

#### 🧭 Flux Déclaration TVA règlement-first — tunnel 8 écrans (PO 11/07/2026, `reflexion dectva.md`)
Décisions PO actées : **règlement-first assumé** (point d'entrée = les règlements décaissés) ; densité **0 espace perdu** (cohérent 035/037/041) ; justificatif = **`ProofModal` à la demande**, pas de panneau latéral. Front-only + réutilisation massive de l'existant (back déjà livré : sélection/figeage TASK-012/017, verrou TASK-028, valorisation TASK-022/023/024, contrôle TASK-009, exports TASK-010/011, conformité TASK-027). Le tunnel remplace le stepper 2 étapes (`DeclarationStepper.tsx`).

| Ordre | Task | Nature | Objet | État |
|---|---|---|---|---|
| — | _(tunnel 053→059 livré)_ | — | — | ✅ |

> **Séquentiel strict** : 053 → 054 → 055 → 056 → 057 → 058 → 059 (chaque étape consomme la sortie de la précédente). 053 peut être livré seul (étapes en placeholder honnête).
> **Écran §1 « Gestion déclarations »** (liste + création + statut/historique) : **déjà couvert** par `DeclarationList.tsx` + `CreateDeclarationModal.tsx` (TASK-012/013) — enrichissement colonnes (Lignes/TVA/Actions) à traiter dans 053 si manquant, pas de task dédiée.
> **Écran §7 « Justificatif »** : **pas de task dédiée** — `ProofModal` existant (TASK-038) enrichi timeline dans 055 (décision PO : preuve à la demande, pas de panneau latéral).
> ⚠️ Garde-fou règlement-first : ne **jamais** filtrer `MV_DECAISSE=1` (trou ~465 factures/mois, mémoire `grf-trou-selection-mv-decaisse`) — utiliser `MV_Domaine IN (0,1)`.

#### 🗺️ Roadmap produit — APRÈS le cœur (décision PO 09/07/2026)
Jalons **planifiés pour plus tard** (à cadrer en TASKS le moment venu, **pas maintenant** — priorité = rapprochement + valorisation TVA). Repère : GénéraFi (`generafi-reference-produit-tva`), sans copier.

| Jalon | Domaine | Note de cadrage (à approfondir au lancement) |
|---|---|---|
| R1 | **RAS fournisseurs** (Retenue à la Source) | Nouveau domaine de calcul + sélection ; s'appuie sur le même socle règlement/affectation. |
| R2 | **Télé-Déclaration** (SIMPL) | Dépend d'une déclaration complète et figée (verrou `DT_Id` déjà en place) ; format/transport à spécifier. |
| R3 | **Tableau de bord / indicateurs** | TVA Collectée / Récupérable / **Due**, courbes (Solde TVA, RAS). Dépend de la disponibilité des données Récupérable + RAS (donc après R1). Peut être plus aéré que les écrans de travail. |

> **Hors roadmap confirmée** (non mentionnés — restent à décider) : intégration **Comptabilité**, Sauvegarde/Restauration, Assistance. **TVA Collectée (ventes/clients) reclassée 14/07/2026 → [TASK-084](TASKS/TASK-084-ouverture-domaine-reglements-clients-tva-collectee.md)** (demande PO explicite, cf. §Ouverture domaine TVA Collectée ci-dessus).
> ⚠️ Ne pas dériver : on **finit 034→038** avant d'ouvrir R1/R2/R3.

### ⚡ Track C — Perf & sources de TVA par `EC_Type` (retour worker TASK-009)
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-023](DONE_DETAIL/TASK-023-lecture-sage-session-reutilisee.md) | **Session Sage réutilisée** (Type 0) : 1 `Open()` → boucle `ReadPiece` → 1 `Close()` + cache devise/codes taxe ; **pas** de `Parallel.ForEach` | ✅ **livré et approuvé** (2026-07-09) — thread STA unique, **timeout par pièce**, pièce KO isolée (`EnErreur`+motif → alerte `FACTURE_ILLISIBLE_OM`) sans perte. Preuves réelles `DISTRI_DEMO` : 12/12 montants identiques, **30,04 s → 2,84 s (×10,6)**. Voir DONE.md. |
| 2 | [TASK-028](DONE_DETAIL/TASK-028-verrou-integration-declaration-dt-id.md) | **Verrou d'intégration déclaration** : tampon `RT_AFFECTATION.DT_Id` + triggers immuabilité `RT_MOUVEMENT`/`RT_AFFECTATION` (portée globale, GRFN inclus ; réouverture = `DT_Id→NULL`) | ✅ **livré et approuvé** (2026-07-09) — re-soumis après rejet PO (trou d'intégrité collision fermé). Voir DONE.md. |
| 3 | [TASK-024](DONE_DETAIL/TASK-024-cache-ventilations-sage-check-delta.md) | **Cache ventilations Sage (gel par le paiement, validé à la lecture)** : fraîcheur = facture clôturée Sage au règlement (pas la déclaration) ; matérialisée à la 1re lecture, relue 100 % SQL ; validation du token de paiement local avant de servir (aucun code GRF ni trigger). `cbModification` abandonné (PO) | ✅ **livré et approuvé** (2026-07-09) — cache **SQL Server exclusif** (0 SQLite), IT1-IT6 sur `GR_EMA_DISTRIBUTION`. |
| 4 | [TASK-025](DONE_DETAIL/TASK-025-cadrage-tva-solde-initial.md) | **Cadrage TVA solde initial** (`EC_Type=4`) : inventaire réel + décision PO | ✅ **livré et approuvé** (2026-07-09) — 34 lignes / 854 793 MAD, 0 TVA déclarable ; **décision PO = maintenir l'alerte** `SOLDE_INITIAL_NON_GERE` (statu quo, aucune implémentation). Voir DONE.md. |

### ⏸️ Backlog différé — domaines non gérés par le client (PO 09/07/2026)
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-031](TASKS/TASK-031-domaine-operation-bancaire-tva.md) | **Opération bancaire (frais bancaire) avec TVA** : domaine de déduction entièrement absent de notre code (legacy `GetDeclarationCommissionBancaire`, `<mp><id>=3`) → sélection + valorisation directe + intégration workflow/exports | ⏸️ **différé** — le client ne gère pas ce cas aujourd'hui ; hors chemin critique. |
| 2 | [TASK-032](TASKS/TASK-032-revue-depense-avec-tva.md) | **Revue dépense avec TVA** : (a) absence de filtre `WithTva` dans `GetDepenseSql`, (b) divergence de calcul (notre ventilation/prorata vs legacy direct `depense.MontantTva`) → vérifier + aligner sur base réelle | ⏸️ **différé** — à traiter avec TASK-031. |

> **Origine** : analyse d'écart ancien GRF ↔ notre code (09/07/2026). Le legacy a 3 sources de déduction (décaissement, dépense, **frais bancaire**) ; notre module en couvre 2 (frais bancaire absent) et traite la dépense différemment. Différé car non prioritaire pour le client.

### 🚧 Blocage valorisation famille B (Sage OM) — isolé par la traçabilité (10/07/2026)
| # | Task | Objet | État |
|---|---|---|---|
| 5 | [TASK-060](TASKS/TASK-060-remontee-claire-erreurs-valorisation.md) | **Remontée claire des erreurs de valorisation à l'utilisateur** : le rafraîchissement facture-first juin 2026 affiche `240 en erreur` en agrégat, sans qu'aucun écran ne détaille la répartition par code. **Preuve code** : le backend calcule et transmet déjà le détail complet (`RapportValorisation.Erreurs[]`, `Code`/`Message`/`RefLigne`, `FacturesController.cs:107-126`), mais le front (`FactureInterrogation.tsx:174-196`) ne fait qu'un `console.warn` + toast agrégé renvoyant vers « la console / les logs serveur ». Objectif : agrégation par code + affichage lisible (modale/panneau), distinguant qualité-de-données tiers (`TIERS_SANS_ICE`/`IF`) des anomalies applicatives (`ERREUR_FGR`, `FACTURE_ILLISIBLE_OM`...). | 🎯 **prêt** — demande PO 11/07 ; lecture seule, aucune logique de valorisation touchée. Dépend de TASK-050/052 (DONE). |
| 6 | [TASK-061](DONE_DETAIL/TASK-061-montants-float-vers-decimal.md) | **Montants persistés en FLOAT au lieu de DECIMAL** : `dbo.LigneCandidate` (`DeclarationTVA.sql:49-54,70,74`) déclare `HT`/`Taux`/`TVA`/`TTC`/`Prorata`/`MontantAffecte` en `FLOAT`, alors que le modèle C# est déjà en `decimal` (`WorkflowEntities.cs:56-67`). Désalignement de type sur des montants de TVA → arrondis binaires + conversion Dapper. Passer en `DECIMAL(18,6)` (CREATE + ALTER ADD + commentaire) **et** ajouter un bloc `ALTER COLUMN` idempotent pour migrer les bases déjà déployées. Aucune logique C# touchée. | ✅ **implémenté** (2026-07-13) — DECIMAL(18,6) appliqué, migration idempotente avec gestion dynamique des contraintes de défaut incluse. Voir DONE.md. |
| 7 | [TASK-064](DONE_DETAIL/TASK-064-trigger-immuabilite-totale-affectation-declaree.md) | **Immuabilité TOTALE d'une affectation déclarée (trigger)** : le verrou TASK-028 (`003_Verrou_DT_Id.sql:83-117`, bloc 1b) ne bloque l'UPDATE que des colonnes **financières** (`AF_Montant`/`MV_Id`/`EC_Id`/`AF_Date`) d'une affectation `DT_Id NOT NULL` → toute autre colonne reste modifiable. Généraliser le bloc 1b : **tout** UPDATE refusé si `DT_Id NOT NULL` avant ET après, **sauf** le dé-tamponnage pur `DT_Id : valeur → NULL` (réouverture, à préserver pour `ReouvriDeclarationAsync`). DELETE (1a) et trigger `RT_MOUVEMENT` (2) inchangés. Fichier unique. | ✅ **implémenté** (2026-07-13) — Trigger d'immuabilité totale implémenté avec exception exclusive de dé-tamponnage pur. Voir DONE.md. |
| 8 | [TASK-065](TASKS/TASK-065-renommage-tables-conformes-dm-enttva-dm-lgtva.md) | **Renommer les tables en `DM_ENTTVA` / `DM_LGTVA` (nommage conforme)** : `DeclarationEntete`/`LigneCandidate` sont en PascalCase C# non conforme à la convention legacy (`PREFIXE_XX_`). Renommer **uniquement les tables SQL** (DDL `DeclarationTVA.sql` + index/FK + 22 littéraux `DeclarationRepository.cs`) via `sp_rename` idempotent ; **ne pas** toucher aux classes C#/DTO/TS (Dapper mappe par colonne). **Supprime aussi `001_Schema_TVA.sql`** (mort + incompatible `UNIQUEIDENTIFIER`). | ✅ **implémenté** (2026-07-13) — Tables, index et contraintes renommés via `sp_rename` idempotent, `001_Schema_TVA.sql` supprimé, repository mis à jour. |

> **Isolé grâce à la traçabilité** (mémoire `grf-valorisation-tracabilite-blocage-om`) : `logs/valorisation.log` + rapport HTTP `{ facturesTraitees, nbErreurs, erreurs[] }` + toast front. Les 30 `TIERS_SANS_ICE` + 30 `TIERS_SANS_IF` sont de la qualité de données, **pas** ce blocage.

### 🧹 Dette technique
✅ **Terminé** — TASK-026 livrée et approuvée (2026-07-09) : `Declaration.Core.Tests` compilable et vert (28/28, dont 3 tests chemin FGR), 6 projets de tests intégrés au `.slnx` (`Controle.Tests` filtrable sans DB). Build/test solution exhaustif rejoué par l'architecte (0 erreur, 75 réussites hors DB). Voir DONE.md.

| # | Task | Objet | État |
|---|---|---|---|
| 2 | [TASK-039](DONE_DETAIL/TASK-039-filtre-mv-domaine-rapprochement.md) | **Filtre `MV_Domaine` manquant sur l'endpoint rapprochement (TASK-036)** : un bordereau de remise `BORD26060022` remonte dans la liste des règlements. `RapprochementFromWhere` ne filtre pas la nature du mouvement → ajouter `MV_Domaine IN (0,1)` (0=encaissement, 1=décaissement), écarter bordereaux/virements/alimentations. Régression : filtre présent en TASK-001, perdu en TASK-036. | ✅ **livré et approuvé** (2026-07-09) — filtre `MV_Domaine IN (0,1)` sur `RapprochementFromWhere` (liste+COUNT partagés) + 2 sous-requêtes `distincts` ; preuve réelle `.\sql2022`/`GR_EMA_DISTRIBUTION` (BORD absent, 19 non-règlements exclus, 0 NULL, 694 règlements conservés), 0 erreur + 15/15 tests. Voir DONE.md. |
| 3 | [TASK-040](DONE_DETAIL/TASK-040-compteur-reglements-vs-filtres-grid.md) | **Compteur « Règlements : N » incohérent avec les filtres de la grille (rappro)** : `total` = `totalCount` serveur, alors que `numeroReglement`/`origine`/`domaine` sont filtrés **côté client sur la page seule** (`RapprochementInterrogation.tsx:146-157`) → compteur ≠ liste affichée. Fix : pousser ces 3 filtres **côté serveur** (WHERE partagé liste+count) pour rendre `totalCount` exact. | ✅ **livré et approuvé** (2026-07-09) — 3 filtres poussés dans `RapprochementFromWhere` (liste+COUNT), expressions `CASE` origine/domaine répliquant à l'identique `LibelleOrigine`/`LibelleDomaine`, filtrage client supprimé, multi-sélection `string[]` bout-en-bout. Build 0 erreur + 43/43 tests. Voir DONE.md. |
| 4 | [TASK-043](TASKS/TASK-043-tva-par-reglement-liste-rapprochement.md) | **Montant de TVA par règlement dans la liste de rapprochement (FGR + Sage)** : afficher, par règlement, la TVA de la facture/FGR. Enrichissement **de la page courante seule** (borné `size`), FGR via `LecteurTvaFgr`, Sage via cache TASK-024 + fallback session réutilisée TASK-023 ; prorata affectation partielle, somme multi-facture, état de valorisation explicite (jamais un `0` muet). Lecture seule, hors `COUNT`/tri. | ⏸️ **bloqué** — renommée TASK-041→**043** (collision avec l'écran Factures). back (projection + service + DTO endpoint TASK-036) + front. **Séquencer après TASK-039 et TASK-040** (mêmes projection/DTO ; 039 non livrée). Périmètre PO = FGR + Sage complet. |
| 5 | [TASK-062](TASKS/TASK-062-suppression-bouton-preuve-affectations.md) | **Supprimer le bouton « Preuve » de l'écran ② Affectations** : la modale `ProofModal` (3 onglets, `DomainGrid` filtré sur le n° rapprochement) est **redondante** avec la carte inline `FactureCard` — qui affiche déjà la preuve complète (payé÷TTC=%, TVA par taux, IF/ICE, conformité, origine) — et **non significative** (dump générique type 776 lignes `Encaissement/Proposée`, titre confondant règlement/rapprochement). Retirer bouton + câblage `ProofModal` dans `AffectationsDrill.tsx` **uniquement**. `DomainGrid.readonly` conservé (utilisé ailleurs). Front-only. | 🎯 **prêt** — décision PO 13/07 ; **recadré 13/07 (architecte)** : `ProofModal.tsx` **conservé** (partagé avec `WorkstationPanel.tsx` l.5/228 — sa suppression casserait le build) ; option « descendre aux lignes sources » écartée (tâche distincte si besoin). |
| 6 | [TASK-069](DONE_DETAIL/TASK-069-correctif-mise-en-page-ecran-calcul-tva.md) | **Correctif mise en page écran ③ Calcul TVA** : bandeau pied (total TVA à intégrer) rogné par la barre du stepper — conteneur `DeclarationStepper.tsx:155` (`flex:1`) sans `minHeight:0`, anti-pattern flexbox classique. Décision PO 13/07 : **option A** — bandeau interne du panneau conservé tel quel, correctif limité au layout. | ✅ **livré et approuvé** (2026-07-13) — `minHeight:0` ajouté au conteneur d'étape partagé, preuve visuelle PO confirmée (bandeau total visible, aucun recouvrement stepper). Voir DONE.md. **A révélé un 2e bug distinct** (bloc « Sous-totaux par taux » invisible, écrasé par le tableau factures) → extrait vers **TASK-070**. |
| 7 | [TASK-070](DONE_DETAIL/TASK-070-allegement-contenu-ecran-calcul-tva.md) | **Alléger l'écran ③ Calcul TVA** : retirer le tableau détail facture (déjà disponible en ② Affectations, colonne Taux filtrable + total TVA) ; ne garder que le bloc « Sous-totaux par taux TVA » + bandeau pied total. Corrige au passage le bug de visibilité du bloc sous-totaux (plus rien pour l'écraser) et la redondance signalée par le PO. Décision PO 13/07 : option retenue = alléger ③ (pas de fusion ②+③). | ✅ **livré et approuvé** (2026-07-13) — capture PO réelle (68 règlements/245 lignes), build 0 erreur. Voir DONE.md. |

### 🎨 Identité visuelle & branding produit (PO 13/07/2026)
Demande PO : la page d'authentification affiche aujourd'hui « Déclaration TVA » alors que la TVA n'est qu'un premier module d'une plateforme appelée à en accueillir d'autres (roadmap R1 RAS / R2 Télé-Déclaration / R3 Tableau de bord, cf. §Roadmap ci-dessous) — le nom de produit doit être distinct du nom de module. En parallèle, léger affinage du thème visuel (rester **simple**, mais choisir une teinte **élégante et propre au produit** plutôt que le bleu Material générique), sans toucher aux écrans denses existants. Direction retenue (clarification architecte 13/07) : **« Sobre + touche couleur signature »** (indigo profond + monogramme sidebar, pas de mode sombre).

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-082](TASKS/TASK-082-renommage-module-auth-declaratif-maroc.md) | **Renommer le titre de la page d'authentification en « Déclaratif Maroc »** (`Auth.tsx` + `index.html`) — nom de produit distinct du nom de module (« TVA » reste affiché comme module actif dans la sidebar post-connexion, non touché). | 🎯 **prêt** — front-only, texte seul, risque quasi nul. |
| 2 | [TASK-083](TASKS/TASK-083-theme-visuel-sobre-signature-declaratif-maroc.md) | **Thème visuel sobre + couleur signature** : nouvelle teinte d'accent (indigo profond, variables CSS `index.css`) + monogramme sidebar (`App.tsx`). Zéro écran de grille dense retouché (layout inchangé, seule la couleur d'accent se répercute via les variables). | 🎯 **prêt** — front-only, périmètre strict (5 variables CSS + 1 ajout sidebar), vérification visuelle avant/après requise en VERIFY sur écrans denses. |

> Séquencer 082 → 083 (les deux touchent `Auth.tsx` en zones disjointes) ou en parallèle si préféré — aucune dépendance technique bloquante entre elles.

### 🆕 Ouverture domaine TVA Collectée — règlements clients (PO 14/07/2026)
Demande PO : le step1 (écran ① Règlements) ne traite que les règlements **fournisseurs**
(`MV_Domaine=1`) ; il faut aussi gérer les règlements **clients** (`MV_Domaine=0`), avec les
mêmes règles d'éligibilité, en les **distinguant** (TVA collectée ≠ TVA déductible). Reclasse
« TVA Collectée » hors de la case roadmap « non mentionnés — restent à décider » (cf. §Roadmap
ci-dessous) — **à faire confirmer explicitement par le PO** avant lancement (arbitrage vs R1/R2/R3).

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-084](TASKS/TASK-084-ouverture-domaine-reglements-clients-tva-collectee.md) | **Ouverture domaine règlements clients (`MV_Domaine=0`)** dans le tunnel : socle SQL déjà partiellement écrit (TASK-015/021, jamais exercé sur le chemin de figeage réel) ; export XML DGI **bloqué** (TASK-014 : schéma TVA Collectée `⛔ inconnu`) → v1 = sélection+calcul+front, export explicitement différé. | 🎯 **prêt** — ⚠️ nécessite confirmation PO du reclassement roadmap avant lancement. |

### 🧮 Colonne TTC — écran ③ Calcul TVA (PO 14/07/2026)
Demande PO (capture écran ③ Calcul TVA) : ajouter une colonne « Total TTC » (= Total HT + Total TVA) dans le tableau des sous-totaux par taux, par ligne de taux et sur `Σ Total`. Affichage pur, aucune donnée nouvelle requise (dérivé de `totalHT`/`totalTVA` déjà présents en front).

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-085](TASKS/TASK-085-colonne-ttc-calcul-tva.md) | **Colonne TTC** sur le tableau des sous-totaux TVA (`CalculTvaPanel.tsx`) : entête + ligne par taux + `Σ Total`, front-only, aucun changement back/DTO. | 🎯 **prêt** — front-only, périmètre trivial (3 emplacements), vérification visuelle requise en VERIFY. |

### 🔍 Écran ④ Intégration — récap vide, écart sans détail, avertissements tronqués (PO 14/07/2026)
Demande PO (capture écran ④ Intégration) : (1) le bloc « Récapitulatif de l'intégration » en haut
s'affiche vide ; (2) le contrôle « Équilibre comptable » affiche un écart global sans détail et
son libellé prête à confusion ; (3) les avertissements « Ligne exclue : ... » n'indiquent ni
référence ni montant. Analyse code (architecte) : dans les 3 cas, le backend expose déjà les
données utiles (`recapSource`/`recapTaux`/`refLigne`/`filtre` via `/checkup`), mais
`IntegrationPanel.tsx` ne les consomme pas (troncature d'affichage / état front non robuste), pas
un manque de données en base ou en API.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-086](TASKS/TASK-086-recap-integration-vide-fallback-api.md) | **Récap vide** : source des chiffres = réponse `/checkup` (déjà chargée) au lieu des props volatiles de l'étape ③, robuste au rechargement de page / ouverture directe sur ④. | 🎯 **prêt** — front-only, lecture seule d'un contrat déjà exposé. |
| 2 | [TASK-087](TASKS/TASK-087-detail-ecart-equilibre-comptable.md) | **Détail de l'écart + renommage** du contrôle « Équilibre comptable » : afficher la ventilation `recapSource`/`recapTaux` déjà disponible sous le badge BLOQUANT ; libellé à trancher par le PO (options proposées dans la task). | 🎯 **prêt** — ⚠️ nécessite validation PO du nouveau libellé avant implémentation. |
| 3 | [TASK-088](TASKS/TASK-088-detail-avertissements-lignes-exclues.md) | **Détail des avertissements** : afficher `refLigne` (référence facture/pièce, déjà renvoyée par l'API) pour chaque « Ligne exclue », drill-down optionnel via le mécanisme TASK-016. | 🎯 **prêt** — front-only, lecture seule d'un contrat déjà exposé. |

> Aucune dépendance bloquante entre elles ; les 3 touchent `IntegrationPanel.tsx` (zones
> distinctes) — séquencer si livrées ensemble pour éviter les conflits d'édition.

### 🎛️ UX grilles — filtres « valeurs disponibles » & sélecteur de colonnes (PO 13/07/2026)
Demande PO (capture écran ② Affectations) : (1) chaque filtre de colonne doit proposer **la liste des valeurs réellement présentes** (type Excel : cases + recherche) sur **toutes** les listes ; (2) **sélecteur de colonnes** persistant (`localStorage`) sur toutes les listes. `ExcelFilter` gère déjà le mode `'list'` — le travail porte sur l'alimentation des options et un nouveau composant colonnes.

| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-068](DONE_DETAIL/TASK-068-selecteur-colonnes-persistant-listes.md) | **Sélecteur de colonnes persistant (`localStorage`) sur toutes les listes** : hook `useColumnPrefs` + composant `ColumnSelector` réutilisables, câblés aux 6 grilles (clé de stockage par écran). Rend entête+corps sur `visibleColumns`, repli sûr si préférence absente. Front-only. | ✅ **livré et approuvé** (2026-07-13) — 6 grilles câblées, entête/corps cohérents (y compris virtualisé), repli sûr, build tsc+vite/oxlint 0 erreur, e2e Playwright vert. Réserve non bloquante : `DomainGrid` partage une clé unique entre ses 3 contextes d'usage. Voir DONE.md. |

> ⚠️ TASK-068 touche **les mêmes fichiers de grille** que TASK-067A/B (`ReglementsSelection`, `FactureInterrogation`, `RapprochementInterrogation`, `AffectationsDrill`, `DomainGrid`, `ControlGrid`), déjà stabilisés. Aucune dépendance bloquante restante.

### 🔐 Gouvernance & traçabilité
✅ **Terminé** — TASK-074 (authentification réelle GRF), TASK-073 (garde `UT_Admin=1` + audit
réouverture) et TASK-079 (suppression déclaration EnCours + écran liste enrichi) livrées et
approuvées (2026-07-13), dans cet ordre. Voir DONE.md. Périmètres C (exposition UI) et D (test
d'intégration automatisé) de TASK-073 non traités — non bloquants, laissés à la discrétion du PO
(pas de task de suivi ouverte, à recréer si le PO le demande). **TASK-079 porte une réserve non
levée** (aucune preuve réelle en base rejouée dans cette session, cf. DONE.md) — à confirmer par
le PO avant usage réel en suppression. **Complément** : TASK-080 (exclusivité règlement entre
déclarations `EnCours` concurrentes) ✅ **livrée et approuvée** (2026-07-14, preuve réelle rejouée
contre `.\sql2022`/`GR_EMA_DISTRIBUTION`) — voir `DONE.md`.

### 📘 Documentation
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-029](TASKS/TASK-029-guide-fonctionnel-accessible-app.md) | **Guide fonctionnel accessible depuis l'app** : servir `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html` comme asset statique (`public/guide-fonctionnel-tva.html`) + entrée « Guide » dans la sidebar `App.tsx` (ouverture nouvel onglet) ; source unique = `DOCS/`, `public/` en miroir | 🎯 **prêt** — front-only, découplé des chemins critiques. Guide client v1 livré (design v0). |

### 📦 Déploiement
| # | Task | Objet | État |
|---|---|---|---|
| 1 | [TASK-044](TASKS/TASK-044-deploiement-mono-service-mono-dossier.md) | **Déploiement mono-service / mono-dossier** : **un seul** service Windows (l'API) qui appelle tout (worker OM inclus) et sert le front, dans **un seul** dossier deploy. Socle déjà en place (front `wwwroot`, `WorkerExePath` relatif à l'exe, `logs/` à côté de l'exe). Reste : `publish.ps1` (build front + `dotnet publish` API + worker net48 → `deploy/`), `connections.json` de déploiement (chemins relatifs, secret hors dépôt), install service (compte Sage+SQL), `DOCS/DEPLOIEMENT.md`. | 🎯 **prêt** — indépendant de TASK-045. Aucune modif métier/worker. |

> **Origine Track C** : le worker TASK-009 a mesuré ~0,5-1 s/facture (session COM Sage ouverte **par facture**) → ~35 min/2085. Cause réelle = pas de routing par origine (`EC_Type`) : tout partait à l'OM, même les FGR (détail dispo en SQL `RT_HISTOCOMPTA`). Modèle figé en mémoire (`grf-echeance-ectype-mapping`, `grf-tva-perf-lecture-sage`). **Découplé de TASK-009** (désormais livrée : le recalcul du contrôle vit dans `Declaration.Controle`).

### ✅ Track B — recalcul vs GRFN (base prod `GR_EMA_DISTRIBUTION`)
✅ **Terminé** — TASK-009 livrée et approuvée (voir table ✅ Fait). Comparaison réelle sur EMA `DT_Id=66`.

> **Débloqué** : accès base prod obtenu et validé via TASK-001 (`GR_EMA_DISTRIBUTION` mono-société `SO_Id=1` / Sage `[NEW_EMA DISTRIBUTION]`, nom à espace confirmé). Source de rapprochement retenue = **locale** `RT_MOUVEMENT.MV_Point`/`MV_PointDate` (option B).
> Track A et Track B sont **parallèles** : le pur avance pendant que la prod se débloque.
> **Contrôle = grilles interactives du front** (TASK-013), pas l'Excel. Les exports Excel/XML (010/011) sont des **artefacts de dépôt/archive** → repoussés **après le front**.

> Références : `CAHIER_DES_CHARGES.md` (contrat validé), `MODULE_DECLARATION_TVA.md` (analyse technique + plan §5).
> Doc OM = source unique : `D:\_vibe\objetmetiers\*.pdf`. Aucune réutilisation de code/DLL GRFN ou GOCOM.
