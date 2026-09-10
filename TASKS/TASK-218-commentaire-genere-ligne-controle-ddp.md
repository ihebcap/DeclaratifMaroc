# TASK-218 — Commentaire généré automatiquement expliquant chaque ligne (écran Contrôle DDP)

## Contexte

Demande PO (10/09/2026), complémentaire à TASK-217 (tooltips + renommage des colonnes de bornes),
mais de portée distincte : plutôt que de faire déduire le sens de chaque ligne du contrôle à partir
de 5-6 colonnes séparées (Échéance légale, Origine du délai, Dernière déclaration, Constaté le,
Dépassement, Mode, Cas), afficher un **texte généré automatiquement, par ligne**, qui explique en une
phrase pourquoi cette ligne figure dans le contrôle et comment son dépassement a été calculé.

Objectif PO : un utilisateur (y compris le PO lui-même) doit pouvoir comprendre une ligne SANS
recomposer mentalement la logique incrémentale (bornes + anti-double-déclaration) à partir de
plusieurs colonnes séparées.

Exemple de texte cible (à ajuster selon les cas réels du calculateur,
`SelectionDelaiPaiementCalculator.cs` bucket par bucket) :
- Ligne payée, retard nouveau : *« Échéance légale le 07/04/2026 (convention 90 j). Réglée le
  20/07/2026, rapprochée le 06/08/2026. Déjà déclarée jusqu'au 30/06/2026 → 20 jours de retard
  nouveaux comptés sur cette période. »*
- Ligne non payée : *« Échéance légale le 08/06/2026 (défaut société 60 j), toujours impayée. Déjà
  déclarée jusqu'au 30/06/2026 → 92 jours de retard nouveaux comptés jusqu'à la fin de la période
  (30/09/2026), et continueront à courir tant que non réglée. »*
- Ligne en reprise manuelle requise : *« Échéance légale le [date], antérieure à la date de mise en
  route du module et sans historique de déclaration : le retard déjà couvert doit être saisi
  manuellement avant intégration (bouton "Reprise manuelle"). »*

## Objectif
```
Entrée  : comprendre une ligne du contrôle DDP exige de recomposer mentalement 5-6 colonnes (Origine
          du délai, Dernière déclaration, Constaté le, Dépassement, Mode, Cas) — aucune synthèse en
          langage naturel n'existe.
Traitement : générer côté backend (ou front, à trancher à l'étape 1) un texte en une ou deux phrases
             par ligne, dérivé directement des mêmes champs déjà calculés par
             SelectionDelaiPaiementCalculator (aucune nouvelle donnée métier, uniquement une mise en
             phrase des champs existants), gabarit différent selon le bucket/statut de la ligne.
Sortie  : chaque ligne du contrôle porte un texte explicatif autonome, lisible sans avoir à croiser
          les autres colonnes, fidèle au calcul réel (jamais un texte générique qui masquerait un cas
          particulier).
```

## Périmètre STRICT

- **Inclus** :
  1. Choix d'implémentation (backend vs front) à trancher en étape 1 — recommandation par défaut :
     **backend**, dans `SelectionDelaiPaiementCalculator`/`DeclarationDelaiPaiementDto`, pour garder
     une seule source de vérité sur la mise en phrase (le front ne fait alors qu'afficher un champ
     `commentaire`/`explication` déjà prêt) ; à documenter le choix retenu et pourquoi dans le VERIFY
     si le WORKER juge le front préférable (ex. si la logique de gabarit est jugée trop UI-only).
  2. Un gabarit de texte par **bucket**/statut réel du calculateur (`BucketDelaiPaiement`,
     `StatutLigneDelaiPaiement`) — ne pas se limiter aux 2 exemples payé/non payé ci-dessus : couvrir
     aussi `HorsPeriodePartAffectee`/`HorsPeriodePartNonAffectee` (cas 1/2 du calculateur) et
     `RepriseManuelleRequise`, distinctement, chacun avec ses données pertinentes (pas de texte
     générique qui ignorerait le cas réel de la ligne).
  3. Affichage de ce texte dans l'écran (`ControleLignesDelaiPaiementPanel.tsx`) — colonne dédiée,
     ou info-bulle au survol de la ligne / icône dédiée : le choix de présentation est laissé au
     WORKER, à condition que le texte reste lisible sans action supplémentaire (pas caché derrière un
     clic qui ouvre un modal, par exemple) et sans casser la densité de la grille existante.
  4. Cohérence stricte avec les valeurs déjà affichées dans les autres colonnes de la ligne (aucune
     incohérence entre le texte généré et Origine du délai/Dernière déclaration/Constaté le/
     Dépassement affichés par ailleurs) — le texte doit être un dérivé fidèle, jamais une source
     parallèle qui pourrait diverger.
- **Exclus / hors périmètre** :
  - Le renommage des colonnes et les tooltips de header — traités par TASK-217, ne pas dupliquer ce
    travail ici (les deux TASKs sont complémentaires mais indépendantes techniquement).
  - Un champ de commentaire **saisi manuellement** par l'utilisateur (persisté en base) — cette TASK
    ne couvre qu'un texte **généré**, en lecture seule, recalculé à chaque chargement, jamais stocké.
  - Traduction/i18n — français uniquement, comme le reste de l'écran actuel.
  - Modifier la logique de calcul du dépassement/bornes elle-même — cette TASK ne fait que mettre en
    phrase des valeurs déjà calculées.

## Étapes
1. Trancher backend vs front pour la génération du texte (voir recommandation ci-dessus), documenter
   le choix.
2. Lister tous les cas distincts à couvrir : croiser `BucketDelaiPaiement` (DansPeriodePartAffectee,
   DansPeriodePartNonAffectee, HorsPeriodePartAffectee, HorsPeriodePartNonAffectee) avec
   `StatutLigneDelaiPaiement` (Candidate, RepriseManuelleRequise) et l'état payé/non payé — vérifier
   dans `SelectionDelaiPaiementCalculator.cs` qu'aucune combinaison réelle n'est oubliée.
3. Rédiger un gabarit de phrase par cas, en réutilisant le vocabulaire retenu par TASK-217 (« Dernière
   déclaration », « Constaté le ») pour rester cohérent entre les deux TASKs — coordination requise
   si TASK-217 est livrée après TASK-218 (sinon les libellés utilisés dans le texte généré risquent de
   ne plus correspondre aux nouveaux noms de colonnes).
4. Implémenter, tester sur des cas réels couvrant chaque bucket/statut (au moins un exemple par cas
   dans le VERIFY, avec capture d'écran).
5. `npm run lint` + `npm run build` (et `dotnet build DeclarationTVA.slnx` si backend touché) → 0
   erreur.

## Livrables
- Code de génération du texte (backend ou front selon le choix de l'étape 1) + affichage dans
  `ControleLignesDelaiPaiementPanel.tsx`.
- `VERIFY/TASK-218_verify.md` : captures d'écran couvrant AU MOINS un exemple par bucket/statut
  (payé dans période, payé hors période part affectée, non payé, reprise manuelle requise), logs
  build/lint, checklist UI de `DOCS/UI_STANDARDS.md` cochée.

## Critères de validation
- Chaque ligne affichée dans le contrôle porte un texte explicatif cohérent avec son bucket/statut
  réel — aucun texte générique ou approximatif qui gommerait un cas particulier.
- Le texte reste synchronisé avec les colonnes déjà affichées (pas de contradiction possible entre le
  texte et Origine du délai/Dernière déclaration/Constaté le/Dépassement).
- Terminologie cohérente avec TASK-217 si celle-ci est déjà livrée au moment de la review.
- `npm run lint` + `npm run build` → 0 erreur (+ `dotnet build` si backend touché).

## Risques / dépendances
- **Dépendance de coordination avec TASK-217** (pas de dépendance technique bloquante, mais risque de
  divergence de vocabulaire si les deux TASKs sont menées par des WORKERs différents sans relecture
  croisée) — signaler ce risque en review VERIFY si les deux TASKs sont livrées dans un ordre
  inversé ou en parallèle.
- Risque de complexité de maintenance si le nombre de gabarits de phrase grandit (buckets +
  statuts + payé/non payé) sans factorisation claire — vérifier en review que la structure du code
  reste lisible (ex. une fonction par cas plutôt qu'un empilement de conditions imbriquées).
