# TASK-134 — DDP : front (liste, fiche, sélection des lignes, contrôle)

## Contexte
Front de la Déclaration délai de paiement (CDC §3.1, §6), consommant TASK-131/132/133. Réutilise les
composants génériques déjà livrés côté GRF (`DomainGrid`, `ExcelFilter`, `ColumnSelector` TASK-068,
mêmes conventions de densité que le reste de l'app).

## Écrans à livrer
1. **Liste des déclarations DDP** : CRUD, ouverture fiche, colonnes statut/période/nb lignes/dépôt.
2. **Fiche déclaration** : création (exercice/type/trimestre/libellé), clôture/déclôture, dépôt (flag
   manuel), déclenchement génération fichier (TASK-133), affichage des lignes intégrées.
3. **Popup de sélection des lignes hors délai** : ouvert depuis une fiche déclaration `EnCours` — **la
   période de filtrage est toujours celle de la déclaration parente** (`DateDebut`/`DateFin` de l'entête),
   jamais une plage saisie librement (cf. point ci-dessous). Affiche les lignes candidates de TASK-131
   (avec `Depassement` incrémental déjà calculé), intégration manuelle multi-sélection.
4. **Écran de contrôle des lignes hors délai** (CDC §5.A-9) : écran de **simple visibilité/reporting**,
   indépendant du workflow d'intégration — ne doit jamais permettre d'intégrer une ligne à une déclaration
   depuis cet écran (rôle strictement séparé de la popup de sélection).

## Point corrigé (demande PO 19/07/2026) — filtre de période raisonné, pas une plage libre
Le legacy (`FrmControleLigneDelaisPaiement.cs:56-60`) utilise deux dates libres (`txtDateDebut`/
`txtDateFin`), sans lien avec le paramétrage société — confirmé en code, anomalie non documentée au CDC.
**Dans la réécriture, le filtre de période doit toujours raisonner en "période de déclaration"** :
- Popup de sélection (rattachée à une déclaration) : période = bornes de la déclaration parente, non
  modifiable par l'utilisateur.
- Écran de contrôle (autonome, CDC §5.A-9) : sélection d'un **exercice** + **type** (Annuelle/Trimestrielle)
  + **trimestre** si applicable — les bornes exactes sont **calculées** de la même façon que
  `DeclarationDelaisPaiementCreate` (TASK-132), jamais saisies en dates libres. Le type par défaut
  proposé vient du paramétrage société (`SO_TypeDecDP`, §7.1).

## Reprise manuelle "date de mise en route" (TASK-128)
Sur l'écran de contrôle : les lignes marquées "antérieure à la mise en route — retard réel inconnu"
(TASK-128/131) doivent afficher un badge explicite et une action de saisie manuelle ("déjà déclaré
jusqu'au [date]") — jamais un `Depassement` calculé automatiquement pour ces lignes tant que la reprise
n'a pas été faite.

## Périmètre STRICT
- **Inclus** : 4 écrans ci-dessus, cohérents avec la densité et les composants déjà en place.
- **Exclu** : branchement au menu (TASK-136 — cette tâche livre les écrans, pas leur point d'entrée),
  écran conventions (TASK-130), mesure du délai (TASK-135).

## Étapes
1. Liste + fiche déclaration (cycle de vie complet, branché sur TASK-132).
2. Popup de sélection (période = bornes de la déclaration parente, intégration multi-sélection).
3. Écran de contrôle (période raisonnée exercice/type/trimestre, jamais de plage libre ; badge de reprise
   manuelle pour les lignes antérieures à la mise en route).
4. Déclenchement génération fichier (TASK-133) avec affichage explicite des fournisseurs fautifs si le
   contrôle IF/ICE bloque (TASK-132).
5. Build tsc+vite / oxlint 0 erreur ; tests e2e Playwright (cycle complet : création → sélection →
   intégration → clôture → génération bloquée par IF/ICE manquant → correction → génération réussie →
   dépôt).

## Livrables
- 4 écrans + tests e2e.

## Critères de validation
- Parcours complet testé de bout en bout (création à dépôt), y compris le cas de blocage IF/ICE avec
  message explicite.
- Aucun filtre de date libre visible nulle part dans ce périmètre — toujours raisonné en période de
  déclaration.
- Build 0 erreur, e2e vert.

## Risques / dépendances
- Dépend de TASK-131/132/133 (tout le back du domaine DDP).
- Dépend du branchement menu (TASK-136) pour être atteignable en usage réel — peut être développé en
  parallèle avec une route directe temporaire.
