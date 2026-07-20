# TASK-128 — Paramètre "date de mise en route" par société + garde-fou de bascule

## Contexte
Décision PO (19/07/2026, session d'analyse CDC-DELAI-PAIEMENT-MAROC) : le calcul incrémental du retard
déclaré (TASK-131) a besoin de savoir, pour chaque société, **à partir de quand** le module Délai de
Paiement Maroc est piloté par ce nouveau système. Sans cette borne, une échéance déjà en retard au moment
de la mise en route serait traitée comme "jamais déclarée" et se verrait attribuer tout son retard cumulé
depuis la date de facture — alors qu'une partie a pu être déjà régularisée ou déclarée hors système
(papier, Excel, ancienne app) avant l'adoption de ce module.

**Contrainte explicite du PO** : ne pas toucher au schéma existant (`P_SOCIETE` est partagé avec l'app
legacy et le reste de GRF) — ce paramètre vit dans une **table neuve**, propriété exclusive de GRF.

## Objectif
```
Entrée  : société
Donnée  : date de mise en route du module Délai de Paiement (saisie une fois, par société)
Usage   : TASK-131 (sélection des lignes hors délai) compare l'échéance légale de chaque facture à
          cette date pour décider si le calcul automatique du retard s'applique ou si une reprise
          manuelle est nécessaire (cf. §Règle ci-dessous)
```

## Règle de bascule (à appliquer dans TASK-131, portée par cette table)
Pour une échéance dont l'échéance légale est **antérieure** à la date de mise en route de sa société,
**et** qui n'a aucune ligne historique dans `RT_DECLARATIONDELAISPAIEMENTLG` (donc jamais déclarée dans
ce système ni dans l'ancien, puisque la table est partagée/réutilisée telle quelle) :
- **ne pas** calculer automatiquement un `Depassement` cumulé depuis l'échéance légale (silencieusement
  faux — retard potentiellement déjà connu/traité hors système) ;
- l'afficher dans l'écran de contrôle (TASK-134) avec un statut explicite **"Antérieure à la mise en
  route — retard réel inconnu"**, exclue par défaut du calcul automatique ;
- permettre une **saisie manuelle ponctuelle** : "retard déjà connu/déclaré jusqu'au [date]" — cette
  saisie initialise la borne de référence pour le calcul incrémental futur de cette échéance précise
  (équivalent d'un solde d'ouverture comptable). Une fois saisie, l'échéance rentre dans le calcul
  automatique normal pour les périodes suivantes.
- Sans saisie manuelle, la ligne reste visible (pas de suppression silencieuse) mais non intégrable
  à une déclaration tant que la reprise n'a pas été faite.

## Périmètre STRICT
- **Inclus** : nouvelle table (ex. `DM_PARAM_DELAIPAIEMENT_SOCIETE` ou nom conforme à la convention `DM_*`
  déjà actée TASK-065 — à trancher avec l'architecte au moment du nommage), migration SQL idempotente
  (miroir des migrations déjà livrées, ex. `008_DM_ENTTVA_DT_Id.sql`), et une seconde table/mécanisme pour
  la reprise manuelle par échéance (ex. `DM_REPRISE_DELAIPAIEMENT` : `SO_Id`, `EC_Id`, `DateDejaDeclareeJusquau`,
  `UT_Id`, `DateSaisie`).
- **Exclu** : l'algorithme de sélection lui-même (TASK-131 consomme cette donnée), l'écran de saisie
  (TASK-134 porte l'UI ; cette tâche ne livre que le back : entité, repository, endpoint minimal de
  lecture/écriture).

## Étapes
1. Modéliser les 2 tables (paramètre société + reprise par échéance), migration SQL idempotente
   (`IF NOT EXISTS` / vérification de colonne avant `ALTER`, comme les migrations précédentes du projet).
2. Repository + service : lecture de la date de mise en route par société (valeur par défaut si absente :
   à trancher — probablement "aucune bascule à gérer" tant que le PO n'a pas saisi la date, donc calcul
   automatique désactivé par défaut plutôt qu'une date arbitraire).
3. Endpoint API minimal : `GET/PUT` date de mise en route par société (protégé, cohérent avec le reste
   des endpoints de paramétrage) ; `GET/POST` reprise manuelle par échéance.
4. Tests unitaires : société sans date de mise en route configurée (comportement par défaut), échéance
   antérieure sans reprise (exclue), échéance antérieure avec reprise saisie (incluse, borne = date saisie).

## Livrables
- 2 tables + migration idempotente.
- Repository/service + endpoints.
- Tests unitaires.

## Critères de validation
- Aucune échéance antérieure à la mise en route n'est incluse automatiquement dans un calcul de retard
  sans reprise manuelle explicite (jamais de valeur silencieuse).
- Migration rejouable sans erreur sur une base déjà à jour (idempotence).

## Risques / dépendances
- **Bloquant pour** TASK-131 (la sélection des lignes DDP dépend de cette garde-fou).
- Nommage des tables à aligner avec la convention `DM_*` (TASK-065) avant de coder — vérifier avec
  l'architecte si un préfixe dédié au module Délai de Paiement est souhaité (éviter la collision avec
  les tables TVA `DM_ENTTVA`/`DM_LGTVA`).
