# TASK-236 — V2 compta : export Excel d'aide à la déclaration (format contractuel versionné)

Status: 🆕 à faire
Priority: HIGH (le fichier est le produit principal de la V2)
Risk: HIGH (format = contrat avec TASK-237 ; une dérive casse le retour)
Module: Declaration.Export.Excel (extension, sans toucher à l'existant) + Declaration.Compta.* + Declaration.API
Dépend de : TASK-226, TASK-227 (lignes), TASK-228, TASK-229, TASK-234 (alertes) ; réutilise le patron de TASK-223

> **Origine :** PO 16/10/2026 — « le projet ne livre pas une déclaration 100 % définitive : on aide au maximum, on exporte un fichier Excel dans un format bien déterminé, le user le complète et le ramène pour la déclaration définitive ».

## Décisions de conception

1. **Le classeur est un contrat** : format **versionné** (`FormatVersion`, ex. `V2.1`), documenté dans `DOCS/FORMAT_EXCEL_AIDE_DECLARATION_V2.md` (colonnes, types, droits, listes). Toute évolution = nouvelle version, l'import (TASK-237) accepte les versions qu'il connaît et refuse les autres avec un message explicite.
2. **Feuilles** :
   - `LisezMoi` : version du format, dossier, période, sens (décaissement/encaissement), date/heure d'export, `ExportId`, empreinte de contrôle, mode d'emploi en 10 lignes (quelles colonnes remplir).
   - `Lignes` : une ligne = (règlement × facture × taux). Colonnes **verrouillées** (source compta, grisées) : clé technique stable, n° pièce facture, date facture, tiers, HT, taux, TVA constatée, TVA attendue, écart, TTC, date d'opération, journal, n° règlement, statut rapprochement. Colonnes **à compléter** (jaunes, avec validation de données Excel) : IF, ICE (préremplis si présents dans `F_COMPTET`), désignation, code activité, **décision** (`Inclure` / `Reporter` / `Exclure`), motif (obligatoire si `Exclure`), **validation d'écart** (`OUI` + commentaire).
   - `Alertes` : toutes les anomalies du moment (non rapprochés, écarts HT × taux, avoirs non traités, comptes de taxe sans tiers, paramétrage trésorerie) avec la gravité (bloquant à la déclaration définitive / avertissement).
   - `SansTiers` : flux TASK-229 (dépenses, frais bancaires) au même format que `Lignes`.
   - `Récap` : totaux par taux, par source, par code activité — **formules Excel vivantes** (le total se recalcule quand l'utilisateur change une décision).
   - `Listes` : valeurs autorisées (décisions, codes activité du référentiel, modes de paiement).
3. **Clé technique par ligne** : `RèglementCbMarq|FactureCbMarq|TA_Code` (jamais `EC_Piece` seul) + colonne de contrôle (somme de contrôle de la ligne source) pour détecter toute altération des colonnes verrouillées.
4. **Montants non modifiables** par l'utilisateur (feuille protégée sauf colonnes jaunes, sans mot de passe fort : c'est un garde-fou d'usage, la vraie protection est le contrôle de TASK-237). Si un montant est faux, on corrige la compta et on ré-exporte. **À confirmer PO.**
5. **Export répétable** : chaque export reçoit un `ExportId` enregistré dans `DeclaratifMaroc` (dossier, période, date, utilisateur, nombre de lignes, empreinte). L'export **ne déclare rien** et ne verrouille rien (l'idempotence `LigneDeclareeCompta` n'est écrite qu'à la déclaration définitive, TASK-230).
6. Vraies dates Excel, montants numériques, largeurs de colonnes définies, en-têtes figés, filtres actifs (comme TASK-223). Séparateurs et formats Maroc/FR.
7. Les lignes déjà **déclarées** n'y figurent pas ; les règlements non déclarés des périodes antérieures y figurent (report, TASK-227).
8. Aucun appel Sage en écriture ; uniquement les données lues par TASK-226/227/229.

## Périmètre STRICT

- **Inclus** : générateur du classeur (sens décaissement et encaissement), document de spécification du format, table `ExportAideDeclaration` (EF), endpoint de téléchargement, bouton d'export dans l'écran, tests (structure, protections, formules, relecture du `.xlsx` produit).
- **Exclu** : relecture/import (TASK-237) ; déclaration définitive (TASK-230) ; XML.

## Livrables

- Générateur + spécification `DOCS/FORMAT_EXCEL_AIDE_DECLARATION_V2.md` + table EF + endpoint + tests.

## Critères de validation

- Classeur ouvert sans erreur dans Excel ; les 6 feuilles présentes ; colonnes verrouillées/jaunes conformes à la spécification (relecture programmatique du `.xlsx` jointe).
- Totaux de `Récap` (formules recalculées) = totaux calculés par le back sur NEW_EMA (décaissement) et LOTREX (décaissement + encaissement), écart 0,00.
- Deux exports successifs sans changement de données : lignes et clés identiques, `ExportId` différents.
- Modifier une cellule verrouillée est empêché par la protection de feuille.
- Build 0 erreur, tests verts ; preuve par critère datée ; **scénarios de test manuel** (utilisateur réel : ouvrir, compléter, enregistrer) dans `DOCS/`.

## Files

- `Declaration.Export.Excel/` (étudier d'abord le code existant et TASK-223), `Declaration.Compta.*`, `Declaration.API/Controllers/`.
- `DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md` §0.
