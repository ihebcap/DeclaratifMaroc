# TASK-237 — V2 compta : import du fichier Excel complété, contrôles et prévisualisation avant déclaration

Status: 🆕 à faire
Priority: HIGH
Risk: CRITICAL (le fichier est modifié par un humain : toute faille ici donne une déclaration fausse)
Module: Declaration.Compta.* + Declaration.Export.Excel (lecture) + Declaration.API + declaration-tva-web
Dépend de : TASK-236 ; front = TASK-231 + TASK-232

> **Origine :** PO 16/10/2026 — « le user complète le fichier et le ramène pour la déclaration définitive ».

## Décisions de conception

1. **Rien n'est écrit tant que l'utilisateur n'a pas confirmé.** L'import se fait en 2 temps : (a) **analyse / prévisualisation** (fichier lu, rapport ligne par ligne, aucune persistance de déclaration) ; (b) **confirmation** explicite qui crée le jeu de lignes validées pour TASK-230.
2. **Contrôles d'intégrité du fichier** (bloquants) : `FormatVersion` connue ; `ExportId` existant dans `DeclaratifMaroc` ; même dossier et même période ; empreinte et sommes de contrôle des colonnes verrouillées **intactes** (toute modification d'un montant, d'une clé ou d'une date source = ligne rejetée avec la raison) ; clés uniques (pas de ligne dupliquée) ; feuille `Lignes` non restructurée (colonnes présentes, ordre libre toléré uniquement si les en-têtes sont reconnus).
3. **Contrôle de dérive** : pour chaque ligne, comparaison avec la compta **actuelle** (relue maintenant). Si la compta a changé depuis l'export (montant, lettrage, rapprochement, ligne disparue), la ligne est signalée « dérive » et **n'est pas acceptée automatiquement** (l'utilisateur doit ré-exporter ou accepter explicitement la version actuelle).
4. **Contrôles sur les colonnes complétées** : IF 8 caractères, ICE 15 caractères, sans espace (règles Maroc existantes) ; `Décision` dans la liste ; motif obligatoire si `Exclure` ; commentaire obligatoire si `Validation d'écart = OUI` ; code activité présent dans le référentiel ; tiers sans IF/ICE sur ligne `Inclure` = bloquant.
5. **Import d'un ancien export** : si un export plus récent existe pour le même dossier/période, avertissement fort ; refusé si des lignes de l'ancien ont déjà été déclarées définitivement (idempotence).
6. **Statut par ligne** après analyse : `Acceptée`, `Acceptée avec avertissement`, `Rejetée` (raison), `Dérive`, `Reportée`, `Exclue` ; synthèse par statut et total TVA qui serait déclarée.
7. **Les alertes bloquantes** (TASK-234 A1/A3/A4 non acquittées, écarts HT × taux non validés sur lignes `Inclure`, règlements non rapprochés `Inclure`) restent bloquantes à la confirmation, **dans le fichier comme dans l'application**.
8. **Fichier jamais modifié** par l'import ; rapport d'analyse téléchargeable (Excel) listant les rejets, pour que l'utilisateur corrige et ré-importe.
9. Les validations d'écart saisies dans le fichier alimentent `EcartValide` (TASK-228) **à la confirmation seulement**.

## Périmètre STRICT

- **Inclus** : lecteur du classeur, contrôles §2 à §5, prévisualisation, rapport d'import, endpoint (analyser, confirmer), écran d'import (dépôt du fichier, synthèse, liste XGrid, téléchargement du rapport), tests (fichier valide, chaque type d'altération, dérive, doublon, mauvaise version).
- **Exclu** : génération du XML et clôture (TASK-230) ; export (TASK-236).

## Livrables

- Lecteur + contrôles + API + écran + tests + jeu de fichiers d'essai (valide et corrompus).

## Critères de validation

- Round-trip : export → complétion simulée → import : 100 % des lignes acceptées, totaux identiques à l'export.
- Chaque altération testée est rejetée avec le bon motif : montant modifié, clé modifiée, ligne dupliquée, ligne ajoutée à la main, IF de 7 caractères, ICE avec espace, décision hors liste, `Exclure` sans motif, mauvais dossier, mauvaise période, version inconnue.
- Modification de la compta après export → ligne « dérive », non acceptée automatiquement.
- Aucune écriture en base tant que la confirmation n'est pas donnée (vérifié).
- Build + lint + build front 0 erreur ; **checklist UI** ; preuve par critère datée ; scénarios de test manuel dans `DOCS/`.

## Files

- `Declaration.Export.Excel/`, `Declaration.Compta.*`, `Declaration.API/Controllers/`, `declaration-tva-web/src/` (lire `DOCS/UI_STANDARDS.md`).
- `DOCS/FORMAT_EXCEL_AIDE_DECLARATION_V2.md` (livré par TASK-236).
