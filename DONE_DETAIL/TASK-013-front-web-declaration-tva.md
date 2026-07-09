# TASK-013 — Front web Déclaration TVA (workflow par domaine, optimisé volume)

## Contexte
Front web de **pilotage complet** de la déclaration TVA (décision PO). La v1 (`declaration-tva-web`)
était un modèle **« une seule grande grille, tout chargé côté client »** (`ControlGrid` + `ExcelFilter`
filtrant en mémoire) : correct en démo, **non tenable en volume réel** (des milliers d'affectations sur
4 domaines chargées et filtrées en JS).

**Décision PO (cadrage 08/07/2026) :**
- Les **données sources** (Sage commerciale + GRF trésorerie) sont lues **en lecture seule**.
- Le **process de déclaration, l'algorithme et l'état** sont **à nous** : notre module porte la
  déclaration dans **sa propre persistance** — **jamais** d'écriture dans les tables GRFN (`RT_*`).
- La déclaration suit un **cycle de vie** (création → intégration par domaine → checkup → clôture →
  génération), calqué sur la logique GRFN (§1bis `MODULE_DECLARATION_TVA.md`) mais **rejoué chez nous**.

> ⭐ **Rôle central inchangé** : les grilles + le checkup **sont** le « moyen de contrôle avant dépôt »
> demandé par le client (§1sexies). Les exports (Excel/XML) restent des **artefacts de dépôt**, produits
> **après** que le front a permis de contrôler.

### Compteur / numéro de déclaration
Format **déterministe** : `TVA{Societe}-{Exercice}-{Periode}`
(ex. `TVAEMA-2026-06` en mensuel ; `TVAEMA-2026-T2` en trimestriel).
⇒ **unicité** (société, exercice, période, type) : une seule déclaration par combinaison ; le front
bloque la re-création et propose d'**ouvrir** l'existante.

### Conventions de référence (gocom-web) à reprendre
- **Stack** : React 19 + Vite + TypeScript ; lint `oxlint` ; e2e `playwright`.
- **API** : `src/api.ts` axios, `API_BASE` runtime (`window.*_CONFIG?.API_BASE` /
  `import.meta.env.VITE_API_BASE` / `/api`), **JWT Bearer** en interceptor ; token en `sessionStorage`.
- **Grilles** : composant maison **`ExcelFilter`** — **réutilisé mais adapté en mode serveur**
  (cf. Contraintes). Colonnes configurables (`DEFAULT_COLUMNS` / `getAvailableColumns` /
  `renderSharedCell`), helpers `formatMoney`.
- **UI** : CSS maison (pas de framework), **toasts** (success/error/warning), layout sidebar +
  icônes `lucide-react`.

## Principe directeur — confiance & transparence (PRIORITÉ N°1)
> L'objectif du front n'est **pas** l'esthétique : c'est de **regagner la confiance du client**. Ce qui
> l'a perdue avec l'ancienne appli = des factures **disparaissaient en silence** (sauts silencieux de
> l'algo, §1bis/§2). Règle absolue :
>
> **Rien ne disparaît en silence. Tout est remonté, tout est expliqué, l'utilisateur sait toujours où
> il en est et quoi faire.**

Décliné en 6 règles (priment sur tout choix d'UI) :
1. **Aucune ligne silencieuse** — toute ligne écartée par le système apparaît, avec **son motif en
   clair** (chèque non rapproché, hors période, non affecté, non comptabilisé, taxe non à taux, facture
   introuvable). ⇒ chaque domaine expose une vue/état **« Écartées par le système »** (ce que
   l'ancienne appli n'avait pas). *(le back doit fournir le motif — cf. contrat lignes)*
2. **Réconciliation totale** — `candidates = intégrées + exclues + reportées + écartées(motif)` et
   `Σ sources = total`. Aucune ligne ne sort du décompte.
3. **Un seul fil conducteur** — stepper linéaire ; à chaque instant : *fait / en cours / à faire* + le
   *prochain pas*. L'utilisateur ne se perd jamais.
4. **Erreurs actionnables** — quoi / où (clic → la ligne) / quoi faire ; bloquant vs avertissement ;
   jamais de code cryptique.
5. **Langage clair** — messages en français métier, zéro jargon technique à l'écran.
6. **Traçabilité** — répondre en un clic à « pourquoi cette facture n'est pas dans la déclaration ? ».

> Conséquence sur l'UX : **simple avant beau**. Un flux linéaire lisible, des totaux qui bouclent, des
> erreurs explicites — pas d'écrans riches au prix de la clarté.

## Périmètre STRICT
- **Uniquement** : le front (écrans, workflow/stepper, grilles serveur, appels API, exports côté UI).
- **Exclu** : le calcul/sélection/persistance (backend TASK-012 + briques `Declaration.*`),
  l'authentification serveur, la génération des fichiers (produite par l'API).

## Objectif
Une SPA en **workflow piloté par le statut de la déclaration**, remplaçant l'écran-grille unique.

### Cycle de vie (statuts portés par notre persistance)
```
[Créer]  EnCours ──▶ (intégration par domaine) ──▶ [Checkup] ──▶ Clôturée ──▶ Générée (─▶ Déposée)
```

### Écrans
1. **Auth** : login → JWT (pattern axios/interceptor gocom-web).
2. **Liste des déclarations** : déclarations existantes (n°, société, période, statut, avancement) +
   bouton **Créer**.
3. **Création** : société + exercice + période (mois/trimestre) + type → n° auto
   `TVA{Societe}-{Exercice}-{Periode}` + contrôle d'unicité → statut **EnCours**.
4. **Espace déclaration (stepper)** : onglets par **domaine**, dans l'ordre
   **Décaissement → Encaissement → Dépense → Frais bancaire**. Chaque onglet :
   - grille **virtualisée** des lignes candidates du domaine, **paginée/filtrée/triée côté serveur** ;
   - **état par ligne** : décisions utilisateur *Proposée / Intégrée / Exclue / Reportée* (unitaire **et**
     en masse) **+** état système *Écartée* (non éligible) porteur d'un **motif en clair** — jamais
     masquée, filtrable comme les autres ;
   - **indicateur d'avancement** du domaine (n intégrées / n proposées, montant cumulé).
5. **Checkup** : panneau **agrégé** (récap par source / par taux / par code activité) +
   **contrôle d'équilibre** `Σ sources = total` + **anomalies typées** (bloquantes vs avertissements).
   Le checkup **conditionne la clôture** (bloquantes non résolues ⇒ clôture interdite).
6. **Clôture + Génération** : passage **Clôturée**, puis génération et **téléchargement** :
   - **N fichiers XML par domaine** (téléchargeables séparément et/ou en zip),
   - **état de checkup** (Excel),
   - **rapport d'anomalies**.

> **Hors workflow — diagnostic GRFN (TASK-009)** : le « Mode Contrôle vs GRFN » n'est **pas** un écran
> du cycle de vie. C'est un **outil de diagnostic éphémère** (comprendre les erreurs des anciennes
> déclarations pour ne pas les reproduire) → au mieux une vue annexe/rapport, **priorité basse**, pas
> dans le stepper principal.

## Contrat d'API attendu (à exposer par TASK-012 — voir Risques)
> Le front est développable **contre un mock** respectant ce contrat.

| Endpoint | Rôle | Note volume |
|---|---|---|
| `POST /declarations` | créer (société, exercice, période, type) → n° + id + statut | unicité 409 si existe |
| `GET /declarations` | liste (filtre société/exercice/statut) | pagination |
| `GET /declarations/{id}` | en-tête + statut + avancement par domaine (compteurs/totaux) | léger |
| `GET /declarations/{id}/lignes` | `?domaine=&page=&size=&sort=&filtre[...]` → **page** de lignes | **cœur volume** : pagination + filtre + tri **serveur** |
| `PATCH /declarations/{id}/lignes/{ligneId}` | changer l'état d'une ligne (Intégrer/Exclure/Reporter) | — |
| `POST /declarations/{id}/lignes:bulk` | décision en masse (sélection ou filtre courant) | évite N appels |
| `GET /declarations/{id}/checkup` | **agrégats** (source/taux/activité + équilibre + anomalies) | ne renvoie **pas** les lignes |
| `POST /declarations/{id}/cloture` | EnCours → Clôturée (refuse si anomalie bloquante) | — |
| `POST /declarations/{id}/generation` + `GET .../fichiers/{domaine|checkup|anomalies}` | produit et sert XML/Excel/rapport | fichiers = artefacts |

## Contraintes techniques (volume = priorité n°1)
1. **Chargement par domaine, à la demande** : on n'ouvre jamais les 4 sources ensemble ; seul l'onglet
   actif charge ses données.
2. **Pagination + filtre + tri côté serveur** : `ExcelFilter` **envoie les critères à l'API** ; il ne
   filtre plus un tableau complet en mémoire (adapter le composant gocom-web à ce mode).
3. **Grille virtualisée** (ex. `react-window`/`@tanstack/react-virtual`) : seules les lignes visibles
   dans le DOM.
4. **État par ligne persisté côté serveur** (pas dans le state React) : survit au reload et tient sur
   de gros volumes ; UI optimiste + réconciliation.
5. **Checkup = agrégats serveur** : l'écran de contrôle lit des totaux, jamais l'ensemble des lignes ;
   coût constant quel que soit le volume.
6. React 19 + Vite + TS ; `oxlint` ; e2e `playwright`.
7. `API_BASE` runtime (multi-client) ; **JWT Bearer**.
8. Réutiliser le **design + `ExcelFilter`** gocom-web (adapter, ne pas réinventer) ; pas de framework UI tiers.

## Étapes
1. **Refactor de cadrage** : figer le contrat d'API (ci-dessus) + un **mock** conforme (remplace
   `mockData.ts` en simulant pagination/filtre/tri/état serveur).
2. **Auth + layout + liste des déclarations** (sidebar, toasts) repris de gocom-web.
3. **Création de déclaration** (compteur `TVA{Societe}-{Exercice}-{Periode}` + unicité).
4. **Stepper par domaine** : navigation + indicateur d'avancement + garde de statut.
5. **Grille domaine serveur** : virtualisation + `ExcelFilter` mode serveur + actions état
   (unitaire + masse).
6. **Écran Checkup** : agrégats + équilibre + anomalies typées + gating de clôture.
7. **Clôture + génération** : déclenchement + téléchargement multi-XML / Excel checkup / rapport
   anomalies.
8. **Tests playwright** du parcours : créer → intégrer un domaine → checkup → clôture → génération.

## Livrables
- Front `React/Vite/TS` (`declaration-tva-web` **retravaillé**) : workflow par domaine, grilles
  serveur virtualisées, checkup, génération.
- Mock d'API conforme au contrat (pagination/filtre/tri/état/agrégats).
- Tests playwright du parcours principal.
- `VERIFY/TASK-013_verify.md` : captures (liste, création, stepper/domaine, checkup/anomalies,
  génération) + preuve du **mode serveur** (pas de filtrage client sur l'ensemble).

## Critères de validation
- La grille d'un domaine **ne charge jamais tout le jeu** : pagination/filtre/tri **serveur** prouvés
  (réseau : une page par requête, filtre = nouvel appel).
- Grille **virtualisée** (DOM borné) et fluide sur un jeu volumineux (mock ≥ quelques milliers de lignes).
- **État par ligne persisté** (survit au reload) ; actions unitaires **et** en masse.
- Compteur `TVA{Societe}-{Exercice}-{Periode}` correct + **unicité** respectée.
- Checkup : récap (source/taux/activité) + équilibre + anomalies typées (bloquant/avertissement) ;
  **clôture bloquée** si anomalie bloquante.
- Génération : **un XML par domaine** + Excel checkup + rapport anomalies téléchargeables.
- JWT Bearer + `API_BASE` runtime ; design cohérent gocom-web (composants réutilisés).
- Parcours e2e playwright vert.

## Risques / dépendances
- **⚠️ Contrat TASK-012 à étendre** : la TASK-012 actuelle expose un modèle **one-shot**
  (`POST /declarations/preview` → tout le `DeclarationModele`). Le workflow exige **persistance +
  endpoints paginés par domaine + état par ligne + agrégats checkup + génération**. ⇒ TASK-012 doit
  être révisée (persistance propre + endpoints ci-dessus). Le front avance sur **mock** en attendant.
- **⚠️ Persistance propre à créer** (backend) : store de la déclaration (en-têtes, lignes candidates
  figées à la création/rechargement, état par ligne, statut). **Jamais** dans les `RT_*` GRFN.
  Point d'archi à trancher hors de cette task (où/quel store).
- **⚠️ Format XML par domaine à confirmer** : seul le XML **décaissement/déductions**
  (`DeclarationReleveDeduction`, §3) est documenté. **Encaissement (TVA facturée/vente)** relève d'un
  **autre formulaire DGI** dont le schéma n'est **pas** encore identifié. Dépense/frais bancaire =
  probablement dans le relevé de déductions. ⇒ **mapping domaine → formulaire DGI à confirmer** avant
  de figer la génération multi-fichiers.
- **Réutilisation gocom-web** : `ExcelFilter` doit passer en **mode serveur** (émission de critères)
  au lieu du filtrage client — adaptation, pas simple copie.
- Données **réelles** dépendent de la chaîne backend + base prod (TASK-008/009) → jusque-là, démo sur
  mock/fixtures.
