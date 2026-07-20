# TASK-151 — IF manquant tiers ZF FOOD (bloque export XML TVA1-2026-06)

**Date d'exécution :** 2026-07-20 (nuit, worker autonome).
**Méthode :** lecture seule stricte, requête directe sur la fiche tiers Sage réelle
(`F_COMPTET`, base `NEW_EMA DISTRIBUTION`) + lecture de code (mapping colonne IF/ICE et alerte
`TIERS_SANS_IF`).

## Cause racine identifiée

1. **Mapping colonne confirmé sain** : `P_SOCIETE.SO_DecTvaColNameIdentifiantFrs = 'IF'` pour
   `SO_Id=1` — la colonne Sage lue pour l'identifiant fiscal fournisseur est bien `F_COMPTET.[IF]`
   (résolution dynamique, `Declaration.Selection/IdentiteFiscaleFournisseurConfig.cs`). D'autres
   tiers (`C0001 STARICE CAFE`, `C0002 EMES FACTORY`, etc.) portent bien une valeur dans cette
   colonne (ex. `000000`) — **le mapping fonctionne correctement**, ce n'est pas un bug de lecture
   applicative.
2. **Fiche tiers ZF FOOD interrogée directement dans Sage** :

   ```
   CT_Num | CT_Intitule | IF | ICE
   C0174  | ZF FOOD     |    | 003809780000075
   ```

   La colonne `[IF]` est une **chaîne vide réelle** (`LEN([IF])=0`, pas `NULL`) sur la fiche tiers
   Sage — confirmé par requête directe sur `F_COMPTET`. L'ICE, lui, est bien renseigné et cohérent
   avec ce que l'application affiche (`003809780000075`).
3. **Alerte applicative confirmée correcte** : `ConstructeurDeclaration.cs:239` lève
   `TIERS_SANS_IF` (bloquant) dès que l'IF résolu est vide — comportement attendu, pas une
   sur-détection.

## Verdict : (a) donnée Sage manquante — PAS un bug applicatif

L'Identifiant Fiscal du tiers ZF FOOD est **réellement absent de sa fiche Sage**. Ce n'est ni un
défaut de mapping, ni un défaut de lecture côté GRF — la correction doit se faire **exclusivement
côté Sage** (compléter le champ `IF` de la fiche tiers `C0174 — ZF FOOD`), jamais par un contournement
applicatif (valeur codée en dur, placeholder, etc. — explicitement interdit par le garde-fou de
cette TASK, l'IF étant une donnée légale).

## Action requise (hors périmètre code)

Transmettre au PO/service comptable client : compléter l'Identifiant Fiscal du fournisseur
**ZF FOOD** (`CT_Num=C0174`, ICE `003809780000075`) dans la fiche tiers Sage, avant toute tentative
d'export XML de `TVA1-2026-06` (3 lignes concernées : `EC_Id 24048` ×2, `24049`).

## Aucun code modifié par cette TASK

Conforme au garde-fou : lecture seule stricte, aucune valeur d'IF insérée où que ce soit (ni code,
ni base).
