# TASK-127 — Socle : résolution du délai de paiement applicable + calcul de l'échéance légale

## Contexte
Nouveau périmètre **Délai de Paiement Maroc**, analysé en lecture seule dans `apbs-gr_winform`
(`D:\_vibe\apbs-gr_winform\analayse\CDC-DELAI-PAIEMENT-MAROC.md`, validé PO 19/07/2026).
Développement neuf sur la plateforme GRF web (`Declaration.API`/`Declaration.Application`/`Declaration.Core`,
clean architecture) — **aucune réutilisation de DLL/code WinForms**, même principe que le module TVA
(`MODULE_DECLARATION_TVA.md`, `CAHIER_DES_CHARGES.md`).

Cette tâche porte le **calcul partagé** utilisé par trois consommateurs : sélection des lignes DDP
(TASK-131), résolution du délai dans les conventions (TASK-129), et mesure du délai fournisseur
(TASK-135). Elle doit être livrée **avant** ces trois tâches.

## Référence legacy (comportement à reproduire à l'identique)
`EcheancePaiementCalculator.GetDelaisPaiementTiers` (`Tresorerie.UICommun/Helper/EcheancePaiementCalculator.cs:21-56`) :

1. **Résolution du nombre de jours** (ordre de priorité) :
   - Convention type **Facture** exacte (`TiersNo` + `FactureNumero` == numéro du document) ;
   - sinon Convention type **Convention** dont `DateDebut ≤ DateDocument ≤ DateFin` ;
   - sinon délai par défaut société (`NombreJoursDelaisPaiementTiers` / colonne `SO_NbJoursDelaiPaiement`, amorçage 60).
2. **Calcul de l'échéance légale** : `datePaiement = DateDocument + nbJours`. Tant que `datePaiement`
   tombe un jour présent dans `P_JOURSREPOS` **ou** un samedi **ou** un dimanche : incrémenter `nbJours`
   de 1 et **recalculer depuis `DateDocument`** (`DateDocument.AddDays(++nbJours)`), pas depuis la dernière
   date testée. La fonction legacy retourne le **nombre de jours**, pas la date — reproduire ce contrat
   (le nombre de jours résolu ET la date d'échéance légale doivent être exposés, les deux sont utilisés
   par les consommateurs).

## Objectif
```
Entrée  : société, tiers, numéro de document, date du document, domaine (Achat/Vente)
Traitement : résoudre le délai applicable (convention Facture > convention Convention > défaut société),
             ajouter à la date document, décaler au jour ouvré suivant (weekend + P_JOURSREPOS)
Sortie  : { NombreJoursApplique, EcheanceLegale, OrigineDelai (Convention|ConventionFacture|Defaut) }
```
`OrigineDelai` est un ajout (absent du legacy) — nécessaire pour la traçabilité UI (TASK-134/135 doivent
pouvoir afficher pourquoi tel délai a été appliqué), cohérent avec le principe "aucune ligne silencieuse"
déjà appliqué sur le module TVA.

## Périmètre STRICT
- **Inclus** : service partagé (`Declaration.Application` ou `Declaration.Core`, à aligner sur l'architecture
  existante du module TVA), lecture de `P_SOCIETE.SO_NbJoursDelaiPaiement`, lecture de `P_JOURSREPOS`,
  lecture des conventions actives (dépend du repository livré en TASK-129 — si TASK-129 n'est pas encore
  livrée, mocker l'interface de lecture des conventions pour ne pas bloquer).
- **Exclu** : écriture de conventions (TASK-129), sélection de lignes DDP (TASK-131), UI (aucune).

## Étapes
1. Modéliser `IJoursReposRepository`/lecture `P_JOURSREPOS` (société) — lecture seule, table déjà existante,
   aucune modification de schéma.
2. Implémenter le calculateur avec la sémantique exacte décrite ci-dessus (retest depuis `DateDocument`,
   pas depuis la date décalée) — un test unitaire doit vérifier ce comportement précis (jours fériés
   consécutifs : `+1` puis retest complet, ne pas confondre avec un simple `while` incrémental depuis la
   date courante).
3. Exposer `OrigineDelai` pour la traçabilité.
4. Tests unitaires hors DB (mêmes standards que `Declaration.Core.Tests`) : délai par défaut, convention
   Convention active, convention Facture exacte, priorité Facture > Convention > défaut, décalage weekend
   seul, décalage jour férié seul, décalage cumulé (jour férié tombant sur le nouveau samedi calculé).

## Livrables
- Service de résolution du délai + échéance légale, avec interface pour être consommé par TASK-129/131/135.
- Tests unitaires (couverture des cas ci-dessus).

## Critères de validation
- Résultats identiques à `EcheancePaiementCalculator` sur des cas de test rejoués depuis la base réelle
  (comparaison manuelle sur un échantillon de factures fournisseur Maroc, si base disponible).
- Aucune régression de build ; tests unitaires 100 % verts.

## Risques / dépendances
- **Bloquant pour** TASK-129, TASK-131, TASK-135.
- Dépend de la lecture de `P_SOCIETE`/`P_JOURSREPOS` déjà exploitée ailleurs dans GRF (mêmes tables que
  le module TVA) — pas de nouveau risque d'accès.
