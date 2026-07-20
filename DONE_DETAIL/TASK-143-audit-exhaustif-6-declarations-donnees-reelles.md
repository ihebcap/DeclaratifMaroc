# TASK-143 — Audit exhaustif des 6 déclarations existantes contre les données réelles Sage (factures + règlements)

Status: 🆕 à faire
Priority: HIGH
Risk: LOW (lecture seule stricte)
Module: Declaration.API / Declaration.Application / Declaration.Core / Declaration.Orchestration (audit), aucune modification fonctionnelle requise

> **Origine :** demande PO — 6 déclarations créées (`TVA1-2026-01` à `TVA1-2026-06`, société `SO_Id=1`,
> toutes `Statut=0` « en cours », confirmé en base `DM_ENTTVA`/`DM_LGTVA` : **5 182 lignes** au total).
> Le PO veut une vérification **exhaustive**, comme le ferait un comptable qui doit signer la
> déclaration : chaque facture, chaque règlement, chaque ligne recoupée avec les **données réelles**
> (Sage + GRF), pas seulement les contrôles déjà affichés à l'écran. Déclencheur direct : l'anomalie
> `FA2600106` diagnostiquée manuellement dans cette session (facture Sage `EC_Id=18608` orpheline —
> aucune ligne `F_DOCREGL` derrière son `DR_No`, coïncidence de numéro avec une facture réelle d'un
> autre fournisseur) — le PO veut savoir **combien d'autres cas similaires existent** sur les 5 182
> lignes des 6 déclarations, sans devoir les débusquer une par une à la main.

## Constat (infra déjà en place — à réutiliser, ne pas réinventer)

- **Persistance** : entêtes `DM_ENTTVA` (id, numéro, société, exercice, période, statut), lignes
  `DM_LGTVA` (FK `DeclarationId`, `Etat`, `Domaine`, `NumeroFacture`, `TiersICE`/`TiersIdentifiantFiscal`,
  `EC_Id`, `MV_Id`, `IncoherenceValidee`/`Par`/`Le` — TASK-078). Le tampon de verrouillage vit à part :
  `RT_AFFECTATION.DT_Id` (base GRF), verrouillé par triggers d'immuabilité (TASK-064).
- **Endpoint d'agrégation par déclaration déjà exhaustif** : `GET /api/declarations/{id}/checkup`
  (`Declaration.API/Controllers/DeclarationsController.cs:216+`) boucle déjà en interne sur
  `GetLignesAsync(..., 1, int.MaxValue, ...)` pour Decaissement + Encaissement — **toutes les lignes,
  sans pagination** — et retourne `recapSource`, `recapTaux`, `recapIncoherence` (résidu TTC≠HT+TVA),
  `equilibre{isValid, ecart, ecartExplique}` et la liste complète des `alertes`. C'est le point d'entrée
  naturel pour boucler sur les 6 déclarations.
- **Diagnostic tampon DT_Id déjà scripté** : `GET /api/diagnostic/dt-id?dtIds=...`
  (`DeclarationsController.cs:457-475`, réservé `UT_Admin=1`) — sans paramètre, balaie déjà tous les
  `DT_Id` distincts de `RT_AFFECTATION` à la recherche de tampons orphelins.
- **Codes d'alerte déjà émis** (à agréger, pas à réinventer) : `REGLEMENT_NON_AFFECTE`,
  `SOLDE_INITIAL_NON_GERE`, `ECART_FGR`, `CODE_TAXE_INCONNU`/`ERREUR_FGR`, `FACTURE_INTROUVABLE`,
  `FACTURE_ILLISIBLE_OM`, `LIGNE_A_ZERO`, `SANS_ACTIVITE`, `EQUILIBRE_RESIDU_INEXPLIQUE`,
  `TIERS_SANS_ICE`/`ICE_INVALIDE`/`TIERS_SANS_IF`/`IF_INVALIDE` (`Declaration.Core/ConstructeurDeclaration.cs`),
  `REGLEMENT_LIBERE_REINTEGRE`, `LIGNE_FIGEE_A_REVERIFIER`, `AUCUNE_LIGNE_INTEGREE`, `LIGNE_EXCLUE`,
  `REGLEMENT_EXCLU`, `FACTURE_NON_VENTILEE` (`Declaration.Application/Services/DeclarationWorkflowService.cs`).
  **TASK-060 constate déjà qu'aucune agrégation par code n'existe nulle part** (ni back, ni front, ni
  `logs/valorisation.log`) — cet audit doit produire cette agrégation, au moins pour son propre rapport.
- **Aucun contrôle existant ne couvre** le cas trouvé aujourd'hui : numéro de pièce Sage (`DO_Numero`)
  partagé entre 2 tiers différents dans `RT_ECHEANCE`, avec une échéance orpheline côté Sage (son
  `EC_No` n'a **aucune** ligne correspondante dans `F_DOCREGL.DR_No`). Ce contrôle est **nouveau**, à
  ajouter à l'audit (cf. mémoire projet `grf-do-numero-collision-multi-tiers`).
- **Aucun script/outil ne boucle déjà sur les 6 déclarations pour un audit consolidé** — les scripts
  `scratch/*.ps1` existants sont tous des sondes ponctuelles sur un cas précis.

## Objectif

Produire, en lecture seule stricte, un **rapport d'audit consolidé** sur les 6 déclarations
(`TVA1-2026-01` → `TVA1-2026-06`) qu'un comptable peut relire pour juger si elles sont déclarables en
l'état. Le rapport doit couvrir, pour **chaque ligne des 6 déclarations** (5 182 lignes) :

1. **Recoupement avec les alertes déjà calculées** : appeler `/api/declarations/{id}/checkup` pour les
   6 `Id` (`DM_ENTTVA.Id`), agréger toutes les `alertes` par `Code` (compte + exemples), par déclaration
   et en cumulé sur les 6 — combler le manque documenté par TASK-060.
2. **Contrôle de cohérence arithmétique** : reprendre `recapIncoherence` (déjà calculé par le checkup)
   pour lister toutes les lignes où `TTC ≠ HT + TVA`, avec le motif réel sous-jacent (ne pas s'arrêter à
   l'écart chiffré — remonter jusqu'à l'alerte/`MotifRejet` qui l'explique, comme fait manuellement pour
   `FA2600106`).
3. **Nouveau contrôle : numéro de pièce Sage dupliqué entre tiers**. Requête SQL en lecture seule sur
   `RT_ECHEANCE` (base GRF) : grouper par `DO_Numero` (+ `DO_Domaine`) ayant `COUNT(DISTINCT CT_No) > 1`
   sur les échéances effectivement référencées par les 5 182 lignes (`DM_LGTVA.EC_Id`). Pour chaque
   groupe trouvé, vérifier via `F_DOCREGL` (base Sage `NEW_EMA DISTRIBUTION`, jointure `EC_No = DR_No`)
   laquelle des échéances a un document réel — signaler les orphelines.
4. **Contrôle du tampon DT_Id** : appeler `/api/diagnostic/dt-id` (sans filtre) pour les règlements des
   6 déclarations, vérifier l'absence de tampon orphelin ou de collision entre déclarations.
5. **Contrôle règlements/affectations** : pour chaque déclaration, vérifier que
   `Σ MontantAffecte (RT_AFFECTATION liées aux lignes DM_LGTVA)` correspond au total affecté attendu, et
   qu'aucune affectation n'apparaît sur 2 déclarations différentes (double-déclaration).
6. **Contrôle qualité tiers** : compter/lister les lignes `TIERS_SANS_ICE`/`TIERS_SANS_IF`/`ICE_INVALIDE`/
   `IF_INVALIDE` déjà remontées par les alertes (point 1) — les regrouper par tiers (pas seulement par
   ligne) pour que le PO sache **quels fournisseurs** ont un dossier incomplet.

### Format du rapport attendu

Un document (Markdown ou JSON, au choix de l'exécutant) par déclaration + une synthèse globale,
contenant au minimum : total lignes / lignes propres / lignes en alerte (par code) / lignes en
incohérence arithmétique (avec motif réel) / cas de numéro dupliqué (liste des `DO_Numero` concernés,
tiers impliqués, lequel est orphelin) / anomalies DT_Id / total HT-TVA-TTC recalculé vs `equilibre` du
checkup. Pas de correctif appliqué dans cette tâche — uniquement le diagnostic.

## Garde-fous

- **Lecture seule stricte** : uniquement des `SELECT` (base GRF `GR_EMA_DISTRIBUTION` et base Sage
  `NEW_EMA DISTRIBUTION`) et des appels `GET` aux endpoints existants. **Aucun** `INSERT`/`UPDATE`/
  `DELETE`, aucune écriture dans `DM_ENTTVA`/`DM_LGTVA`/`RT_*`/`F_DOCREGL`, aucun appel au worker OM en
  écriture, aucune génération de fichier XML.
  \ Ne jamais modifier `RT_AFFECTATION.DT_Id` (verrou TASK-028/064) ni contourner les triggers
  d'immuabilité.
- Aucune modification de code de production requise pour produire le rapport — si l'agrégation par code
  (point 1) ou le contrôle de duplication (point 3) s'avèrent utiles à pérenniser dans l'application,
  ce sera l'objet d'une **tâche distincte** (ne pas mélanger audit et développement dans cette tâche).
- Le rapport doit citer les **preuves réelles** (Id/numéro de facture, `EC_Id`, `DT_Id`, valeurs
  chiffrées) — jamais une estimation ou un résumé non vérifiable.

## Files (lecture / requêtes, aucune modification)

- `Declaration.API/Controllers/DeclarationsController.cs` (`checkup` l.216+, `diagnostic/dt-id` l.457-475).
- `Declaration.Core/ConstructeurDeclaration.cs` (liste des codes d'alerte, référence).
- `Declaration.Application/Services/DeclarationWorkflowService.cs` (codes d'alerte additionnels).
- Base GRF `GR_EMA_DISTRIBUTION` : `DM_ENTTVA`, `DM_LGTVA`, `RT_ECHEANCE`, `RT_AFFECTATION`,
  `RT_MOUVEMENT`.
- Base Sage `NEW_EMA DISTRIBUTION` : `F_DOCREGL` (lien `RT_ECHEANCE.EC_No = F_DOCREGL.DR_No`).
- Mémoire projet : `grf-do-numero-collision-multi-tiers` (mécanisme du contrôle point 3),
  `grf-echeance-ectype-mapping`, `grf-verrou-declaration-dt-id`.

## Validation

- [ ] Les 6 déclarations (`TVA1-2026-01` à `06`) sont couvertes, aucune ligne des 5 182 exclue du
      périmètre de l'audit sans justification tracée.
- [ ] Agrégation par code d'alerte produite pour chaque déclaration + cumul global (nombre de lignes,
      pas juste présence/absence).
- [ ] Chaque ligne en incohérence arithmétique (`recapIncoherence`) est expliquée par son motif réel
      (alerte/`MotifRejet`), pas seulement listée avec son écart chiffré.
- [ ] Contrôle « numéro de pièce dupliqué entre tiers » exécuté sur l'ensemble des `EC_Id` référencés
      par les 6 déclarations (pas seulement `FA2600106`), avec verdict `F_DOCREGL` pour chaque cas
      trouvé.
- [ ] Contrôle DT_Id exécuté, aucune anomalie non tracée.
- [ ] Rapport livré (un fichier par déclaration + une synthèse), lisible par un non-développeur
      (comptable), avec chiffres vérifiables (pas de résumé qualitatif seul).
- [ ] Aucune écriture en base, aucun appel de génération XML/déclaration, confirmé par le VERIFY.

## Dépendances / risques

- Dépend de l'accès DB déjà disponible (`GR_EMA_DISTRIBUTION` + `NEW_EMA DISTRIBUTION`,
  `DESKTOP-5BFKKEP`, lecture seule).
- Volume : 5 182 lignes / 6 déclarations — prévoir un traitement scripté (pas une revue manuelle
  écran par écran), en s'appuyant sur `/checkup` (déjà non paginé) plutôt que sur l'écran ② paginé.
- Risque de faux positifs sur le contrôle « numéro dupliqué » (point 3) si deux tiers partagent
  légitimement un même `DO_Numero` avec 2 documents Sage réels valides des deux côtés (cas non observé
  à ce jour, mais possible) — le rapport doit distinguer collision réelle (2 documents valides) vs
  échéance orpheline (0 document `F_DOCREGL`, cas confirmé sur `FA2600106`).
- Cette tâche est un **diagnostic**, pas un correctif : toute anomalie confirmée générera ses propres
  TASK de correction (ex. affichage du tiers en cas de collision, cf. discussion FA2600106).
