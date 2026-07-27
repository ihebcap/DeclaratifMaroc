# TASK-127 Verify — Socle : résolution du délai de paiement applicable + calcul de l'échéance légale

> Implémentation worker. Développement NEUF sur GRF web, AUCUNE réutilisation de DLL/code WinForms.
> Contrainte schéma respectée : AUCUNE modification de table, AUCUNE table créée — lecture seule de
> `P_JOURSREPOS` et `P_SOCIETE` (tables existantes possédées par apbs-gr_winform).
> Build back (`dotnet build DeclarationTVA.slnx`) : **0 erreur**. Tests Core : **61/61 verts** (dont
> 9 nouveaux pour ce socle). Rejeu contre les **données réelles** `GR_EMA_DISTRIBUTION` effectué
> (voir § Vérification base réelle). **Un point de câblage DI reste volontairement ouvert** (dépend de
> TASK-129) — voir § « Reste à valider ».

## Périmètre livré

Socle partagé (calcul unique du délai de paiement Maroc + échéance légale), reproduisant à l'identique
`EcheancePaiementCalculator.GetDelaisPaiementTiers` (`Tresorerie.UICommun/Helper/EcheancePaiementCalculator.cs:21-56`),
avec ajout de `OrigineDelai` pour la traçabilité. Trois couches, alignées sur l'architecture du module TVA :

1. **Calculateur PUR (Declaration.Core)** — `EcheanceLegaleCalculator.Calculer(...)`, hors DB, testable
   comme `CodeActiviteResolver`. Sémantique :
   - Résolution du délai, priorité stricte : **convention Facture exacte** (`TiersNo` +
     `FactureNumero == documentNumero`, comparaison ordinale, reproduisant l'opérateur `==` du legacy)
     > **convention Convention** (`DateDebut ≤ DateDocument ≤ DateFin`) > **défaut société**.
   - Échéance légale : `DateDocument + nbJours`, puis **tant que** la date tombe dans `P_JOURSREPOS`
     **ou** un samedi **ou** un dimanche : `nbJours++` et **recalcul depuis `DateDocument`**
     (`DateDocument.AddDays(nbJours)`), jamais depuis la dernière date testée — contrat legacy
     `documentDate.AddDays(++nbJours)` conservé (date et compteur toujours cohérents).
   - Sortie : `{ NombreJoursApplique, EcheanceLegale, OrigineDelai (ConventionFacture|Convention|Defaut) }`.
2. **Service d'orchestration (Declaration.Application)** — `IDelaiPaiementService` / `DelaiPaiementService` :
   lit conventions (filtrées par domaine), jours de repos et délai défaut société, puis délègue au
   calculateur pur. Aucune logique métier dupliquée.
3. **Repository de référentiel (Declaration.Infrastructure)** — `DelaiPaiementReferentielRepository`
   (LECTURE SEULE, Dapper, connexion GRF) : `SELECT JR_Date FROM P_JOURSREPOS WHERE SO_Id=@…` et
   `SELECT SO_NbJoursDelaiPaiement FROM P_SOCIETE WHERE SO_Id=@…` (repli 60 si NULL).

Interface de lecture des conventions (`IConventionDelaiPaiementRepository`) : **définie seule** (contrat),
implémentation déléguée à TASK-129 (comme prévu par la TASK : « si TASK-129 n'est pas encore livrée,
mocker l'interface »). Aucun mock/repli silencieux posé en production (voir § Reste à valider).

## Fichiers créés

Back — Declaration.Core :
- `Declaration.Core/EcheanceLegaleCalculator.cs` — calculateur pur + types d'E/S (`ConventionDelaiPaiement`,
  `ResultatDelaiPaiement`) + enums `DomaineDelaiPaiement` (Achat/Vente), `OrigineDelai`.

Back — Declaration.Application :
- `Declaration.Application/Interfaces/IJoursReposRepository.cs`
- `Declaration.Application/Interfaces/IDelaiPaiementParametrageRepository.cs`
- `Declaration.Application/Interfaces/IConventionDelaiPaiementRepository.cs` (contrat seul, impl TASK-129)
- `Declaration.Application/Services/DelaiPaiementService.cs` (`IDelaiPaiementService` + impl)

Back — Declaration.Infrastructure :
- `Declaration.Infrastructure/Repositories/DelaiPaiementReferentielRepository.cs`

Tests :
- `Declaration.Core.Tests/EcheanceLegaleCalculatorTests.cs` (9 tests).

Fichiers modifiés : **aucun** (aucun fichier existant touché). `Program.cs` **non modifié** — voir
§ Reste à valider (câblage DI = concern TASK-129).

## Couverture de tests (9 nouveaux, hors DB)

- Délai par défaut société (aucune convention).
- Convention intervalle active sur la date document.
- Convention Facture exacte.
- Priorité Facture > Convention > défaut (les trois applicables simultanément → Facture gagne).
- Convention d'un autre tiers ignorée → repli défaut.
- Décalage week-end seul (samedi → dimanche → lundi).
- Décalage jour férié seul sur un jour ouvré.
- Décalage cumulé : deux fériés consécutifs + week-end, dont un férié tombant un samedi — vérifie le
  **retest depuis `DateDocument`** (nbJours=5, échéance lundi).
- Partie heure de la date document ignorée (travail sur `.Date`).

Dates réelles 2025, jours ouvrés/week-ends vérifiés.

## Vérification base réelle (GR_EMA_DISTRIBUTION, DESKTOP-5BFKKEP → 127.0.0.1)

- `P_SOCIETE` : `SO_Id=1`, `SO_NbJoursDelaiPaiement = 60` (les deux requêtes exactes du repository ont
  été exécutées et renvoient les données attendues).
- `P_JOURSREPOS` : 16 jours de repos réels 2025 pour `SO_Id=1`.
- `RT_CONVENTIONTIERS` : **0 ligne** (aucune convention en base → tous les cas réels retombent
  aujourd'hui sur `OrigineDelai.Defaut`).
- **Rejeu du calculateur compilé contre le référentiel réel** (test temporaire, exécuté puis retiré,
  non commité) :
  - `2025-05-31 + 60` = `2025-07-30` (mercredi férié) → `2025-07-31` (jeudi ouvré), nbJours=61 — conforme.
  - `2025-04-08 + 60` = `2025-06-07` (samedi ET férié) → `06-08` (dimanche ET férié) → `2025-06-09`
    (lundi ouvré), nbJours=62 — conforme (exerce le décalage cumulé week-end + fériés consécutifs réels).

## Checklist

- [x] Build back `dotnet build DeclarationTVA.slnx` — **0 erreur** (24 warnings, tous préexistants,
      hors périmètre TASK-127).
- [x] Tests Core `dotnet test Declaration.Core.Tests` — **61/61 verts**, dont 9 nouveaux ; aucune
      régression sur les tests existants.
- [x] Aucune modification de schéma / aucune table créée (`DeclarationTVA.sql` non touché).
- [x] Lecture seule stricte : uniquement des `SELECT` sur `P_JOURSREPOS` et `P_SOCIETE`. Aucun
      INSERT/UPDATE/DELETE.
- [x] Aucune réutilisation de DLL/code WinForms — code neuf, seule la SÉMANTIQUE legacy est reproduite.
- [x] Pas de SQL inline hors couche repository (ARCHITECTURE §5) : le SQL est confiné dans
      `DelaiPaiementReferentielRepository` ; Core et Application sont sans SQL.
- [x] Pas de secret codé en dur, pas de bypass sécurité.
- [x] `OrigineDelai` exposé pour la traçabilité (consommable TASK-134/135).
- [x] Comportement de retest « depuis DateDocument » couvert par un test dédié.

## Reste à valider (NON couvert / décision hors périmètre worker)

1. **Câblage DI volontairement non posé (dépend de TASK-129) — À FINALISER PAR TASK-129.**
   `Program.cs` n'a **pas** été modifié. Raison : `DelaiPaiementService` dépend de
   `IConventionDelaiPaiementRepository`, dont l'implémentation (lecture `RT_CONVENTIONTIERS`) relève de
   TASK-129. Enregistrer le service sans cette implémentation créerait un échec DI latent ; enregistrer
   un mock renvoyant une liste vide introduirait un **repli silencieux** en production (délai toujours
   ramené au défaut société même en présence de conventions) — proscrit (ARCHITECTURE §5, « aucune
   ligne silencieuse »). Le socle est livré comme **bibliothèque compilée et testée** ; TASK-129
   enregistrera `IConventionDelaiPaiementRepository` + `IJoursReposRepository`/
   `IDelaiPaiementParametrageRepository` (→ `DelaiPaiementReferentielRepository`) + `IDelaiPaiementService`.
   **Non bloquant pour TASK-127** (aucun consommateur runtime dans ce périmètre), mais à ne pas oublier
   au moment de câbler TASK-129/131/135.

2. **Mapping `DomaineDelaiPaiement` → `RT_CONVENTIONTIERS.CP_Domaine`** : l'enum est nommé métier
   (Achat=0/Vente=1) pour aligner sur l'objectif TASK-127, en écho au legacy `DomaineConvention`
   (Fournisseur=0/Client=1). La correspondance exacte Achat↔Fournisseur / Vente↔Client au niveau de la
   requête conventions est **à confirmer et à implémenter dans TASK-129** (le calculateur, lui, ne fait
   que recevoir des conventions déjà filtrées par domaine — il n'utilise pas la valeur du domaine).

3. **Rejeu sur conventions réelles impossible aujourd'hui** : `RT_CONVENTIONTIERS` est vide en base.
   Les cas « convention Facture » et « convention intervalle » ne sont donc validés que par tests
   synthétiques (données fournies explicitement). Un rejeu sur conventions réelles restera pertinent
   quand TASK-129 en aura chargé/créé (comparaison avec l'échantillon factures fournisseur Maroc
   mentionné dans les critères de validation, « si base disponible »).

4. **Edge legacy conservé à l'identique (documenté, non corrigé)** : l'appariement Facture utilise
   `FactureNumero == documentNumero` (ordinal), reproduisant l'opérateur `==` du legacy — y compris
   `null == null → true`. En théorie, un `documentNumero` null + une convention à `FactureNumero` null
   seraient appariés comme `ConventionFacture`. Ce cas **ne se produit pas en données réelles** (un
   document porte toujours un numéro). Reproduction fidèle assumée ; à signaler si un besoin de durcir
   ce point émerge côté consommateurs.

## Verdict

Socle livré, compilé (0 erreur), testé (61/61), et rejoué contre les données réelles pour le chemin
`Defaut` + décalages jours ouvrés. Les seuls points ouverts sont le **câblage DI final (TASK-129)** et
le **rejeu sur conventions réelles** (base actuellement sans convention) — aucun n'est bloquant pour la
livraison du socle lui-même.
