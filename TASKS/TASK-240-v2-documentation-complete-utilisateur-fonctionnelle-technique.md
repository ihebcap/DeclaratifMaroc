# TASK-240 — V2 : documentation complète (utilisateur, fonctionnelle et fiscale, installation, technique)

Status: 🆕 à faire (vit tout au long du projet ; clôture après la dernière TASK fonctionnelle)
Priority: HIGH
Risk: MEDIUM (une doc fausse ou périmée coûte plus cher que pas de doc)
Module: `DOCS/V2/` + aide intégrée à l'application
Dépend de : s'alimente de toutes les TASKs V2 ; TASK-238 (codes d'événements), TASK-239 (catalogue des règles)

> **Origine :** PO 16/10/2026 — « on doit tout documenter, une documentation complète ». Objectif commercial : l'utilisateur et le support se débrouillent sans intervention sur place.

## Décisions de conception

1. **Quatre documents, quatre publics**, rédigés en français :
   - **Guide utilisateur** (comptable) : le parcours Exporter → compléter → Importer → Déclarer ; comment remplir chaque colonne jaune du classeur ; que faire pour chaque alerte (une fiche par code d'alerte : sens, cause probable, action) ; exemples avec captures.
   - **Dossier fonctionnel et fiscal** : règles métier (date d'opération `JM_Date + EC_Jour − 1`, rapprochement banque seule, report, prorata, imputation proportionnelle, contrôle HT × taux), **chaque formule avec un exemple chiffré**, hypothèses et limites (avoirs hors périmètre, encaissement sans XML), correspondance avec le format XML DGI.
   - **Guide d'installation et d'administration** : prérequis (accès SQL en lecture seule à la base compta Sage), installation, configuration du dossier (colonnes IF/ICE, préfixe des comptes de trésorerie), mise à jour, sauvegarde de `DeclaratifMaroc`, résolution d'incidents avec le pack de diagnostic.
   - **Dossier technique** : architecture (schéma du cadrage §5), modèle de la base `DeclaratifMaroc`, **spécification du format Excel** (`FORMAT_EXCEL_AIDE_DECLARATION_V2.md`, TASK-236), catalogue des codes d'événements (TASK-238) et des règles de plausibilité (TASK-239), procédure de test.
2. **Documentation intégrée** : l'application affiche l'aide contextuelle (bouton « Aide » par écran, lien depuis chaque alerte vers sa fiche, formule visible via le panneau « Pourquoi ce montant ? ») à partir des mêmes sources Markdown (une seule source de vérité).
3. **Règle de « terminé » pour toute TASK V2** (appliquée par l'architecte en revue, ajoutée à `TODO_V2.md`) : une TASK n'est approuvée que si sa documentation est mise à jour (formules, alertes, colonnes, codes d'événements, captures). Documentation absente ou périmée = REJECT.
4. **Anti-dérive** : les catalogues d'alertes et de règles sont **générés ou vérifiés automatiquement** depuis le code (test qui échoue si un code d'alerte n'a pas de fiche de documentation).
5. **Réutiliser** `GUIDE_PROCESS_DECLARATION_TVA.html` et `PREREQUIS-CLIENT-DECLARATIF-MAROC.html` (V1) comme point de départ de forme et de ton ; ne pas y mélanger les règles V2.
6. **Glossaire** unique (lettrage, rapprochement, date d'opération, ligne, report, exclusion…), pour que les mêmes mots désignent les mêmes choses partout (application, Excel, docs).

## Périmètre STRICT

- **Inclus** : les quatre documents, la fiche par code d'alerte, le glossaire, l'aide intégrée, le test anti-dérive, un jeu de captures.
- **Exclu** : documentation de la V1 ; supports commerciaux ; vidéos de formation (à décider ensuite).

## Livrables

- `DOCS/V2/` (4 documents + glossaire + fiches d'alerte), aide intégrée, test anti-dérive.

## Critères de validation

- Un comptable n'ayant jamais vu l'outil réalise un cycle complet (export, complétion, import, déclaration) **en suivant uniquement le guide** (test d'usage, observations jointes).
- Chaque code d'alerte et d'événement existant a sa fiche ; le test anti-dérive passe, et échoue si on en retire une.
- Chaque formule du dossier fonctionnel a un exemple chiffré qui redonne le résultat du code (vérifié par test).
- Relecture fiscale par le PO (règles et exemples).
- Preuve par critère datée dans le VERIFY.

## Files

- `DOCS/V2/`, `DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md`, `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html`, `DOCS/PREREQUIS-CLIENT-DECLARATIF-MAROC.html`, `declaration-tva-web/src/` (aide intégrée).
