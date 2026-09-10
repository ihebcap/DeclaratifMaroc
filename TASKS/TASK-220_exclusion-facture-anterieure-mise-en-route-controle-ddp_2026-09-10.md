# TASK-220 — Exclure toute facture dont la date de facture précède la mise en route (écran Contrôle DDP)

## Contexte

Décision PO (10/09/2026) : le comportement actuel du garde-fou TASK-128
(`DelaiPaiementBootstrapGuard`) est **remplacé**, pas ajusté.

**Comportement actuel (à supprimer) :**
Le garde-fou compare la **date d'échéance légale** (`echeanceLegale`) à `DateMiseEnRouteSociete`.
Si l'échéance légale est antérieure à la mise en route ET qu'aucun historique legacy
(`RT_DECLARATIONDELAISPAIEMENTLG`) n'existe pour cette échéance, la ligne est **bloquée** en statut
`RepriseManuelleRequise` : elle attend une saisie manuelle du retard déjà connu
(`DM_REPRISE_DELAIPAIEMENT`), via le bouton "Reprise manuelle" sur
`ControleLignesDelaiPaiementPanel.tsx:154-172`, avant de pouvoir entrer dans le calcul automatique.

**Nouveau comportement demandé :**
Le critère devient la **date de facture** (`DO_Date`, propriété `DoDate` déjà portée par
`LigneSelectionDelaiPaiement`/paramètre d'entrée du calculateur — champ déjà disponible, aucun accès
DB supplémentaire requis) comparée à `DateMiseEnRouteSociete` :
- `DoDate < DateMiseEnRouteSociete` → la facture est **purement et simplement exclue** du contrôle
  DDP (pas affichée, pas comptée, pas bloquée en attente de saisie). Ceci s'applique **même si
  l'échéance légale calculée tombe après la mise en route** (ex. facture du 20/06/2024, échéance à
  90 jours = 18/09/2024, mise en route au 01/07/2024 → **exclue quand même**, règle stricte sur
  `DoDate`, décision PO explicite).
- `DoDate >= DateMiseEnRouteSociete` → calcul automatique normal, inchangé.

**Conséquence directe : le mécanisme "reprise manuelle" (saisie du retard initial) est supprimé.**
Il n'existe plus de cas où une échéance est bloquée en attente d'une saisie manuelle de retard
initial — soit la facture est postérieure à la mise en route et rentre normalement, soit elle est
antérieure et disparaît complètement du contrôle. Le PO a confirmé explicitement ce choix (pas de
variante "gardé mais recentré").

## Objectif
```
Entrée  : le garde-fou TASK-128 bloque les échéances légales antérieures à la mise en route sans
          historique, en attente d'une saisie manuelle du retard initial (mécanisme "reprise
          manuelle") — critère basé sur l'échéance légale calculée.
Traitement : remplacer le critère par la date de facture (DO_Date) et remplacer le blocage par une
             exclusion pure et simple ; supprimer le mécanisme de reprise manuelle devenu obsolète
             (backend + UI).
Sortie  : toute facture dont DO_Date < DateMiseEnRouteSociete de sa société n'apparaît plus du tout
          dans le contrôle DDP, sans distinction d'échéance légale ni possibilité de saisie manuelle
          pour la réintégrer.
```

## Périmètre STRICT

- **Inclus** :
  1. `Declaration.Core/DelaiPaiementBootstrapGuard.cs` : remplacer le paramètre d'entrée
     `echeanceLegale` par `dateFacture` (DO_Date) dans `Resoudre(...)` — ou équivalent selon ce que
     le WORKER juge le plus propre : possiblement supprimer entièrement ce garde-fou et son enum
     `StatutBasculeEcheance`/`ResultatBasculeEcheance` si sa seule fonction (bascule automatique vs
     reprise manuelle) n'a plus de raison d'être une fois le 3ᵉ état (`AnterieureAvecRepriseSaisie`)
     supprimé — à trancher et documenter en VERIFY. Si conservé, le garde-fou devient une simple
     exclusion booléenne (`DoDate >= DateMiseEnRouteSociete`), sans état "en attente de reprise".
  2. `Declaration.Core/SelectionDelaiPaiementCalculator.cs` (lignes ~378-414 et alentours) : la
     ligne dont `DoDate < DateMiseEnRouteSociete` doit être **filtrée avant construction de toute
     `LigneSelectionDelaiPaiement`** (ni Candidate, ni RepriseManuelleRequise) — elle ne doit
     produire AUCUNE ligne de sortie, quel que soit son bucket/cas (1, 2 ou 3).
  3. Suppression du statut `StatutLigneDelaiPaiement.RepriseManuelleRequise` et de la valeur
     `OrigineBorneReference.Indeterminee`/`RepriseManuelle` si plus aucun code chemin ne peut les
     produire après ce changement — vérifier tous les usages avant suppression (grep complet sur la
     solution, y compris tests).
  4. `Declaration.Infrastructure/Repositories/SelectionDelaiPaiementRepository.cs` : si
     `GetDernieresBornesDeclareesAsync`/la requête de sélection des échéances candidates peut être
     simplifiée pour exclure `DoDate < DateMiseEnRouteSociete` en amont (filtre SQL plutôt qu'en C#
     après coup) — optimisation laissée au jugement du WORKER, non strictement requise si le filtre
     côté calculateur pur suffit.
  5. `Declaration.API/Dtos/DeclarationDelaiPaiementDto.cs` : retirer les champs devenus orphelins si
     `RepriseManuelleRequise`/`Indeterminee`/`RepriseManuelle` sont supprimés (`OrigineBorneReference`
     n'aurait alors plus que 2 valeurs utiles : `DerniereDeclaration`, `EcheanceLegale`).
  6. `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx` : supprimer le bouton "Reprise
     manuelle" (lignes ~154-172) et le badge Statut "Reprise manuelle requise" (colonne `statut`,
     ligne ~141) devenus impossibles à atteindre ; supprimer aussi le mapping `RepriseManuelle` dans
     `LIBELLES_ORIGINE_DELAI` si l'enum est réduit à l'étape 5.
  7. `DM_REPRISE_DELAIPAIEMENT` (table SQL) : **ne pas supprimer ni modifier le schéma** (règle
     absolue du projet — aucune modification de schéma sur les tables GRF) ; si elle devient inutile
     fonctionnellement, le signaler en VERIFY sans y toucher, décision de rétention/archivage laissée
     au PO séparément.
  8. `Declaration.Core/DelaiPaiementBootstrapRepository.cs` (repository associé) et tout appelant du
     garde-fou : mettre à jour la signature d'appel en cohérence avec le nouveau critère.
- **Exclus / hors périmètre** :
  - Modifier `DateDebutDeclarationLoi` (seuil légal statutaire, 2023-07-01,
    `SeuilsLegauxDelaiPaiement.cs`) — distinct de `DateMiseEnRouteSociete` (paramètre par société),
    ne pas confondre les deux, ne pas toucher au premier.
  - Modifier la logique de calcul de l'échéance légale elle-même (`EcheanceLegaleCalculator`) —
    aucun changement requis, l'échéance légale continue d'être calculée normalement pour les
    factures qui passent le nouveau filtre.
  - TASK-219 (badge d'origine) : la valeur `RepriseManuelle` du badge devient obsolète par
    conséquence de cette TASK — **coordination requise, cf. Risques ci-dessous**, mais ne pas
    modifier TASK-219 directement ici ; signaler l'impact en VERIFY.
  - Purge/nettoyage des données historiques déjà présentes dans `DM_REPRISE_DELAIPAIEMENT` — hors
    périmètre technique, décision PO séparée si nécessaire.

## Étapes
1. Grep exhaustif de tous les usages de `StatutBasculeEcheance`, `AnterieureAvecRepriseSaisie`,
   `AnterieureRetardInconnu`, `RepriseManuelleRequise`, `OrigineBorneReference.RepriseManuelle`,
   `OrigineBorneReference.Indeterminee`, `ReprisesManuelles` (backend + front + tests) pour
   cartographier tout ce qui doit être adapté ou supprimé avant de commencer.
2. Modifier `DelaiPaiementBootstrapGuard`/le point d'appel dans `SelectionDelaiPaiementCalculator`
   pour utiliser `DoDate` au lieu de `echeanceLegale` comme critère, et transformer le résultat
   "bloqué en attente de reprise" en **exclusion pure** (aucune ligne produite).
3. Adapter/supprimer les enums, DTO, et code front devenus orphelins (étapes 3-6 du périmètre
   ci-dessus).
4. Mettre à jour ou supprimer les tests unitaires existants qui couvrent
   `DelaiPaiementBootstrapGuard`/le cas `RepriseManuelleRequise` (chercher les fichiers de test
   associés, ex. `*BootstrapGuard*Tests.cs` ou équivalent) — remplacer par des tests couvrant le
   nouveau critère `DoDate` (au moins : facture avant mise en route → exclue ; facture après →
   incluse ; facture avant mise en route mais échéance légale après → exclue quand même ; pas de date
   de mise en route configurée → comportement à confirmer avec le PO si ambigu, cf. Risques).
5. Vérifier sur données réelles (SO_Id=1, ou toute société avec des factures à cheval sur la date de
   mise en route) que le nouveau filtre produit le résultat attendu — capture ou requête SQL de
   preuve dans le VERIFY.
6. `dotnet build DeclarationTVA.slnx` → 0 erreur ; `npm run lint` + `npm run build` dans
   `declaration-tva-web/` → 0 erreur.

## Livrables
- `DelaiPaiementBootstrapGuard.cs`, `SelectionDelaiPaiementCalculator.cs`,
  `SelectionDelaiPaiementRepository.cs` (si touché), `DeclarationDelaiPaiementDto.cs`,
  `ControleLignesDelaiPaiementPanel.tsx` mis à jour.
- Tests unitaires mis à jour couvrant le nouveau critère.
- `VERIFY/TASK-220_verify.md` : preuve sur données réelles (avant/après), liste des enums/champs
  supprimés avec justification qu'aucun code résiduel n'y fait plus référence, logs build/lint,
  signalement explicite de l'impact sur TASK-219 si celle-ci est déjà livrée ou en cours.

## Critères de validation
- Aucune facture avec `DO_Date < DateMiseEnRouteSociete` de sa société n'apparaît dans le contrôle
  DDP, quel que soit son bucket ou son échéance légale calculée.
- Aucune facture avec `DO_Date >= DateMiseEnRouteSociete` n'est affectée par ce changement (calcul
  automatique strictement identique à avant pour ces lignes).
- Le bouton et le statut "Reprise manuelle (requise)" ont disparu de l'écran (plus jamais atteignables
  après ce changement).
- Aucune référence de code morte aux symboles supprimés (`StatutBasculeEcheance.AnterieureAvecRepriseSaisie`
  et voisins) si le WORKER choisit de les supprimer plutôt que de les laisser inutilisés.
- `dotnet build` + `npm run lint`/`build` → 0 erreur.

## Risques / dépendances
- **Cas non trivial à trancher explicitement avec le PO si ambigu en cours d'implémentation** :
  `DateMiseEnRouteSociete == null` (société pas encore configurée). Comportement actuel :
  `AnterieureRetardInconnu` pour TOUTES les échéances (calcul désactivé pour la société entière). Le
  nouveau critère doit préciser ce cas — probablement "aucune exclusion possible tant que la date
  n'est pas configurée" reste la position la plus sûre (le WORKER ne doit pas improviser, signaler et
  bloquer si le comportement souhaité n'est pas évident depuis cette TASK).
- **Impact direct sur TASK-219** (badge d'origine par ligne) : si TASK-219 est déjà implémentée ou en
  cours au moment de cette TASK, la valeur de badge "Reprise manuelle" devient un état mort à retirer
  — à signaler explicitement en VERIFY, coordination nécessaire entre les deux TASKs.
- **Table `DM_REPRISE_DELAIPAIEMENT`** : les données historiques saisies avant ce changement restent
  en base (pas de suppression de schéma/données autorisée par ce projet) mais deviennent
  fonctionnellement mortes — écart entre code et données legacy à documenter, pas à corriger ici.
- Changement de comportement métier significatif (des factures visibles aujourd'hui dans le contrôle
  disparaîtront) : recommandé de vérifier l'impact chiffré sur les déclarations en cours (nombre de
  lignes qui disparaîtraient) avant bascule en production, pour éviter une surprise au moment du dépôt
  T3 2026.
