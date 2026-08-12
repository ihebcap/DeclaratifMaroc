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

Renommage des 2 champs lus par les 4 `valueGetter` concernés (`montantPart` → `montantLigne`,
`depassementJours` → `depassement`), dans les 2 fichiers. Aucun autre fichier ne référençait ces 2
noms de champs erronés (`grep` de contrôle après correctif, 0 occurrence résiduelle).

## Fichiers livrés

- `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx`
- `declaration-tva-web/src/DeclarationsDelaiPaiementPanel.tsx`

## Vérification

- `npx tsc -b` (déclaration-tva-web) : 0 erreur.
- `grep -r "montantPart|depassementJours" declaration-tva-web/src` : aucune occurrence résiduelle.
- Confirmation PO : correctif rebuild + redéployé chez le client concerné.

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