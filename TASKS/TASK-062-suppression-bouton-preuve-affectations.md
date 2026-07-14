# TASK-062 — Supprimer le bouton « Preuve » de l'écran ② Affectations (modale redondante)

## Contexte
Sur l'écran ② Affectations (`AffectationsDrill.tsx`), chaque section règlement porte un bouton **« Preuve »** (`FileSearch`) qui ouvre une modale latérale `ProofModal` à 3 onglets (Rapprochement / Affectation / Conformité IF/ICE), remplie par un `DomainGrid` en lecture seule filtré sur le n° de rapprochement.

**Décision PO (13/07/2026)** : supprimer ce bouton. Deux raisons constatées à l'écran :
1. **Redondance** — la carte inline de la facture (`FactureCard`) affiche déjà la preuve complète et lisible de la valorisation : `Payé … ÷ TTC … = prorata%`, puis par taux `TVA brute × prorata% = TVA déclarée`, IF/ICE, badge conformité, origine. La modale n'apporte rien de plus.
2. **Non significatif / trompeur** — la modale relance le `DomainGrid` générique des interrogations ; en pratique elle affiche un dump non pertinent (ex. 776 lignes `Encaissement / Proposée` répétées) et un titre « Règlement N° {numeroRapprochement} » qui confond n° de règlement et n° de rapprochement.

La preuve fine « descendre aux lignes comptables sources (OM Sage / détail RT_HISTOCOMPTA FGR) » n'est **pas** l'objet de cette tâche (option écartée par le PO) — on retire, on ne remplace pas.

> **⚠️ Recadrage architecte (13/07/2026)** : `ProofModal.tsx` **n'est PAS orphelin** — il est aussi importé et rendu par `WorkstationPanel.tsx` (import l.5, usage `<ProofModal … />` l.228). **Sa suppression casserait le build.** Le périmètre est donc réduit au **retrait du bouton « Preuve » et de son câblage sur l'écran ② `AffectationsDrill.tsx` uniquement** ; le composant `ProofModal.tsx` **est conservé** (partagé). Le reste de l'objectif (redondance + modale non significative sur l'écran ②) est inchangé.

## Périmètre STRICT
- **Inclus** : retrait du bouton « Preuve » et de tout le câblage de `ProofModal` **dans `AffectationsDrill.tsx` uniquement** (écran ② Affectations).
- **Exclu** :
  - Ne **pas** supprimer `declaration-tva-web/src/ProofModal.tsx` — il reste utilisé par `WorkstationPanel.tsx`. Le fichier est conservé tel quel.
  - Ne **pas** toucher `WorkstationPanel.tsx` ni son usage de `ProofModal`.
  - Ne **pas** toucher la carte inline `FactureCard` / `ReglementSection` ni aucun calcul d'affichage (inversions `inverse()`, prorata, totaux) — la preuve reste là.
  - Ne **pas** modifier `DomainGrid` : la prop `readonly` reste utilisée par `ControleDeclarationPanel.tsx` et `WorkstationPanel.tsx`.
  - Aucun changement back / API / SQL. Front-only.

## Cause racine
Le bouton a été ajouté comme surcouche de traçabilité, mais la carte inline couvre déjà 100 % du besoin de preuve à ce niveau ; la modale duplique l'information en moins lisible et réutilise une grille non scopée à l'histoire du règlement.

## Objectif
```
Entrée : écran ② Affectations, une section par règlement sélectionné
Traitement : plus de bouton « Preuve », plus de modale ProofModal
Sortie : la preuve reste entièrement portée par la carte inline (payé÷TTC=%, taux×%=TVA, IF/ICE, conformité, origine) ; build front vert, aucun import mort
```

## Étapes
1. `AffectationsDrill.tsx` :
   - Retirer l'import `ProofModal` (ligne 5) et `FileSearch` de l'import `lucide-react` (ligne 2, plus utilisé après retrait du bouton).
   - Retirer l'état `proofNumero` (`useState`, ligne 133) et le bloc de rendu conditionnel `{proofNumero && <ProofModal … />}` (lignes 199-201).
   - Retirer la prop `onProof` : signature de `ReglementSection` (ligne 206), passage `onProof={() => setProofNumero(row.numeroReglement)}` au call (ligne 194), et le `<button … Preuve>` (lignes 218-220).
   - Ajuster le conteneur d'en-tête de section (`ReglementSection`, ligne 212) si le `justify-content: space-between` laisse un vide gênant une fois le bouton parti (le bloc gauche `Règlement / tiers / montant` suffit).
2. **NE PAS** supprimer `declaration-tva-web/src/ProofModal.tsx` — le composant reste utilisé par `WorkstationPanel.tsx` (import l.5, usage l.228). Il est conservé.
3. Vérifier qu'aucune référence à `ProofModal` ne subsiste **dans `AffectationsDrill.tsx`** (`grep ProofModal declaration-tva-web/src/AffectationsDrill.tsx` = 0) ; les références dans `WorkstationPanel.tsx` restent attendues.
4. Build front (`npm run build` / `tsc`) vert, aucun import/variable inutilisé dans `AffectationsDrill.tsx` (`FileSearch`, `ProofModal`, `proofNumero`).

## Livrables
- `AffectationsDrill.tsx` sans bouton « Preuve » ni câblage `ProofModal`. `ProofModal.tsx` **conservé** (partagé avec `WorkstationPanel.tsx`).
- `VERIFY/TASK-062_verify.md` : capture de l'écran ② Affectations sans bouton « Preuve », la carte inline intacte (preuve toujours visible) ; sortie build front sans erreur ni warning d'import mort ; `grep ProofModal declaration-tva-web/src/AffectationsDrill.tsx` = 0 résultat, `WorkstationPanel.tsx` toujours fonctionnel.

## Critères de validation
- Plus aucun bouton « Preuve » ni modale sur l'écran ② Affectations.
- La carte inline affiche toujours la preuve complète (payé÷TTC=%, TVA par taux, IF/ICE, conformité, origine).
- `ProofModal.tsx` conservé ; `WorkstationPanel.tsx` inchangé et fonctionnel (import + usage l.5/228 intacts).
- `DomainGrid`, `ControleDeclarationPanel` inchangés et fonctionnels (`readonly` conservé).
- Build front vert, aucun import/symbole orphelin **dans `AffectationsDrill.tsx`** (`FileSearch`, `ProofModal`, `proofNumero`).
- Aucun changement back / API / SQL.

## Risques / dépendances
- Risque très faible : retrait front pur sur **un seul fichier** (`AffectationsDrill.tsx`), périmètre isolé.
- **Piège écarté (recadrage 13/07)** : ne pas supprimer `ProofModal.tsx` (partagé `WorkstationPanel.tsx`) — la suppression casserait le build.
- Aucune dépendance bloquante. Si le besoin d'une vraie « preuve » descendant aux lignes sources (OM Sage / RT_HISTOCOMPTA FGR) revenait, ce serait une tâche distincte à part entière (option explicitement écartée ici).
