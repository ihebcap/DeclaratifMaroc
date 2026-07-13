# TASK-077 — Purge/recalcul des lignes déjà figées avant le garde-fou TASK-072 (incohérence HT/TVA/TTC)

Status: TODO
Priority: HIGH
Risk: MEDIUM (touche des lignes déjà figées `DM_LGTVA` sur des déclarations non clôturées)
Module: `Declaration.Application/Services/DeclarationWorkflowService.cs` (figeage) +
`Declaration.Orchestration/OrchestrateurDeclaration.cs` (revalidation cache) +
`Declaration.Infrastructure/Repositories/DeclarationRepository.cs` (persistance `DM_LGTVA`)

## OBJECTIF
Le garde-fou `IncoherenceHtTvaTtc.EstIncoherent` (TASK-072, DONE) protège correctement tout **nouveau**
chargement de lignes : lecture OM Sage (`SageTaxReaderService.cs:363-369`), revalidation rétroactive du
cache de ventilation (`OrchestrateurDeclaration.cs:367-386`), et construction du modèle
(`ConstructeurDeclaration.cs:88-101`, exclusion + alerte `FACTURE_ILLISIBLE_OM`).

Mais `DeclarationWorkflowService.ChargerCandidatesSiNecessaireAsync` (lignes 117-154) n'exécute ce
pipeline **qu'une seule fois** par déclaration+domaine :

```csharp
if (await _repository.GetLignesCountAsync(declarationId, domaine, null) > 0) return; // déjà figé
```

Une fois les lignes écrites dans `DM_LGTVA`, elles ne sont **plus jamais recalculées**, quel que soit
l'état actuel du garde-fou ou du cache. Cas réel constaté par le PO (13/07/2026) sur l'écran ② Affectations
de la déclaration TVA1-2026-01 : la pièce Sage `EC_Id=21473`/`FC2501717` (Règlement `RF26040040`) apparaît
avec **TVA déclarée = 344 050,24 MAD pour un TTC affiché = 20 700,00 MAD** (Base TVA = TVA, ce qui est en
soi le signe patent de l'incohérence Σ(HT+TVA+Parafiscale)≠TTC détectée par TASK-072). Cette pièce a été
figée dans `DM_LGTVA` **avant** que TASK-072 (et sa revalidation rétroactive du cache) ne s'applique à
cet `EC_Id` — le commentaire de `OrchestrateurDeclaration.cs:367-386` cite d'ailleurs explicitement ce cas
(«cas réel observé : EC_Id=21473/FC2501717, caché avant ce correctif»).

## BUSINESS VALUE
Sans purge, toute déclaration figée avant TASK-072 (ou avant la revalidation rétroactive du cache pour
une pièce donnée) contient des lignes TVA incohérentes qui **restent affichées et potentiellement
déclarables indéfiniment**, y compris après correction du garde-fou en amont. Risque fiscal direct
(TVA déclarée manifestement fausse, ici ×16 le TTC) sur une déclaration non clôturée que le PO croit
protégée par TASK-072.

## CONTRAINTES
- Ne pas modifier `DM_LGTVA` pour une déclaration déjà **clôturée** (verrou `DT_Id`/TASK-028,
  triggers d'immuabilité TASK-064) — cette task ne concerne que les déclarations **non clôturées**
  (lignes encore à l'état `Proposee`/modifiable).
- Le mécanisme doit être **détectable et rejouable**, pas une purge ponctuelle en base : une nouvelle
  pièce Sage incohérente peut arriver après cette task, il faut un moyen répétable de resynchroniser une
  déclaration ouverte avec l'état courant du garde-fou (pas nécessairement automatique à chaque lecture —
  décision produit à trancher, voir VALIDATION).
- Ne pas recalculer silencieusement des montants qui changeraient une déclaration en cours d'examen par
  le PO sans traçabilité — toute ligne purgée/recalculée doit être visible (alerte, historique, ou les deux).
- Cohérence avec le garde-fou existant : réutiliser `IncoherenceHtTvaTtc.EstIncoherent` (ne pas
  dupliquer la règle de détection).

## FILES
- `Declaration.Application/Services/DeclarationWorkflowService.cs:117-154`
  (`ChargerCandidatesSiNecessaireAsync` — garde `count > 0` à faire évoluer ou contourner via une action
  explicite de resynchronisation)
- `Declaration.Orchestration/OrchestrateurDeclaration.cs:317-407` (pipeline de revalidation déjà existant,
  à réutiliser pour identifier les pièces incohérentes touchant une déclaration ouverte)
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs:118-130, 811-859`
  (persistance/lecture `DM_LGTVA`, méthode de suppression/réécriture ciblée à ajouter si besoin)
- `SageTaxReader/SageTaxReader.Contracts/DTOs.cs:48-57` (`IncoherenceHtTvaTtc.EstIncoherent`, règle à
  réutiliser sans dupliquer)
- `Declaration.Core/ConstructeurDeclaration.cs:88-101` (logique d'exclusion existante à l'entrée du
  pipeline, référence pour le comportement attendu sur une ligne déjà figée)

## DÉCISIONS PO ACTÉES (13/07/2026, ne pas redemander)
- **Déclenchement** : resynchronisation **automatique**, à **chaque** appel de
  `ChargerCandidatesSiNecessaireAsync` (donc à chaque chargement de l'étape 1 / `GET /{id}/lignes`),
  même quand la déclaration est déjà figée (`count > 0`) — pas de bouton manuel dédié.
- **Périmètre de revérification** : (a) incohérence Sage détectée après coup (sentinelle `ERREUR` en
  cache `GRC_VENTILATION_SAGE_CACHE` pour l'`EC_Id` de la ligne, règle TASK-072/076) **et** (b)
  règlement dépointé depuis le figeage (`MV_Point` courant ≠ `Point_Oui` sur le `MV_Id` de la ligne).
- **Action à la détection** : **signaler seulement, ne rien retirer**. La ligne figée et les totaux
  ③/④ restent strictement inchangés (aucun recalcul, aucune exclusion automatique) — uniquement une
  alerte visible (nouveau code, ex. `LIGNE_FIGEE_A_REVERIFIER`) exposée au PO pour vérification
  manuelle avant clôture. Cohérent avec la contrainte « aucune valeur inventée ni recalcul silencieux ».
- **Prérequis technique** : `LigneCandidate`/`DM_LGTVA` ne portent aujourd'hui ni `EC_Id` ni `MV_Id`
  (seulement `NumeroFacture`/`NumeroRapprochement` texte) — ajouter ces deux colonnes (migration
  idempotente) + les persister au figeage (disponibles sur `AffectationADeclarer`/`AffectationCandidate`
  au moment de `MapLignesCandidates`), condition nécessaire pour la revalidation ciblée par clé.

## VALIDATION
- [x] Décision produit actée avec le PO (voir DÉCISIONS PO ACTÉES ci-dessus)
- [ ] Build OK
- [ ] Sur la déclaration TVA1-2026-01 (cas réel PO), la ligne `EC_Id=21473`/`FC2501717` est détectée comme
      incohérente et **exclue** (ou signalée + non comptée dans les totaux ③/④) après application du
      correctif, sans intervention SQL manuelle
- [ ] Aucune régression sur les lignes déjà figées et **cohérentes** (montants inchangés, pas de
      re-figeage inutile)
- [ ] Aucune tentative de resynchronisation sur une déclaration **clôturée** (verrou `DT_Id` respecté,
      test explicite)
- [ ] Traçabilité : une alerte ou un historique visible signale qu'une ligne a été exclue/corrigée par
      resynchronisation rétroactive (pas une disparition silencieuse)
- [ ] Test de non-régression couvrant : déclaration ouverte + ligne incohérente pré-existante → purge/
      exclusion effective

## ARCHITECTURE RULES APPLICABLES
- Pas de dette technique silencieuse — le trou de couverture (figeage = snapshot non revalidé) doit être
  fermé de façon rejouable, pas par une correction SQL ponctuelle sur ce seul cas.
- Aucune modification d'une déclaration clôturée (cohérence avec TASK-028/TASK-064).
- Aucune valeur inventée ni recalcul silencieux : toute correction rétroactive doit être traçable.

## NOTES
Origine : investigation PO (13/07/2026) suite à la capture d'écran ② Affectations montrant
`FC2501717`/`RF26040040` avec TVA déclarée (344 050,24 MAD) largement supérieure au TTC affiché
(20 700,00 MAD). Confirmé par relecture de code : le garde-fou TASK-072 est correctement câblé sur tout
nouveau chargement, mais `DM_LGTVA` est un instantané figé une seule fois
(`ChargerCandidatesSiNecessaireAsync`, garde `count > 0`), sans mécanisme de purge/recalcul pour les
déclarations existantes dont les lignes ont été gelées avant l'entrée en vigueur du correctif. Suite
logique de TASK-072, distincte de TASK-076 (qui porte sur la visibilité des montants bruts, pas sur la
persistance des lignes incohérentes déjà figées).
