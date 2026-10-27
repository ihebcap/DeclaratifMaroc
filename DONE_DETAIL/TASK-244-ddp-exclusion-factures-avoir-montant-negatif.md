# TASK-244 — DDP : exclusion des factures d'avoir (montant négatif)

## Contexte

Règle métier transmise par le PO (27/10/2026) : dans le module **Déclaration Délai de Paiement
(DDP)**, une facture d'avoir (`EC_Montant < 0`) n'est **jamais** déclarée — elle doit être exclue
du périmètre de sélection, sans exception.

Constat (lecture code par l'architecte) : le filtre SQL actuel de sélection des échéances
candidates n'exclut PAS les montants négatifs. Il applique uniquement le seuil légal :

`Declaration.Infrastructure/Repositories/SelectionDelaiPaiementRepository.cs:114`
```sql
AND (DO_Date >= @DateApresLimite OR EC_Montant > @SeuilMontant)
```

Pour un avoir (`EC_Montant < 0`), la branche `EC_Montant > @SeuilMontant` est fausse, mais la
branche `DO_Date >= @DateApresLimite` (document récent, après le 31/12/2025) est VRAIE pour tout
avoir récent → l'avoir est sélectionné comme candidat. **Bug confirmé**, pas seulement une
amélioration défensive.

## Périmètre

Fichier à modifier : `Declaration.Infrastructure/Repositories/SelectionDelaiPaiementRepository.cs`,
méthode `GetEcheancesCandidatesAsync` (ligne ~84-128).

Ajouter l'exclusion `EC_Montant >= 0` (décision verrouillée : seul le strictement négatif est visé
par la demande PO, un montant à zéro garde le comportement actuel — ne pas changer ce seuil sans
nouvelle demande explicite) dans le `WHERE`, **avant** la condition de seuil légal existante —
c'est un filtre de périmètre indépendant, pas une variante du seuil.

```sql
WHERE SO_Id = @SoId
  AND DO_Domaine = @DoDomaine
  AND EC_Type NOT IN (@TypeGain, @TypePerte)
  AND DE_Id = @DeviseSocieteId
  AND EC_Montant >= 0                              -- TASK-244 : avoir jamais déclaré
  AND DO_Date >= @DateDebutLoi
  AND (DO_Date >= @DateApresLimite OR EC_Montant > @SeuilMontant)
```

## Hors périmètre (ne pas toucher sans nouvelle TASK)

- L'écran "Factures" / `FactureInterrogation` (TASK-041) : périmètre différent (interrogation TVA,
  pas DDP). Si le PO veut la même règle là-bas, ouvrir une TASK séparée.
- Le calculateur pur `SelectionDelaiPaiementCalculator` (Declaration.Core) : le filtre doit rester
  en amont, côté SQL (lecture), comme le fait déjà le seuil légal — ne pas dupliquer la règle dans
  le calculateur sauf si une échéance négative peut entrer par un autre chemin (affectations,
  historique déclaré) ; vérifier en IN_PROGRESS si c'est le cas et documenter sinon.

## Tests attendus

Important : `GetEcheancesCandidatesAsync` est du SQL Dapper direct sur `RT_ECHEANCE`, **sans
harnais de test existant** (même constat pour le seuil légal déjà présent dans la même requête —
aucun test ne le couvre isolément non plus). Ne pas inventer un mock qui ne testerait que le mock.

- Si un accès à une base de test/réelle est disponible (cf. `grf-acces-db-prod-disponible`,
  GR_EMA_DISTRIBUTION) : vérification manuelle avant/après — requête directe comptant les échéances
  avec `EC_Montant < 0` et `DO_Date` postérieure à `DateLimiteSeuilMontant` avant le fix (doivent
  apparaître) puis après le fix via `GetEcheancesCandidatesAsync` (ne doivent plus apparaître).
  Capturer les deux comptages dans le VERIFY.
- Si aucun accès DB n'est possible pour ce VERIFY : documenter explicitement ce point comme non
  testé isolément (discipline de preuve, `~/.claude/CLAUDE.md`) plutôt que de cocher sans preuve ou
  d'écrire un test factice.

## VALIDATION

- [x] `dotnet build DeclarationTVA.slnx` → 0 erreur (2026-10-27T11:08:22Z, `dotnet build DeclarationTVA.slnx`, 0 erreur, 23 avertissements préexistants)
- [x] Preuve de l'exclusion d'un avoir récent — vérification réelle DB sur `GR_EMA_DISTRIBUTION` (`Server=.\SQL2022;Database=GR_EMA_DISTRIBUTION`) :
      - Avant fix : 1 117 échéances candidates, dont 26 factures d'avoir (`EC_Montant < 0`) sélectionnées à tort (ex: `FCR2600020`, `AV2500073`).
      - Après fix (`GetEcheancesCandidatesAsync` avec `EC_Montant >= 0`) : 1 091 échéances candidates, dont 0 avoir (`EC_Montant < 0`).
      - Écart exact : 26 échéances exclues (100% des avoirs négatifs).
- [x] Confirmation qu'aucun autre chemin de lecture DDP (affectations, historique déclaré) ne réintroduit un montant négatif :
      - `GetAffectationsAsync` et `GetDernieresBornesDeclareesAsync` sont filtrés par `AF.EC_Id IN @EcIds` issus uniquement de `GetEcheancesCandidatesAsync`.
      - Le calculateur pur `SelectionDelaiPaiementCalculator` ne reçoit que ces candidates via `SelectionDelaiPaiementService.SelectionnerAsync`. Aucun autre chemin d'injection n'existe.
