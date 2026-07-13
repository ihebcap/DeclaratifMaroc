# TASK-076 — Visibilité des montants bruts Sage sur une facture exclue pour incohérence HT/TVA/TTC

Status: TODO
Priority: MEDIUM
Risk: LOW
Module: Écran Factures (`FactureInterrogation.tsx`) + back (`FactureInterrogationRow`, cache TASK-024)

## OBJECTIF
Suite à TASK-072 (DONE) : une pièce Sage détectée incohérente (Σ(HT+TVA+Parafiscale) ≠ TTC document,
ex. `EC_Id=21473` / `FC2501717`) est désormais exclue de la valorisation et affichée avec le motif
précis (`MotifValorisation`), mais **sans aucun montant** — `MontantHT`/`MontantTVA` restent `null`
côté cache (sentinelle `Taux=-1, CodeTaxe='ERREUR', TotalHT=0, TotalTva=0, TotalTtc=0`).

Le PO, en testant le correctif TASK-072 sur écran réel, demande à voir les **valeurs brutes lues chez
Sage** (même incohérentes) sur ces factures exclues, pas seulement le texte du motif — pour permettre
une investigation manuelle côté ERP (comparer aux montants Sage, identifier la pièce fautive sans
ressaisir une requête SQL).

## BUSINESS VALUE
Sans les montants bruts, le PO doit rouvrir Sage ou solliciter une requête ad hoc pour chaque facture
exclue signalée — ralentit l'investigation d'anomalies de données ERP découvertes en aval (impact
opérationnel, pas fiscal : la non-valorisation de la pièce reste correcte).

## CONTRAINTES
- Ne pas réintroduire les montants incohérents dans les totaux déclarables : `MontantHT`/`MontantTVA`
  actuels (colonnes valorisées, utilisées par les sous-totaux ③/④) doivent rester `null`/exclus du
  calcul — ne pas mélanger « montant brut affiché pour audit » et « montant valorisé déclarable ».
- Décision à trancher avec le PO avant codage (cf. clarification demandée, PO a répondu « je ne sais
  pas ») : lesquelles des données suivantes sont réellement utiles —
  1. Montants bruts Sage tels que lus (`TotalHTNet`/`TotalTva`/`TotalParafiscale`/`TotalTtc` avant
     exclusion, disponibles dans `SageTaxReaderService.ExtraireTaxes` au moment de la détection) ;
  2. Identification de la pièce Sage (`DO_Piece`, date, tiers) pour investigation manuelle côté ERP ;
  3. Les deux.
- Persistance : la sentinelle cache actuelle (`TotalHT=0, TotalTva=0, TotalTtc=0`) ne conserve pas les
  valeurs brutes — si le PO veut les voir sur l'écran Factures (pas seulement dans les logs/VERIFY),
  il faut soit les stocker sur la ligne sentinelle (colonnes dédiées ou `MotifErreur` étendu), soit les
  relire à la demande (coût Sage, à éviter si l'écran Factures reste lecture-seule-locale rapide).

## FILES
- `Declaration.Application/Entities/FactureInterrogation.cs` (`FactureInterrogationRow.AppliquerCacheB`,
  `MotifCacheAbsent`)
- `Declaration.API/Dtos/FactureInterrogationDto.cs`
- `declaration-tva-web/src/FactureInterrogation.tsx`
- `SageTaxReader/SageTaxReader.Contracts/DTOs.cs` (`IncoherenceHtTvaTtc`, valeurs brutes disponibles au
  moment de la détection, avant écrasement par la sentinelle)
- `Declaration.Orchestration/VentilationSageCacheRepository.cs` (`MarquerEnErreur` — persistance actuelle
  de la sentinelle à revoir si les montants bruts doivent être conservés en cache)

## VALIDATION
- [ ] Décision produit actée avec le PO sur le périmètre exact (montants bruts / identification pièce /
      les deux) avant tout codage
- [ ] Build OK
- [ ] Montants bruts (si retenus) visibles sur l'écran Factures pour une pièce exclue, visuellement
      distingués d'un montant valorisé (ex. grisé, badge « donnée brute non fiable »)
- [ ] Aucune régression : `MontantHT`/`MontantTVA` valorisés (colonnes utilisées par ③/④) restent
      strictement inchangés pour les pièces saines et pour les pièces exclues (toujours `null`/exclues
      du calcul déclarable)
- [ ] Test de non-régression couvrant l'affichage d'une pièce exclue avec ses valeurs brutes

## ARCHITECTURE RULES APPLICABLES
- Pas de dette technique silencieuse — si la solution retenue implique de relire Sage à la demande
  (coût), documenter le compromis perf/fraîcheur explicitement.
- Lecture seule stricte, aucun impact Sage ni écriture `RT_*`.
- Aucune valeur inventée : les montants bruts affichés doivent être ceux réellement lus, jamais
  recalculés/estimés côté front.

## NOTES
Origine : suivi direct de TASK-072 (DONE, 13/07/2026), demande PO en testant le correctif sur l'écran
Factures réel (`EC_Id=21473`, motif « Incoherence Sage detectee retroactivement en cache »). Périmètre
exact du besoin encore à clarifier avec le PO (réponse initiale : « je ne sais pas ») — ne pas démarrer
le codage avant cette clarification (cf. CONTRAINTES).
