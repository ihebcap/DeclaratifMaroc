# VERIFY — TASK-148

Date: 2026-07-20
Agent: worker nocturne (Claude Code)

## BUILD

- Status: OK
- Erreurs: aucune.
- `npx tsc -b` → 0 erreur.
- `npx vite build` → build réussi (1 avertissement préexistant `INEFFECTIVE_DYNAMIC_IMPORT`, non
  lié).

## FICHIERS MODIFIÉS

- `declaration-tva-web/src/FactureInterrogation.tsx` :
  - Ajout de `debouncedDebut`/`debouncedFin` (state), synchronisés depuis `debut`/`fin` via un
    `useEffect`/`setTimeout` de 350ms (annulé si `debut`/`fin` change avant l'échéance — pattern
    debounce standard, aucune dépendance externe ajoutée, conforme au garde-fou).
  - Les `<input type="date">` restent branchés sur `debut`/`fin` (réactivité immédiate à l'écran,
    `min`/`max` croisés inchangés) — seuls les effets réseau (`/factures/distincts`, `fetchPage`)
    utilisent désormais `debouncedDebut`/`debouncedFin`.
  - Garde supplémentaire indépendante (`if (debouncedDebut > debouncedFin) return;`) dans les deux
    effets réseau — défense en profondeur si une plage invalide subsiste après le délai.
  - `handleRefreshValorisation` (bouton explicite « Rafraîchir ») **non touché** — reste sur
    `debut`/`fin` bruts : ce n'est pas un fetch automatique déclenché par la frappe, l'utilisateur a
    déjà fini de saisir au moment du clic.

## DIFF RÉSUMÉ

`debut`/`fin` déclenchaient un fetch immédiat à chaque `onChange`, y compris pendant un état
transitoire de saisie (ex. `fin` encore à sa valeur par défaut pendant que l'utilisateur modifie
`debut`), produisant des requêtes `400 Bad Request` intermédiaires observées en session
(`debut=2026-10-29&fin=2026-07-20`). Ajout d'un debounce 350ms avant que les effets réseau ne
répercutent la nouvelle période, + garde défensive `debut <= fin`.

## VALIDATION CHECKLIST

- [x] Build front OK.
- [~] **Rejeu réel non exécuté dans cet environnement** — même limite structurelle que TASK-120/
      TASK-115 (déjà documentée dans `DONE.md`) : pas de chemin d'accès UI réel jusqu'à l'écran
      Factures sans licence `ApLicence` valide dans cet environnement de nuit (pas de session
      navigateur interactive disponible non plus). **Vérifié à la place par lecture de code +
      raisonnement sur le comportement React** : `setTimeout`/`clearTimeout` classique, dépendances
      d'effet correctement mises à jour (`debouncedDebut`/`debouncedFin` remplace `debut`/`fin`
      partout où un fetch réseau est déclenché automatiquement), aucun changement de contrat API.
      **Point à confirmer par le PO/l'architecte en conditions réelles avant clôture.**
- [x] Non-régression : chargement initial au montage — `debouncedDebut`/`debouncedFin` sont
      initialisés directement aux valeurs par défaut (`yearStartIso()`/`todayIso()`, identiques à
      `debut`/`fin` au montage), donc le premier fetch se déclenche immédiatement (pas de délai de
      350ms artificiel au chargement de l'écran — vérifié par lecture : `useState(debut)` capture
      la valeur initiale, pas de dépendance supplémentaire retardant le premier rendu).
- [x] Tri/pagination non affectés — `sortConfig`/`filters`/`page` restent des dépendances directes
      (non debounced) de `fetchPage`, aucun changement à leur gestion.
- [x] Aucun changement de contrat API/back — confirmé par diff (fichier front seul modifié).

## RESTE À VALIDER (honnête, non silencieux)

1. Aucune preuve visuelle/interaction réelle (navigateur) obtenue dans cet environnement — même
   limite déjà documentée pour TASK-115/TASK-120 (licence `ApLicence` non disponible en dev nocturne
   sans intervention humaine). Le correctif est un pattern React standard, à faible risque, mais son
   effet observable (absence de requêtes 400 pendant une saisie rapide) n'a pas été vu à l'écran.

## IMPACTS DÉTECTÉS

- Aucun — modification strictement locale à `FactureInterrogation.tsx`, aucun autre écran
  n'utilise ce composant.
