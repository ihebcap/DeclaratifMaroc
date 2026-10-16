# TASK-235 — V2 compta : analyse préparatoire des données (LOTREX, NEW_EMA DISTRIBUTION, GOCOM) — lecture seule, aucun code applicatif

Status: 🆕 à faire (worker : Gemini)
Priority: HIGH (alimente TASK-226, 227, 228, 229, 234)
Risk: LOW (lecture seule) — mais **une erreur d'analyse fausserait le design**
Module: analyse / documentation (`DOCS/`)

> **Origine :** PO 16/10/2026 — « lancer une task par Gemini : il analyse et fait les analyses nécessaires ». Complète le spike de l'architecte (`DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md` §2-§3), qui n'a fait qu'un premier passage.

## Décisions de conception

1. **Lecture seule stricte** : uniquement `SELECT` (jamais `INSERT/UPDATE/DELETE/MERGE/EXEC` de procédure d'écriture, aucun `ALTER`, aucune table temporaire persistante). Compte SQL en lecture seule si disponible. Instance `.\sql2022`, bases `LOTREX`, `NEW_EMA DISTRIBUTION` (espace dans le nom → `[NEW_EMA DISTRIBUTION]`), `GOCOM`.
2. **Aucun code dans le dépôt** : le seul livrable est un rapport Markdown + les scripts SQL reproductibles. Pas de modification de `Declaration.*`, `declaration-tva-web` ni `Declaration.Core`.
3. **Chaque chiffre du rapport est accompagné de la requête exacte** qui l'a produit (reproductible par l'architecte). Un chiffre sans requête = rejeté.
4. **Ne jamais extrapoler** : si une base ne permet pas une analyse, le dire explicitement (ex. GOCOM).
5. Dépôt d'un `VERIFY/TASK-235_verify.md` comme point d'arrêt ; **aucune clôture** par le worker (règle de séparation implémentation/clôture de `D:\_vibe\CLAUDE.md`).

## Périmètre STRICT — analyses demandées (pour chaque base)

**Pour TASK-226 (factures)**
1. Collisions de clé de pièce : nombre de groupes (`JO_Num`, `JM_Date`, `EC_Piece`) contenant plusieurs lignes tiers distinctes ou des numéros de pièce répétés pour des opérations différentes.
2. Décomposition d'une pièce d'achat / de vente : pièces dont Σ débits ≠ Σ crédits ; pièces sans ligne de TVA ; sans ligne de charge ; TVA sur un compte ≠ `F_TAXE.CG_Num` du `TA_Code` ; `TA_Code` absent de `F_TAXE` ; `TA_Type ≠ 0` (liste des cas avec exemples).
3. Comptes de TVA partagés par plusieurs `TA_Code` dans `F_TAXE` et impact (pièces où deux codes partageant un compte coexistent).
4. Multi-taux : nombre de pièces et exemples où deux taux coexistent ; pièces où un même compte de charge porte plusieurs `TA_Code`.
5. Tiers : liste des comptes collectifs et `CT_Type` réels utilisés ; préfixes de numéros de tiers ; tiers sans IF/ICE dans `F_COMPTET` (colonnes candidates à confirmer avec le PO).

**Pour TASK-227 / 234 (règlements, rapprochement)**
6. Lettrages : répartition 1 facture ↔ 1 règlement, 1 ↔ n, n ↔ 1, n ↔ m ; avec exemples n ↔ m (pour arbitrage PO sur l'imputation) ; lettrages déséquilibrés.
7. Règlements lettrés par type de journal : rapprochés / non rapprochés, avec montants ; règlements sur un journal ≠ banque.
8. Configuration des journaux de trésorerie : `JO_Type`, `JO_Rappro`, `CG_Num` ; cas A1 à A5 de TASK-234 avec volumes.
9. Écart entre date d'opération (`JM_Date + EC_Jour − 1`) et `EC_DateRappro` : distribution (combien de rapprochements antérieurs/postérieurs, ordre de grandeur).
10. Avoirs : volumes (achats et ventes), pour dimensionner la future TASK avoirs.

**Pour TASK-228 / 229 (contrôles)**
11. Écarts HT × taux ≠ TVA arrondie : volume, distribution des écarts, 20 plus grands exemples.
12. Pièces touchant un compte de taxe **sans aucune ligne tiers** : volumes par journal et par type (dépense, frais bancaires, OD), exemples ; pièces non décomposables.

**GOCOM**
13. Base de nature commerciale (`F_COMPTET`, `F_DOCENTETE`, `F_DOCLIGNE`) : confirmer qu'elle ne contient pas de comptabilité ; chercher sur l'instance la base comptable correspondante (noms proches, même client) ; proposer le nom à confirmer par le PO. Si introuvable, le dire.

- **Exclu** : écriture de données, correctifs, estimations sans requête, toute évolution du code.

## Livrables

- `DOCS/ANALYSE_V2_DONNEES_2026-10-16.md` : une section par analyse (1 à 13), tableaux de chiffres par base, requêtes SQL jointes, constats, **risques pour le design**, questions à poser au PO.
- `DOCS/analyse_v2/*.sql` : les requêtes, une par analyse, rejouables.

## Critères de validation

- Les 13 analyses sont présentes, ou explicitement marquées « impossible » avec la raison.
- L'architecte rejoue au minimum 5 requêtes au hasard : mêmes chiffres.
- Preuve de lecture seule : liste des instructions exécutées (aucune d'écriture) dans le VERIFY.
- Aucun fichier du code applicatif modifié (`git status` joint).

## Files

- `DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md`, `TASKS/TASK-226` à `TASK-229`, `TASKS/TASK-234`.
- `DOCS/PROMPT-WORKER-TASK-235-16-10-2026.md` (prompt prêt à coller).
