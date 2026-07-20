# VERIFY — TASK-139 — Badge « Écart détecté » global vs détail « ligne incohérente » filtré par onglet

## Résumé

Défaut de **restitution pur** (front) : le badge « Écart détecté : X MAD » est calculé toutes lignes
confondues (back, global), mais le détail « ligne incohérente » affiché sous le badge est filtré par
l'onglet actif (`ligneIncoherente = recapIncoherence.find(r => r.domaine === selectedTab)`). Quand la
ligne incohérente responsable de l'écart est dans l'**autre** onglet, cette recherche ne trouve rien
→ ancien message trompeur « Écart détecté mais aucune ligne incohérente identifiée ».

**Option retenue = 1 (recommandation architecte)** : strictement front, aucune donnée back changée.
`recapIncoherence` contient déjà tous les domaines. On repère la ligne incohérente **hors onglet
actif** et, si l'écart est expliqué globalement (`ecartExplique !== false`), on renvoie explicitement
l'utilisateur vers le bon onglet au lieu du faux « aucune ligne incohérente ».

Nouveau message :
> « Écart expliqué : la ou les N ligne(s) incohérente(s) (TTC ≠ HT+TVA) se trouvent dans l'onglet
> « TVA Collectée (Ventes) ». Basculez sur cet onglet pour en voir le détail. »

## Modifications (front uniquement)

- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` :
  - Nouveau `ligneIncoherenteAutreOnglet` (useMemo) : `recapIncoherence.find(r => r.domaine !== selectedTab && r.incoherente)`.
  - Helper `domaineOngletLabel(domaine)` : libellé lisible identique aux boutons d'onglet
    (`Encaissement` → « TVA Collectée (Ventes) », sinon « TVA Déductible (Achats) »).
  - Prop `ligneIncoherenteAutreOnglet` passée à `ChecklistCard` + type associé.
  - Branche de repli du bloc `equilibre`/error : nouvelle condition
    `(ligneIncoherenteAutreOnglet && ecartExplique !== false)` intercalée **avant** l'ancien message,
    qui reste inchangé pour tous les autres cas.
- Aucun changement back. `ecartExplique` et `recapIncoherence` (calcul global,
  `DeclarationsController.cs`) non touchés — lecture seule, contrat JSON identique.

## Respect des contraintes de la task

- [x] Écart ni recalculé ni modifié (défaut d'affichage uniquement).
- [x] « Aucune ligne silencieuse » (TASK-112) : l'écart expliqué mais hors onglet est désormais dit
      explicitement (localisation réelle), pas masqué.
- [x] Cas « ligne incohérente dans l'onglet affiché » (TASK-112) inchangé.
- [x] `ecartExplique` inchangé (calcul global back).

## Build / Tests

- `npm run build` (`tsc -b && vite build`) : **OK** (seul l'avertissement préexistant
  `INEFFECTIVE_DYNAMIC_IMPORT` sur `api.ts`, sans rapport).
- `npm run lint` (oxlint) : aucun nouvel avertissement sur `VerifierIntegrerPanel.tsx`.
- `npx playwright test task139` : **4 passed**.

## Preuve visuelle réelle (VRAI composant `VerifierIntegrerPanel`, Chromium/Playwright)

Rendu du vrai panneau ③ Vérifier & Intégrer, `/lignes` et `/checkup` mockés au niveau réseau (le
défaut est purement front : aucun backend .NET ni base prod nécessaire). Déclaration `TVA1-2026-02`,
écart -49 167,99 MAD — cas exact du signalement PO.

| # | Scénario | Capture | Vérifié |
|---|----------|---------|---------|
| A | Écart, ligne incohérente côté **Encaissement**, onglet **Décaissement** ouvert (le fix) | `task139-apres-A-autre-onglet-renvoi.png` | Nouveau message « Écart expliqué … onglet « TVA Collectée (Ventes) » » **visible** ; ancien message absent |
| B | Même écart, **bascule** sur l'onglet TVA Collectée | `task139-apres-B-onglet-collectee-detail.png` | Tableau détail (colonne « Écart ») visible ; ni nouveau message ni ancien message |
| C | Non-régression TASK-112 : incohérence dans l'onglet **affiché** (Décaissement) | `task139-apres-C-meme-onglet-non-regression.png` | Tableau `RecapSourceTable` (drill) inchangé ; aucun message de repli |
| D | Non-régression : écart **inexpliqué** par aucune ligne (`ecartExplique=false`, aucune incohérence) | `task139-apres-D-non-explique-conserve.png` | Message « aucune ligne incohérente identifiée » **conservé** ; pas de bascule à tort vers « autre onglet » |

Assertions mesurées (pas une lecture de code) dans `tests/task139.spec.ts` : présence/absence des
textes `/Écart expliqué\s*:/`, `/onglet «\s*TVA Collectée \(Ventes\)\s*»/`,
`/aucune ligne incohérente identifiée/i` selon le scénario, via `getByText`.

## Reproduction du cas PO `TVA1-2026-02`

Le scénario A reproduit exactement le signalement : écart -49 167,99 MAD affiché onglet Décaissement,
ligne incohérente côté Collectée (Encaissement). Le nouveau message est correct et non trompeur
(capture A). La reproduction en environnement backend réel (checkup calculé sur la vraie base) reste
recommandée au PO avant clôture, mais le défaut étant strictement front (contrat `recapIncoherence`
inchangé), la preuve navigateur sur le vrai composant est représentative.

## Fichiers livrés

- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` (fix).
- `declaration-tva-web/tests/task139.spec.ts` (preuve, 4 scénarios).
- `declaration-tva-web/task139.html` + `declaration-tva-web/src/task139-harness.tsx` : harnais de
  rendu isolé (dev/test uniquement, **non embarqué** dans le build de prod — vérifié : absent de `dist/`).

## Décision d'arbitrage (NOTES de la task)

Les deux options laissées « à trancher » : **option 1** appliquée (front seul, renvoi explicite au
domaine concerné dans le message). Le raccourci « changer d'onglet directement » (option 2) n'a pas
été implémenté — le message indique clairement l'onglet à ouvrir, périmètre minimal. À proposer au PO
comme amélioration UX ultérieure si souhaité.
