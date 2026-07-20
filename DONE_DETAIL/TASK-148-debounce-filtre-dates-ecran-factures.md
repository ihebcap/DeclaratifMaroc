# TASK-148 — Debounce du filtre de dates sur l'écran Factures (requêtes prématurées / 400 pendant la saisie)

Status: 🆕 à faire
Priority: MEDIUM
Risk: LOW (front-only, pas de changement de contrat API)
Module: declaration-tva-web

> **Origine :** constat PO en session (20/07/2026) : « lenteur sur la liste des factures
> inexpliquable » + erreur console `GET /api/factures?debut=2026-10-29&fin=2026-07-20&soId=1... 400
> (Bad Request)` alors que les deux champs de date affichés à l'écran montraient correctement
> `29/10/2025`. Le PO note aussi : « parfois le filtre de date ne fonctionne pas ».

## Constat (preuve de code)

- [declaration-tva-web/src/FactureInterrogation.tsx:127-128](../declaration-tva-web/src/FactureInterrogation.tsx:127) :
  `debut`/`fin` sont des `useState` mis à jour indépendamment à chaque `onChange` des deux champs
  `<input type="date">` (l.339, l.341). Le `useEffect` de chargement (l.153, l.169) dépend
  directement de `[debut, fin, societeId]` — **sans debounce** : chaque frappe/segment modifié dans
  un champ date déclenche immédiatement un appel API, y compris pendant un état intermédiaire où
  `debut` a été mis à jour mais `fin` ne l'est pas encore (ou inversement), ou pendant qu'un
  segment (jour/mois/année) du input natif n'est pas encore finalisé par l'utilisateur.
- `debut=2026-10-29&fin=2026-07-20` correspond à un état transitoire plausible : `fin` encore à sa
  valeur par défaut `todayIso()` (l.128) pendant que l'utilisateur vient de commencer à modifier
  `debut`, avant d'avoir renseigné `fin`.

## Objectif

Ajouter un debounce (ex. 300-500ms) sur le déclenchement du fetch consécutif à un changement de
`debut`/`fin`, pour que l'appel API ne parte qu'une fois l'utilisateur a fini de modifier les deux
champs (ou une pause dans la saisie), évitant les requêtes intermédiaires invalides et le bruit
console qui en résulte. Optionnellement, valider que `debut <= fin` avant tout appel (défense
supplémentaire, indépendante du debounce).

## Garde-fous

- Ne pas changer le contrat de l'endpoint `/api/factures` ni sa validation back (le 400 est un
  comportement correct du back face à une plage invalide — le problème est uniquement l'émission
  prématurée côté front).
- Ne pas introduire de dépendance externe (lodash.debounce, etc.) si un debounce simple
  `setTimeout`/`useEffect` suffit — cohérent avec le reste du projet (pas de nouvelle dépendance
  non justifiée).

## Files

- `declaration-tva-web/src/FactureInterrogation.tsx` (l.122-239).

## Validation

- [ ] Build front OK.
- [ ] Rejeu réel : modifier rapidement les deux champs de date (Du/Au) vers une plage passée valide
      — plus aucune requête 400 dans la console pendant la saisie, un seul appel final correct.
- [ ] Non-régression : le chargement initial (montage du composant) et le tri/pagination continuent
      de fonctionner sans latence perceptible ajoutée.

## Dépendances / risques

- Aucune dépendance sur les autres TASKs de cette session.
