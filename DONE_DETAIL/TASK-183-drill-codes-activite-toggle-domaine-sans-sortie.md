# TASK-183 — Drill « Codes activité » (écran ③) : permettre de basculer Achats/Ventes sans sortir du drill

## Contexte
Signalement PO (24/07/2026, capture écran ③ Vérifier & Intégrer, drill « Codes activité : TVA
Déductible (Achats) ») : une fois dans ce drill, impossible de voir/affecter les codes activité du
domaine Encaissement (Ventes) — seul « Achats » est accessible dans la grille.

## Constat (lecture code, architecte) — ce n'est PAS un défaut de mapping ou de sauvegarde
Vérifié en base réelle avant toute conclusion :
- Le référentiel `P_DECTVAACTIVITE` a bien des codes dans les deux domaines (57 Encaissement / 39
  Decaissement, `DTA_Domaine` 1/2) — confirmé par les libellés réels (domaine 1 : « opérations de vente
  et de livraison des œuvres d'art », domaine 2 : « Autres achats » — orientation Collecté/Déductible
  confirmée, pas un mapping inversé).
- Le mécanisme d'affectation (`ModifierCodeActiviteLigneAsync`, `GET /api/codes-activite?domaine=...`)
  est strictement symétrique entre les deux domaines, sans aucune restriction côté back.
- `DM_LGTVA` contient bien des lignes des deux domaines (1255 Decaissement / 3893 Encaissement) — les
  lignes Encaissement existent et sont adressables.

**Cause réelle — pure ergonomie front** : `VerifierIntegrerPanel.tsx`, le bandeau du drill (lignes
636-666, `if (drillFiltre) { ... }`) n'affiche que le bouton « ← Retour au contrôle » et un libellé
figé (`drillFiltre.label`), sans aucun contrôle pour changer `drillFiltre.domaine` pendant que le drill
est ouvert. Les deux onglets « TVA Déductible (Achats) » / « TVA Collectée (Ventes) » qui pilotent
`selectedTab` (et donc quel domaine est passé au drill à l'ouverture, lignes 756-797) sont rendus dans
la vue **principale**, masqués dès qu'un `drillFiltre` est actif. Pour changer de domaine, l'utilisateur
doit donc : cliquer « ← Retour au contrôle » → cliquer l'autre onglet en haut → recliquer « Codes
activité » — un aller-retour peu visible, qui explique le signalement PO (« on ne trouve que les
achats »). Le rechargement du référentiel (`useEffect` sur `drillFiltre.kind`/`drillFiltre.domaine`,
`VerifierIntegrerPanel.tsx:327-341`) et le rechargement de la grille (`DomainGrid`, prop `domaine`) sont
**déjà réactifs à un changement de `drillFiltre.domaine`** — aucune modification de la logique de
chargement n'est nécessaire, seul l'accès UI manque.

## Objectif
Dans le bandeau du drill (`VerifierIntegrerPanel.tsx:636-666`), **uniquement quand `drillFiltre.kind
=== 'codeActivite'`** : ajouter un toggle (2 boutons ou mini-onglets) « TVA Déductible (Achats) » /
« TVA Collectée (Ventes) » à côté du libellé actuel, qui appelle :
```ts
setDrillFiltre({ domaine: 'Decaissement' /* ou 'Encaissement' */, filtre: {}, label: '...', kind: 'codeActivite' })
```
— même forme d'objet que celle déjà construite par les boutons « Codes activité » de la vue principale
(lignes 978/997), pas de nouvelle logique de fetch à écrire (les `useEffect` existants réagissent déjà
au changement de `domaine`). Le bouton correspondant au domaine actif (`drillFiltre.domaine`) doit être
visuellement marqué actif, même traitement visuel que les onglets principaux (lignes 766-796).

## Périmètre STRICT
- **Inclus** : `declaration-tva-web/src/VerifierIntegrerPanel.tsx`, uniquement le bandeau du drill
  (lignes ~636-666) pour `kind === 'codeActivite'`.
- **Exclus** : tout changement back (aucun nécessaire, mécanisme déjà symétrique) ; les autres kinds de
  drill (`incoherence`, `toutes`, `anomalie`) — pas concernés par cette demande, ne pas leur ajouter de
  toggle sans demande explicite (périmètre limité au cas signalé) ; la vue principale (onglets
  Décaissement/Encaissement déjà fonctionnels, ne pas toucher).

## Étapes
1. Ajouter le toggle dans le bandeau, conditionné à `drillFiltre.kind === 'codeActivite'`.
2. Vérifier que changer de domaine dans le toggle recharge bien le référentiel (`codeActiviteOptions`)
   ET la grille (`DomainGrid`) sur le nouveau domaine, sans perte de la sélection/scroll en cours
   (comportement attendu : la grille se recharge, comme un changement d'onglet classique).
3. Test manuel réel (ou Playwright) : ouvrir le drill depuis Achats, basculer vers Ventes via le
   nouveau toggle, affecter un code activité à une ligne Ventes, revenir à Achats sans repasser par
   « Retour au contrôle » — confirmer que la ligne Ventes a bien été mise à jour (vérifiable en base,
   `DM_LGTVA.CodeActivite`).

## Livrables
- `VerifierIntegrerPanel.tsx` modifié (bandeau du drill).
- `VERIFY/TASK-183_verify.md` avec capture ou trace du toggle fonctionnel dans les deux sens, preuve
  réelle d'une affectation Encaissement réussie via ce chemin (base `GR_EMA_DISTRIBUTION`).

## Critères de validation
- Depuis le drill « Codes activité » ouvert sur Achats, un clic sur « TVA Collectée (Ventes) » affiche
  la grille Encaissement et le référentiel de codes Encaissement, sans repasser par l'écran principal.
- Une affectation de code activité réalisée ainsi sur une ligne Encaissement est bien persistée
  (`DM_LGTVA.CodeActivite` non vide pour cette ligne après rafraîchissement).
- Aucune régression sur les 3 autres kinds de drill (incoherence/toutes/anomalie) — bandeau inchangé
  pour eux.
- Build front (`tsc -b` + `vite build`) 0 erreur.

## Risques / dépendances
- Risque faible : ajout d'UI pur, réutilise un mécanisme de rechargement déjà exercé (changement
  d'onglet côté vue principale fonctionne déjà). Aucune dépendance avec TASK-178 (consolidation des
  boutons d'action, en attente d'arbitrage PO plus large) — ce correctif est ciblé et n'anticipe pas
  cette consolidation plus large.
