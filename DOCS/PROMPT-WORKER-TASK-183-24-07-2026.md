Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md`, puis le
fichier TASK ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Contexte

Signalement PO (24/07/2026, capture d'écran ③ Vérifier & Intégrer) : dans le drill « Codes activité »
ouvert depuis l'onglet Déductible (Achats), impossible de voir/affecter les codes activité côté
Encaissement (Ventes) sans quitter entièrement le drill. Diagnostic architecte (code + base réelle
`GR_EMA_DISTRIBUTION`) : **ce n'est pas un bug de mapping ni de sauvegarde** — référentiel
(`P_DECTVAACTIVITE`), endpoint (`GET /api/codes-activite`) et persistance (`DM_LGTVA`) sont déjà
strictement symétriques entre les deux domaines. La cause réelle est un pur trou d'ergonomie front : le
bandeau du drill n'offre qu'un bouton « ← Retour au contrôle », les deux onglets qui pilotent le domaine
étant rendus uniquement dans la vue principale, masqués dès qu'un drill est ouvert.

## Ta mission

Traiter **TASK-183-drill-codes-activite-toggle-domaine-sans-sortie.md** (dossier `D:\_vibe\GRF\TASKS\`)
— priorité MEDIUM, front seul, aucune dépendance back.

Vérifie d'abord que les champs Objectif/Périmètre/Livrables/Critères de validation sont bien remplis
(ils le sont). Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les
erreurs, puis écris `VERIFY/TASK-183_verify.md` en suivant le même niveau de détail que
`VERIFY/TASK-144_verify.md` (sers-t'en de modèle) : section Périmètre livré, Fichiers modifiés,
Checklist, **et une section "Reste à valider" honnête si tout n'a pas pu être vérifié** — ne déclare
jamais un point validé si tu ne l'as pas réellement vérifié.

**Exécution en parallèle** : un autre worker traite TASK-182 en même temps sur ce même dépôt
(`Declaration.Export.Excel/Exporter.cs`, backend). Aucun fichier commun avec cette TASK (front seul,
`VerifierIntegrerPanel.tsx`) — travaille normalement, mais **ne stage/commit que les fichiers de ton
propre périmètre** (jamais `git add -A`), pour ne pas embarquer un fichier encore en cours de
modification par l'autre agent.

## Règles de travail (non négociables)

- **Portée exacte** : `declaration-tva-web/src/VerifierIntegrerPanel.tsx`, uniquement le bandeau du
  drill (lignes ~636-666 au moment de la rédaction de la TASK — vérifie les numéros réels avant
  d'éditer, le fichier a pu bouger), et uniquement pour `drillFiltre.kind === 'codeActivite'`. Ne
  touche pas aux 3 autres kinds de drill (`incoherence`, `toutes`, `anomalie`), ni aux onglets de la vue
  principale (lignes ~756-797, déjà fonctionnels, ne pas les dupliquer inutilement — le nouveau toggle
  doit réutiliser `setDrillFiltre` avec la même forme d'objet que les boutons existants lignes
  978/997, pas une nouvelle logique de fetch).
- **Aucun changement back** : le mécanisme de rechargement (référentiel + grille) est déjà réactif à un
  changement de `drillFiltre.domaine` (`useEffect` ligne ~327-341 pour le référentiel, prop `domaine`
  de `DomainGrid` pour la grille) — si tu penses avoir besoin de toucher un fichier `.cs`, arrête-toi et
  documente pourquoi dans le VERIFY plutôt que d'étendre le périmètre.
- Le bouton du domaine actif (`drillFiltre.domaine`) doit être visuellement marqué actif — même
  traitement que les onglets principaux (bordure/couleur), pour la cohérence visuelle.
- **Tu as un accès réel à la base de données** : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d
  GR_EMA_DISTRIBUTION -Q "..."` (identifiants complets dans `D:\_vibe\GRF\connections.json`). Utilise-le
  pour la preuve réelle demandée par la TASK : après avoir affecté un code activité à une ligne
  Encaissement via le nouveau toggle (sans repasser par « Retour au contrôle »), vérifie en base que
  `DM_LGTVA.CodeActivite` a bien été mis à jour pour cette ligne précise.
- Test réel obligatoire (navigateur ou Playwright) : ouvrir le drill depuis Achats, basculer vers Ventes
  via le nouveau toggle (pas via un retour + réouverture), confirmer que la grille et le référentiel de
  codes changent bien de domaine, affecter un code sur une ligne Ventes, vérifier la persistance en
  base, puis re-basculer vers Achats sans sortir du drill.
- Build front (`cd declaration-tva-web && npx tsc -b && npx vite build`) doit passer, 0 erreur, avant
  d'écrire un VERIFY.
- Committe en un seul commit pour cette TASK, message clair, jamais `--no-verify`, **ne jamais pousser
  (`git push`)** — le commit reste local pour revue. Ne stage que les fichiers de ton périmètre (voir
  note parallélisation ci-dessus).
- Respecte les règles universelles de `ARCHITECTURE.md` §5 (pas de dette technique silencieuse —
  documente tout compromis dans le VERIFY).
- Si un point est réellement ambigu (ex. rendu visuel exact du toggle, comportement si l'utilisateur
  change de domaine avec une ligne en cours d'édition) : **arrête-toi sur ce point précis,
  documente-le clairement dans le VERIFY** plutôt que d'inventer une réponse, et continue le reste de
  la TASK.

## À la fin

Laisse TASK-183 dans `VERIFY/` (ne la déplace pas toi-même vers `DONE_DETAIL/` — c'est le rôle de
l'architecte de la reviewer et de faire la clôture). Indique dans un résumé final l'état exact
d'avancement.
