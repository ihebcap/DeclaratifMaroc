# TASK-241 — V2 : mémoire du dossier et suggestions (auto-apprentissage déterministe, sans IA)

Status: 🆕 à faire
Priority: MEDIUM (fort gain d'usage, après l'aller-retour Excel)
Risk: MEDIUM (une valeur mémorisée à tort peut se propager d'un mois à l'autre)
Module: Declaration.Compta.Core + Declaration.Compta.Persistence + Declaration.Export.Excel + Declaration.API + declaration-tva-web
Dépend de : TASK-236, TASK-237 (aller-retour Excel), TASK-238 (audit), TASK-239 (règles de plausibilité) ; front = TASK-231 + TASK-232

> **Origine :** PO 16/10/2026 — « un système avec auto-apprentissage sans IA ». Objectif commercial : réduire le travail de l'utilisateur d'un mois sur l'autre pour tenir le modèle « facile, peu cher, vendu en volume ».

## Décisions de conception

1. **Aucune IA, aucun modèle statistique opaque** : uniquement des règles déterministes et des statistiques classiques (médiane, percentiles, écart à la moyenne) que l'on peut expliquer en une phrase.
2. **Ce qui est mémorisé** (par dossier, jamais partagé entre dossiers) : IF et ICE saisis par tiers, désignation par tiers et par compte de charge, code activité par tiers et par compte de charge, décision habituelle (`Inclure`/`Reporter`/`Exclure`) par tiers avec motif, écarts HT × taux déjà validés (par facture, montants figés, TASK-228). **Jamais mémorisés : les montants, les taux, les dates** (ils viennent toujours de la compta).
3. **Alimentation** : à la **confirmation** d'un import (TASK-237), jamais à la simple analyse ni à l'export. Une valeur n'est mémorisée que si l'utilisateur l'a fournie ou modifiée.
4. **Utilisation** : à l'export (TASK-236), les colonnes jaunes sont **préremplies** depuis la mémoire ; chaque valeur reprise est marquée « prérempli depuis [déclaration du jj/mm/aaaa] » (colonne source ou commentaire de cellule, et dans l'application). Priorité des sources : valeur de la fiche tiers Sage (`F_COMPTET`) si renseignée, sinon mémoire, sinon vide. En cas de conflit entre Sage et mémoire, une alerte avertissement est affichée (jamais de choix silencieux).
5. **Suggestion, jamais décision** : une valeur mémorisée ne contourne aucun contrôle (IF 8 caractères, ICE 15, listes autorisées, alertes bloquantes, validation d'écart explicite). Un écart déjà validé est signalé « déjà validé le… » mais **reste à revalider** si les montants ont changé.
6. **Seuils adaptatifs** pour les règles de plausibilité de TASK-239 : en option par règle, remplacement d'un seuil fixe par un seuil calculé sur l'historique du dossier (par exemple médiane et écart interquartile des 6 dernières périodes). La règle affiche toujours le seuil retenu, son mode (fixe ou calculé) et les périodes utilisées. Nombre minimal de périodes requis, sinon seuil fixe.
7. **Détection de nouveautés** : premier passage d'un tiers, d'un compte de TVA, d'un journal de trésorerie, d'un code taxe ou d'un taux jamais vus dans le dossier → information « nouveau » (jamais bloquant seul).
8. **Contrôle utilisateur** : écran « Mémoire du dossier » (XGrid) listant tout ce qui est retenu, avec source et date ; modifier, **oublier une valeur**, tout réinitialiser, exporter. Toute modification passe par l'audit (TASK-238).
9. **Stockage** : tables EF dans `DeclaratifMaroc` (migrations automatiques, TASK-225), clé = dossier + type + identifiant ; versionnées (historique de la valeur, pas seulement la dernière).
10. **Confidentialité** : aucune sortie hors du poste ; la mémoire d'un dossier n'est jamais utilisée pour un autre dossier, y compris du même cabinet.

## Périmètre STRICT

- **Inclus** : tables et repository de mémoire, alimentation à la confirmation d'import, préremplissage de l'export avec traçabilité de la source, gestion des conflits Sage/mémoire, seuils adaptatifs optionnels, détection de nouveautés, écran « Mémoire du dossier », tests.
- **Exclu** : IA ou apprentissage statistique opaque ; mémorisation de montants, taux ou dates ; partage entre dossiers ; correction automatique de la compta.

## Livrables

- Mémoire + préremplissage + seuils adaptatifs + nouveautés + écran + tests + documentation (TASK-240).

## Critères de validation

- Cycle sur 2 périodes (jeu de test) : après import confirmé de la période 1, l'export de la période 2 préremplit IF/ICE, désignation, code activité et décision pour les tiers déjà vus, chaque valeur portant sa source.
- Une valeur mémorisée invalide (IF de 7 caractères) est quand même rejetée par les contrôles de TASK-237.
- Conflit Sage/mémoire : alerte affichée, valeur Sage retenue, rien de silencieux.
- « Oublier » une valeur : elle n'est plus reprise à l'export suivant, l'action est dans l'audit.
- Écart déjà validé avec montants modifiés : signalé « à revalider ».
- Seuil calculé : la règle affiche seuil, mode et périodes ; avec moins de périodes que le minimum, le seuil fixe est utilisé (preuve par test).
- Aucune lecture de la mémoire d'un dossier A pendant le traitement d'un dossier B (test).
- Build + lint + build front 0 erreur ; **checklist UI** ; preuve par critère datée dans le VERIFY.

## Files

- `Declaration.Compta.Core/`, `Declaration.Compta.Persistence/`, `Declaration.Export.Excel/`, `declaration-tva-web/src/` (lire `DOCS/UI_STANDARDS.md`).
- `TASKS/TASK-236`, `TASK-237`, `TASK-238`, `TASK-239`.
