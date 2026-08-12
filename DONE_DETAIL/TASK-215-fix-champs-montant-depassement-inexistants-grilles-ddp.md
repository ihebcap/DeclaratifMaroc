# TASK-215 — DDP : colonnes « Montant » et « Dépassement (j) » toujours à 0 (champs JSON inexistants)

## Contexte

Signalement PO 12/08/2026 (capture d'écran chez un client) : écran ① Sélection des lignes hors délai
(DDP), toutes les lignes affichent `0,00 MAD` en colonne Montant, alors que le total d'en-tête
(« Total de la déclaration ») affiche bien 1 745 645,15 MAD — incohérence entre total et détail.

## Diagnostic (lecture de code, avant tout correctif)

Deux colonnes AG Grid, dans deux écrans distincts, lisaient un champ qui n'existe nulle part dans le
contrat API :

- `ControleLignesDelaiPaiementPanel.tsx:150` : `valueGetter: (p) => formatMoney(p.data.montantPart)`
- `DeclarationsDelaiPaiementPanel.tsx:629` : `valueGetter: (p) => formatMoney(p.data.montantPart)`
- `ControleLignesDelaiPaiementPanel.tsx:149` : `valueGetter: (p) => p.data?.depassementJours ?? 0`
- `DeclarationsDelaiPaiementPanel.tsx:630` : `valueGetter: (p) => p.data?.depassementJours ?? 0`

Le DTO backend (`Declaration.API/Dtos/DeclarationDelaiPaiementDto.cs`) expose `MontantLigne` et
`Depassement` (confirmé dans le type TS `api.ts:312`/`:313`) — jamais `montantPart`/`depassementJours`.
`p.data.montantPart` est donc toujours `undefined`, masqué par le fallback `amount || 0` de
`formatMoney` (`utils.tsx:25`) qui affiche silencieusement `0,00 MAD` au lieu de planter — même
mécanisme pour `depassementJours` avec le `?? 0` du `valueGetter`. Preuve que le bon champ existe
ailleurs dans le même fichier : `DeclarationsDelaiPaiementPanel.tsx:1196` utilise correctement
`l.montantLigne`.

Le total d'en-tête est calculé côté serveur à partir de la vraie donnée, donc indépendant du bug —
d'où l'incohérence visible total ≠ somme des lignes affichées.

## Correctif appliqué

Renommage des 4 `valueGetter` concernés, avec une nuance découverte après un premier essai
insuffisant : les deux écrans ne consomment pas le même DTO.

- `ControleLignesDelaiPaiementPanel.tsx` (modal de sélection) lie `LigneSelectionDdpDto`, qui expose
  bien `montantLigne`/`depassement` → `montantPart` → `montantLigne`, `depassementJours` → `depassement`.
- `DeclarationsDelaiPaiementPanel.tsx` (grille des lignes déjà intégrées, celle de la capture d'écran
  du PO) lie **`LigneIntegreeDdpDto`, qui n'a pas de champ `montantLigne` du tout** (`api.ts:344-365` :
  `montantAffecte`/`soldeEcheance`/`depassement`, jamais `montantLigne`). Un premier correctif y avait
  remplacé `montantPart` par `montantLigne` par erreur (copié-collé de l'autre écran sans revérifier le
  DTO réellement lié) — toujours `undefined`, donc toujours 0,00 MAD affiché en pratique. Corrigé en
  reprenant exactement la même formule que le total d'en-tête déjà correct (ligne 688) :
  `p.data.montantAffecte ?? p.data.soldeEcheance`. `depassementJours` → `depassement` était en revanche
  correct du premier coup (`LigneIntegreeDdpDto.depassement` existe bien).

Aucun autre fichier ne référençait les 2 noms de champs erronés d'origine (`grep` de contrôle après
correctif, 0 occurrence résiduelle).

## Fichiers livrés

- `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx`
- `declaration-tva-web/src/DeclarationsDelaiPaiementPanel.tsx`

## Vérification

- `npx tsc -b` (déclaration-tva-web) : 0 erreur.
- `grep -r "montantPart|depassementJours" declaration-tva-web/src` : aucune occurrence résiduelle.
- Confirmation PO : correctif rebuild + redéployé chez le client concerné.

## Amélioration complémentaire (même session, demande PO après validation visuelle)

La colonne « Action » (bouton « Retirer cette ligne ») restait affichée, vide, sur une déclaration
Clôturée (le `cellRenderer` renvoyait déjà `null` par ligne, donc aucun bouton n'apparaissait — mais
la colonne elle-même occupait toujours une place dans la grille). Demande PO : la masquer entièrement
dans ce cas plutôt que de laisser une colonne systématiquement vide. Corrigé en excluant l'entrée de
colonne du tableau `columnDefsLignes` (au lieu de la garder avec un `cellRenderer` qui renvoie `null`)
quand `!declaration.actions.peutIntegrerLignes`.

## Note process

Corrigé directement par Claude à la demande explicite du PO (dérogation ponctuelle au workflow
architecte/Gemini de `CLAUDE.md`, correctif de 4 lignes à risque nul — renommage de champ pur, aucune
logique métier touchée). Pas de revue par un 2ᵉ agent indépendant, cohérent avec le même type de
dérogation déjà actée pour TASK-031/032.

## Investigation complémentaire (même session, sans lien avec le correctif)

Le PO a par ailleurs demandé de vérifier si des déclarations DDP de l'ancien logiciel existaient dans
une base séparée (risque documenté dans TASK-131 : historique invisible si base différente). Vérifié
sur la base réelle du client : une seule base sur le serveur (confirmé par le PO, client connu), table
`RT_DECLARATIONDELAISPAIEMENT` ne contient que 3 déclarations, toutes créées par GRF
(`DDP_DateCreation` du 2026-03-18 au 2026-07-20) — aucune déclaration antérieure de l'ancien logiciel.
Le calcul incrémental (TASK-131) démarre donc légitimement de zéro historique pour ce client, sans
risque de double-comptage. Aucune action requise.