# TASK-019 — Poste de travail de déclaration piloté par le règlement (4 interrogations)

## Contexte
TASK-018 ✅ a livré (sur mock) un **hub « tour de contrôle »** centré sur un héros d'agrégats
(`Σ sources = Σ candidates`, écart 0, barre de ventilation). **Décision PO (08/07/2026), sur données
réelles :** cet écran est *« du n'importe quoi »* pour un comptable — il prouve l'intégrité **pour
l'éditeur**, pas le travail **pour celui qui déclare**. Un comptable ne regarde pas un total qui égale un
autre total ; il **interroge des objets métier**.

> ⚠️ **Cette task supersède le concept de TASK-018.** Le « hub tour de contrôle » (héros = écart 0) est
> **abandonné** au profit d'un **poste de travail piloté par le règlement**, structuré par les
> **4 interrogations réelles du comptable**. Les **6 règles de transparence de TASK-013 restent
> souveraines** et priment sur tout choix d'UI.

**Modèle métier établi avec le PO (discovery 08/07/2026) :**
- **Unité atomique = l'affectation** — le lien **règlement ↔ facture**, **partiel des deux côtés**
  (1 règlement solde 1..N factures ; 1 facture payée par 1..N règlements). Ce n'est ni le total de la
  facture ni le total du règlement qui est déclaré, mais **la TVA au prorata du réellement payé/affecté**
  (cf. [[tva-controle-ttc-vs-om]] : ne jamais se fier au total facture, comparer le réel des OM).
- **Déclencheur = le paiement rapproché** (régime des décaissements) : ce qui ouvre le droit à déduire,
  c'est le **règlement rapproché**, pas la facture. On **reporte le règlement**, pas la facture.
- **Périmètre système ≠ périmètre dépôt** : le système suit **tout** (Encaissement + Décaissement +
  Dépense + Frais bancaire → TVA **déductible ou collectée**) ; **seule la TVA déductible sort dans le
  XML DGI** (relevé de déductions). Les 4 domaines sont des **natures d'opération / facettes**, pas des
  étapes ni la distinction achat-vente.

## Décisions de cadrage (PO 08/07/2026 — remarques 1→5)
1. **Reportées = stock à part, HORS équilibre du mois.** Deux totaux distincts : l'« écart 0 » ne porte
   que sur les **rapprochées** (mois déclaré) ; les reportées sont un compteur séparé « ce qui attend le
   rapprochement ». Ne jamais mélanger les deux (sinon l'écart paraît faux).
2. **Conformité IF/ICE = 2ᵉ vague.** Le premier résultat livrable = Rapprochement + Affectation +
   Factures + reportées, **sans** l'interrogation Conformité (repoussée). TASK-020 garde le n° de
   règlement, repousse le flag IF/ICE.
3. **Collecté = citoyen à part entière de la déclaration système** (mêmes interrogations, décisions,
   faces, compte dans le solde) ; **filtré uniquement au moment de l'export XML** (déductible seul).
   → La question « collecté = vue propre ou consultation ? » est **tranchée** : traitement uniforme.
4. **Hub TASK-018 gardé en secours au début.** On ne supprime pas le hub d'emblée ; le nouveau poste de
   travail coexiste, et on nettoie le hub une fois le nouveau modèle validé au test.
5. **Référence de validation = Sage (source), PAS GRFN.** GRFN sert de cross-check partiel pour repérer
   les grosses erreurs, mais n'est pas la vérité (peu fiable). Un écart vs GRFN peut signifier que GRFN
   a tort (cf. [[tva-controle-ttc-vs-om]], TASK-009 = « écarts à investiguer », pas « coller à GRFN »).

## Périmètre STRICT
- **Uniquement** : le front `declaration-tva-web` — remplacer l'écran d'accueil (hub TASK-018) par un
  **poste de travail à 4 interrogations**. Réutilise l'existant (`CheckupPanel`, `DomainGrid`,
  `ExcelFilter` mode serveur, navigation, drill-down + bulk-sur-filtre TASK-016, toasts, design
  gocom-web).
- **Branchement (décision PO 08/07/2026) : API réelle directe, PAS de mock.** Le mock a produit le
  « n'importe quoi » de TASK-018 en masquant le réel N:N ; on construit sur les vraies données.
- **Exclu du front** : tout changement de calcul/sélection/persistance (back). Les manques identifiés
  sont livrés par un **avenant back préalable** (voir « Prérequis »), pas par le front ni par un mock.

## Prérequis — avenant back (API réelle) — À FAIRE AVANT le front
Vérifié dans le code (08/07/2026) : le socle affectation existe déjà dans `SelectionnerAffectationsService`
(`A.AF_Montant` = montant affecté partiel, `M.MV_Numero` = règlement/OM, `E.DO_Numero` = facture,
`M.MV_Point = Oui` = rapproché). Mais deux manques bloquent les 4 interrogations :

- **Gap A — DTO/persistance (léger).** `LigneCandidate` **ne conserve pas** le n° de règlement
  (`NumeroRapprochement`) : l'aplatissement le jette → impossible de **grouper par règlement** côté front.
  → Ajouter `NumeroRapprochement` (règlement) à `LigneCandidate` + persistance + exposition dans le DTO
  `GET /lignes` ; ajouter un **flag conformité IF/ICE** (les valeurs brutes `TiersIdentifiantFiscal` /
  `TiersICE` sont déjà là, il manque l'état vert/rouge). Enrichissement, pas de reshaping.
- **Gap B — sélection (sérieux, Track B / vraie DB).** La sélection ne ramène **que les rapprochés**
  (`MV_Point = Oui`) → les **reportées** (non rapprochées) ne sont jamais lues, donc la face « ce que je
  ne déclare PAS » est incomplète. → Étendre la sélection pour **surfacer les non-rapprochées** et les
  marquer `Reportee`. Touche l'éligibilité (famille TASK-008/015/017) et la base réelle.

> Le front TASK-019 consomme cet avenant. Gap A débloque les interrogations Rapprochement/Affectation/
> IF/ICE + la face « je déclare ». Gap B débloque la face « reportées ». Ces deux gaps sont à traiter en
> **task(s) back dédiée(s)** — cette task-ci reste **front-only**.

## Objectif
Faire de l'écran d'accueil d'une déclaration ouverte un **poste de travail organisé autour de ce que le
comptable interroge réellement** : **3 preuves en lecture seule** qui justifient chaque ligne, et **1
seule surface d'action**.

### 1. Squelette = 4 interrogations
Navigation par **4 interrogations** (remplace tuiles-domaines et héros-agrégats) :

| # | Interrogation | Type | Rôle |
|---|---|---|---|
| 1 | **Rapprochement** | preuve (lecture seule) | Le règlement est-il rapproché en banque ? → **éligible** ce mois / **reporté** |
| 2 | **Affectation** | preuve (lecture seule) | Répartition réelle du règlement sur les factures (partielle) → **montant réel, taux, nature déductible/collectée, domaine** |
| 3 | **Conformité IF/ICE** *(2ᵉ vague)* | preuve (lecture seule) | Fournisseur conforme ? vert / **rouge = rejet DGI** — to-do avant dépôt (correction en amont, pas ici). Reporté après le premier résultat. |
| 4 | **Factures à déclarer** | **action** | Le relevé de déductions, **deux faces** + décisions + clôture |

Les 3 preuves viennent de Sage → **lecture seule** (aucun write-back Sage). Elles ne servent qu'à
**justifier** : depuis n'importe quelle ligne des Factures, on **remonte à ses 3 preuves** (payé ? bien
affecté ? conforme ?).

### 2. La surface d'action = Factures à déclarer (deux faces)
- **Face « je déclare »** : les affectations éligibles, **éclatées par facture** → le relevé de
  déductions (déductible → XML DGI). Montant = **TVA au prorata réellement affecté**.
- **Face « je ne déclare pas »** — chaque ligne avec **motif en clair** (règle n°1) :
  - **Reportée** — règlement pas encore rapproché → repart au mois de rapprochement (on reporte le
    *règlement*).
  - **Écartée** (système) — non éligible, **motif affiché** (pas de TVA, hors période, doublon,
    nature non déductible…).
  - **Exclue** (manuelle) — choix du comptable, tracé et réversible.
- **Actions** (sur le règlement / l'affectation) : **exclure** · **réintégrer / annuler l'exclusion** ·
  **forcer le report**. Réutiliser l'action-de-masse-sur-filtre de TASK-016.
- **Geste final** : **clôturer**, verrouillé tant qu'il reste des lignes à décider / une anomalie
  bloquante (gating TASK-013 conservé), libellé du reste-à-faire.

### 3. Transparence = preuve sur l'objet, pas agrégat
- La preuve de confiance n'est **plus** « écart 0,00 MAD » en héros. C'est : *« clique sur n'importe
  quelle ligne → tu vois son règlement, son rapprochement, son affectation réelle, sa conformité »*.
- La **réconciliation** (Σ candidates = intégrées + exclues + reportées + écartées + proposées) reste
  garantie mais devient une **réassurance discrète** (barre/bandeau), pas le sujet central (règle n°2).

### 4. Domaines = facettes transverses
- Décaissement / Encaissement / Dépense / Frais bancaire deviennent des **filtres/facettes** applicables
  à chaque interrogation, **pas** des étapes ni des tuiles d'accueil. Le chargement **par domaine à la
  demande** (contrainte volume TASK-013) est conservé sous forme de filtre serveur.

## Contraintes techniques
- **6 règles de transparence TASK-013 souveraines** — rien de masqué : toute ligne (reportée / écartée /
  exclue) atteignable avec motif ; la réconciliation boucle toujours.
- **Volume** : lecture d'**agrégats** pour les vues de synthèse ; grilles **paginées serveur** à la
  demande (jamais tout charger). Contrainte n°5 TASK-013 intacte.
- **Réutiliser** `CheckupPanel`, `DomainGrid`, `ExcelFilter` (mode serveur), drill-down + bulk-sur-filtre
  TASK-016, toasts, design gocom-web. **Pas** de framework UI ni de gros composant nouveau.
- **Branché sur l'API réelle** (pas de mock) : le front consomme le DTO enrichi par l'avenant back
  (`NumeroRapprochement`, flag conformité IF/ICE, reportées surfacées). Aucun manque comblé côté front.
- React 19 + Vite + TS ; `oxlint` ; e2e `playwright`. `API_BASE` runtime + JWT Bearer inchangés.
- **Simple avant beau** : lisibilité et densité, pas de sur-UI.

## Étapes
1. Remplacer l'écran d'accueil (hub TASK-018) par la **navigation à 4 interrogations** (déclaration
   ouverte → poste de travail). Domaines rétrogradés en **filtres**.
2. **Interrogation Factures** (surface d'action) : deux faces (déclaré / non déclaré + motif), grilles
   serveur, actions exclure/réintégrer/forcer report (réutilise TASK-016).
3. **Drill « remonter aux preuves »** : depuis une ligne facture → panneau **Rapprochement + Affectation
   + IF/ICE** de son règlement (les 3 preuves lecture seule), via le `NumeroRapprochement` de l'avenant A.
4. **Interrogations Rapprochement / Affectation / IF/ICE** en vues de consultation (listes filtrables,
   états vert/rouge, éligible/reporté) — sans action d'écriture.
5. **Clôture** depuis le poste de travail avec libellé reste-à-faire + gating conservé.
6. **Réassurance discrète** : réconciliation (Σ) en bandeau, plus en héros. Densité, retrait de l'espace
   mort de l'écran actuel.
7. e2e Playwright : ouvrir déclaration → Factures (2 faces) → décision (exclure/réintégrer/report) →
   drill remonter-aux-preuves (rappro+affectation+IF/ICE) → clôture verrouillée tant que reste-à-faire.

## Livrables
- Front `declaration-tva-web` réorganisé : poste de travail **4 interrogations**, règlement/affectation
  comme socle, Factures = seule surface d'action à deux faces.
- Front branché sur l'API réelle enrichie par l'avenant back (Gap A + Gap B). Aucun mock de production.
- Tests Playwright du parcours.
- `VERIFY/TASK-019_verify.md` : captures (les 4 interrogations, les 2 faces des Factures, drill ligne →
  3 preuves, action exclure/réintégrer/report, clôture verrouillée avec reste-à-faire) + preuve que la
  réconciliation boucle et qu'aucune ligne (reportée/écartée/exclue) n'est masquée sans motif.

## Critères de validation
- Ouvrir une déclaration `EnCours` arrive sur le **poste de travail 4 interrogations**, pas sur un héros
  d'agrégats ni un domaine.
- **Factures = deux faces** : « je déclare » (déductible, TVA au prorata affecté, éclatée par facture) et
  « je ne déclare pas » (reportée / écartée+motif / exclue), toutes atteignables.
- Depuis une ligne facture, on **remonte à ses 3 preuves** (rapprochement, affectation réelle, IF/ICE).
- Rapprochement / Affectation / IF/ICE sont en **lecture seule** (aucune écriture, aucun write-back Sage).
- Actions **exclure / réintégrer / forcer report** tracées et réversibles ; action-de-masse-sur-filtre OK.
- **Clôture verrouillée** tant qu'il reste des lignes à décider / une anomalie bloquante ; libellé du
  reste-à-faire correct.
- **Réconciliation bouclée** (Σ candidates = intégrées + exclues + reportées + écartées + proposées),
  affichée en réassurance discrète — 6 règles de transparence sans régression.
- Seul le **déductible** part au dépôt XML ; le collecté reste visible dans le système sans sortir au XML.
- `oxlint` + build tsc/vite OK ; parcours e2e Playwright vert.

## Risques / dépendances
- **⚠️ GATÉE — validation données réelles** : avant lancement, confronter le modèle **règlement N:N
  partiel** aux vraies données (`GR_EMA_DISTRIBUTION` / `SO_Id=1`, TASK-001/017) pour vérifier que la
  ventilation partielle des deux côtés se lit et s'agrège correctement. Ne pas démarrer sans confirmation
  PO du rendu réel.
- **Supersession TASK-018** : le hub « tour de contrôle » livré sur mock est abandonné — décision PO
  explicite, tracée. Réutiliser ce qui reste pertinent (grilles, drill-down, clôture gardée), jeter le
  héros d'agrégats.
- **Dépendance dure = avenant back** (voir « Prérequis ») : Gap A (n° règlement + flag IF/ICE dans le
  DTO) et **surtout Gap B** (surfacer les reportées non-rapprochées — couche sélection, vraie base). Le
  front est **bloqué** tant que Gap A n'est pas livré ; la face « reportées » est bloquée tant que Gap B
  ne l'est pas. Gap B est du **Track B** (credentials/base prod).
- **Modèle N:N partiel** : risque principal = mal représenter la ventilation partielle (double compte ou
  perte). La réconciliation doit boucler **au niveau affectation**, pas au niveau facture ni règlement.
- **À trancher au démarrage** (PO) : l'ordre de présentation des interrogations (Factures en premier
  comme point d'entrée, preuves en appui ? ou parcours 1→N ?). *Le sort du collecté est tranché
  (décision 3) ; IF/ICE est en 2ᵉ vague (décision 2).*
