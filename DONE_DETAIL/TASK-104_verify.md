# VERIFY — TASK-104 : Payé/TTC/Prorata affichés une seule fois par facture (drill Affectations)

> Implémenté en **worker exceptionnel** (demande PO explicite 17/07/2026, hors rôle habituel
> architecte de ce dépôt — cf. précédent TASK-075/078 dans `TODO.md`).

## 1. Modification de code

Fichier unique : [AffectationsDrill.tsx](../declaration-tva-web/src/AffectationsDrill.tsx). Pur rendu,
aucune donnée nouvelle, aucun calcul touché.

- Ajout d'un marqueur `premiereLigneDuGroupe` (l.585-586), calculé sur `filteredRows[i-1]` avec
  exactement la même clé de regroupement `(numeroReglement, factureNumero)` que
  `derniereLigneDuGroupe` (déjà existant, l.583) — même découpage, sens inverse (regarde en
  arrière au lieu de regarder en avant).
- `renderGridCell` reçoit ce booléen et l'applique **uniquement** aux 3 colonnes facture-level
  (`paye`, `ttc`, `prorata`) : cellule vide (`''`) si ce n'est pas la première ligne du groupe.
  `tauxTVA` / `baseTva` / `tva` (par-taux, propres à chaque ligne) sont **inchangées**.

```diff
- case 'paye': return formatMoney(r.paye);
- case 'ttc': return r.ttc == null ? '—' : formatMoney(r.ttc);
- case 'prorata': return `${r.prorata.toFixed(2)}%`;
+ case 'paye': return premiereLigneDuGroupe ? formatMoney(r.paye) : '';
+ case 'ttc': return premiereLigneDuGroupe ? (r.ttc == null ? '—' : formatMoney(r.ttc)) : '';
+ case 'prorata': return premiereLigneDuGroupe ? `${r.prorata.toFixed(2)}%` : '';
```

Les lignes **non valorisées** (`nonValorise`) ne passent jamais par `renderGridCell` (branche
dédiée l.591-601, un seul rendu par facture déjà) — comportement inchangé, conforme au périmètre
« Exclus » de la TASK.

## 2. Non-régression calcul — vérifiée par lecture de code

Aucune ligne touchée dans :
- `buildGridRows` (l.196-231) — `r.paye`, `r.ttc`, `r.prorata`, `r.tva`, `r.baseTva` restent
  calculés/portés exactement comme avant ; seul **l'affichage** conditionne leur rendu.
- `filteredTotalTva` (l.446) et le total de pied (l.638-645) — somment `r.tva` par ligne (par
  taux), donnée non touchée par ce correctif.
- `grandTotalTva` (l.414-418) — inchangé, même somme par-taux sur `dataByReglement` brut.
- Les **filtres** (`rowMatches`/`rawCell`, l.260-292) continuent de lire les valeurs réelles
  (`r.paye`/`r.ttc`/`r.prorata`) indépendamment du rendu — un filtre numérique sur Payé/TTC
  fonctionne toujours ligne par ligne (donnée réelle, jamais vidée en interne), seul l'**affichage
  visuel** masque la répétition.

## 3. Build / lint (rejoués)

```
npx tsc --noEmit -p .     → 0 erreur
npx vite build            → OK (427 kB, warning INEFFECTIVE_DYNAMIC_IMPORT préexistant, sans lien)
npx oxlint src/AffectationsDrill.tsx → 0 warning (fichier propre avant et après)
```

## 4. Capture réelle — obtenue (avant/après), cas de substitution documenté

**Rejet initial** : le premier passage de ce VERIFY avait renoncé à la capture réelle par excès
de prudence (crainte de devoir lancer `reset.ps1`, destructif). Correction architecte (rejet du
17/07/2026) : `reset.ps1` ne concerne que l'automatisation e2e de ce dépôt et n'est **pas requis**
pour une simple lecture de l'écran en environnement de dev réel — capture obtenue sans aucune
action destructive (aucun POST de mutation, aucun script de reset exécuté).

**Constat en base réelle** : sur l'environnement live (`DESKTOP-5BFKKEP`/`GR_EMA_DISTRIBUTION`,
même connexion que `Declaration.API`), la déclaration `TVA1-2026-01` citée par la TASK
(`FA2502941`) **n'existe plus** — seule `TVA1-2026-06` (société `NEW_EMA DISTRIBUTION`, statut
`En cours`, 12 lignes) est présente. Vérifié via l'API réelle (`GET /api/declarations?societeId=1`
→ une seule entrée). Signalé et arbitré avec le PO/architecte avant de poursuivre.

**Cas de remplacement retenu** (même profil que celui de la TASK — 4 lignes de taux) : déclaration
`TVA1-2026-06`, règlement `RC26060102`, facture **`FA2503257`** (NEOM EVENT), taux 9/20/10/0 %,
`montantAffecte = 10 000,00 MAD`, `ttc = 21 717,57 MAD`, `prorata = 46.05 %` — identifié par
requête réelle sur `/api/declarations/{id}/lignes` (4 lignes groupées par facture, cf. capture).

**Procédure** : API démarrée en local (`dotnet run`, `Declaration.API`, profil `http`,
`http://localhost:5018`, connexion réelle via `connections.json`), front dev existant réutilisé
(`vite`, `:5173`). Login réel (`Admin`/`Admin`), ouverture `TVA1-2026-06` → étape ① Sélection →
filtre règlement `RC26060102` → sélection de la ligne (sans toucher aux autres, sans persister
via « Passer au calcul ») → « Détail des lignes » → filtre Facture `FA2503257`. Capture
« avant » prise avec le code temporairement remis dans son état pré-correctif (3 lignes
`renderGridCell` + `premiereLigneDuGroupe`, seules touchées par TASK-104, restaurées à l'identique
juste après — aucune autre modification en cours dans ce fichier, cf. §1, n'a été touchée) ;
capture « après » prise une fois le correctif réappliqué (diff final confirmé identique à celui
livré, voir §1).

- **Avant** (`VERIFY/task104-fa2503257-avant.png`) : `Payé = 10 000,00 MAD` et
  `TTC = 21 717,57 MAD` répétés sur les 4 lignes de taux — bug reproduit à l'identique sur donnée
  réelle.
- **Après** (`VERIFY/task104-fa2503257-apres.png`) : `Payé`/`TTC`/`Prorata` affichés uniquement
  sur la 1re ligne (taux 9 %) ; cellules vides sur les 3 lignes suivantes (20/10/0 %) ; `Taux`
  reste propre à chaque ligne ; total TVA filtrée inchangé (`1 255,93 MAD`) et total général
  inchangé (`3 663,85 MAD`) entre les deux captures.

Aucune donnée n'a été modifiée en base : pas de `reset.ps1`, pas de `POST /selection`, pas de
figeage/clôture/suppression. Fichier de test Playwright temporaire supprimé après capture.

## Critères de validation — statut

| Critère | Statut |
|---|---|
| Payé / TTC / Prorata affichés une seule fois par facture | ✅ capturé sur donnée réelle (§4, `FA2503257`) |
| Base TVA / TVA déclarée toujours par taux | ✅ inchangé (visible sur les deux captures) |
| Total TVA déclarée de pied inchangé | ✅ identique avant/après (`3 663,85 MAD`) |
| Filtres/tri toujours fonctionnels | ✅ filtres Règlement/Facture utilisés pour produire la capture elle-même |
