# TASK-144 — Diagnostic explicatif en ligne pour les lignes en anomalie (fin de la dépendance à l'assistant)

Status: 🆕 à faire
Priority: HIGH
Risk: MEDIUM (nouvelle lecture cross-base Sage, lecture seule stricte)
Module: Declaration.API / Declaration.Application / Declaration.Infrastructure / declaration-tva-web

> **Origine :** retour PO explicite après approbation de TASK-143 (audit exhaustif des 6
> déclarations) : « à chaque fois que je rentre dans une déclaration il faut revenir vers toi pour
> m'expliquer donc objectif non atteint ». TASK-143 a bien produit un diagnostic complet et exact
> (cas `FA2600106` compris), mais **ce diagnostic n'existe que dans un rapport Markdown ponctuel**
> (`DOCS/AUDIT-TASK-143/`) — l'application elle-même n'expose toujours rien de ce niveau
> d'explication. Le vrai objectif n'est pas un audit ponctuel mais que **le comptable comprenne par
> lui-même, dans l'app, à chaque déclaration future**, pourquoi une ligne est en anomalie, sans
> dépendre d'un assistant IA qui recreuse la base à chaque fois (mémoire projet
> `grf-objectif-autonomie-explication-app`).

## Constat (preuve de code — recherche read-only effectuée cette session)

- **Le "motif" affiché est une recopie brute, sans enrichissement.** `LigneCandidateDto.Motif`
  (`Declaration.API/Dtos/LigneCandidateDto.cs:36`) vaut simplement `l.MotifRejet` ; exposé tel quel
  en JSON (`LigneCandidateDto.cs:91-92`). Le front l'affiche sans transformation :
  `declaration-tva-web/src/DomainGrid.tsx:84` (colonne `motif` / « Motif Écartement ») et
  `declaration-tva-web/src/VerifierIntegrerPanel.tsx:720` (`{r.motif || '—'}`).
- **Le message généré est un texte technique court, générique, sans cause racine.**
  `DeclarationWorkflowService.cs:851-864` construit `Message = "Ligne en anomalie de recalcul : {motif}"` ;
  `MapLignesCandidates` (`DeclarationWorkflowService.cs:649-650`) résout ce `motif` soit depuis une
  alerte amont (ex. `ConstructeurDeclaration.cs:81-82`, code `FACTURE_INTROUVABLE` →
  `"Facture introuvable (DTO non fourni)."`), soit par un texte de repli générique
  `"Facture introuvable ou non ventilée"`. Aucun de ces textes ne dit **quelle échéance** (`EC_Id`),
  **quel numéro Sage**, ni **ce que la lecture OM a réellement retourné**.
- **Le contrôle « numéro de pièce Sage dupliqué entre tiers » (`RT_ECHEANCE` × `F_DOCREGL`) n'existe
  nulle part dans le code applicatif vivant.** `F_DOCREGL` n'apparaît dans **aucun fichier `.cs`** du
  dépôt (grep exhaustif, 0 résultat) — seulement dans la documentation d'audit
  (`DOCS/AUDIT-TASK-143/00-SYNTHESE-GLOBALE.md`, point 3). Les requêtes `RT_ECHEANCE` réellement
  exécutées (`Declaration.Infrastructure/Repositories/DeclarationRepository.cs:643,953,1011,1216`)
  ne groupent jamais par `DO_Numero` et ne joignent jamais `F_DOCREGL`. C'est un script d'audit
  ponctuel (session courante), pas une fonctionnalité de l'app.
- **Aucun écran de « preuve de valorisation » par ligne n'existe pour ce cas.**
  `ProofModal.tsx` (panneau « Preuves du règlement ») n'expose ni `EC_Id`, ni le numéro Sage brut, ni
  un statut trouvé/non-trouvé. `AffectationsDrill.tsx:441-467` expose bien `EC_Id` en interne, mais
  uniquement pour un autre type de contrôle (incohérence de recalcul post-figeage, TASK-078), pas
  comme diagnostic générique pour une ligne `FACTURE_NON_VENTILEE`.
- **Important, tiré du cas réel `FA2600106` (TASK-143, point 3)** : la collision `DO_Numero` n'était
  **pas** la cause de l'anomalie visible sur la déclaration (la ligne déclarée référence la bonne
  échéance, `EC_Id=21849`, qui a un document Sage réel) — la vraie cause était un échec de lecture
  OM distinct (« DTO non fourni »). **Conclusion pour cette task** : le diagnostic en ligne doit
  montrer **les deux informations indépendamment** (résultat de la tentative de lecture OM *et*,
  séparément, une alerte si une collision de `DO_Numero` existe) — ne pas laisser croire que l'une
  explique forcément l'autre.

## Objectif

Pour toute ligne dont `motif`/`MotifRejet` est renseigné (au premier chef `FACTURE_NON_VENTILEE`,
mais le mécanisme doit rester générique à toute ligne avec motif), permettre au comptable de voir,
**directement dans l'app, sans aide externe** :

1. L'échéance Sage précise en cause : `EC_Id`, numéro de pièce (`DO_Numero`), tiers (`RT_ECHEANCE`,
   pas `RT_MOUVEMENT` — cf. mémoire `grf-do-numero-collision-multi-tiers`, piège déjà identifié).
2. Ce que la tentative de lecture/valorisation OM a réellement produit (message d'erreur Sage exact
   si disponible, ex. `MotifErreur` de `DM_VENTILATION_SAGE_CACHE` — donnée déjà persistée,
   actuellement non restituée jusqu'au front).
3. **Nouveau contrôle, calculé et affiché** : si un autre tiers partage le même `DO_Numero` dans
   `RT_ECHEANCE`, avec verdict tranché via `F_DOCREGL` (lecture seule, base Sage résolue
   dynamiquement par `SO_Id` comme TASK-118) — lequel des deux a un document Sage réel, lequel est
   orphelin. Affiché comme une information **indépendante**, pas comme *l'*explication automatique
   de l'anomalie (cf. point ci-dessus).
4. **Traduction en langage métier + action recommandée, pas seulement les faits techniques bruts.**
   Afficher `EC_Id`/numéro Sage/message d'erreur ne suffit pas si le comptable ne sait toujours pas
   quoi en faire — c'est exactement ce que reproche le PO au rapport TASK-143 (un diagnostic exact
   mais qui nécessite quelqu'un pour le traduire). Chaque code de motif technique connu (au minimum
   `FACTURE_INTROUVABLE`, `FACTURE_ILLISIBLE_OM`, le texte de repli générique, et le nouveau cas
   collision `DO_Numero`) doit être accompagné d'une phrase en français métier (ex. « Sage n'a pas pu
   fournir le détail TVA de cette facture — vérifiez qu'elle est bien comptabilisée sous ce numéro et
   ce tiers dans Sage, sinon contactez le support Sage ») et, si pertinent, d'une action recommandée.
   **Cette traduction doit être relue et validée par le PO avant clôture** (critère de comprehensibilité,
   pas seulement d'exhaustivité technique) — ne pas se contenter d'empiler plus de données brutes.

Forme suggérée (à trancher par l'implémenteur avec le PO si besoin) : une action « Diagnostiquer »
sur la ligne en anomalie (drill dédié ou extension de `ProofModal`/`AffectationsDrill`) plutôt qu'un
enrichissement systématique de la grille — le détail est coûteux (jointure cross-base), à charger à
la demande, pas pour les 5 182 lignes à chaque affichage.

## Garde-fous

- **Lecture seule stricte** pour le nouveau contrôle collision et toute lecture `F_DOCREGL` : `SELECT`
  uniquement, aucune écriture `RT_ECHEANCE`/`F_DOCREGL`/`DM_LGTVA`.
- Connexion à la base Sage **résolue dynamiquement par `SO_Id`** (`P_SOCIETE.SO_ErpDb`, pattern déjà
  en place TASK-118) — jamais de connexion statique/codée en dur.
- Ne pas dupliquer `OrchestrateurDeclaration` : si le détail de lecture OM déjà tenté est nécessaire,
  le lire depuis le cache déjà persisté (`DM_VENTILATION_SAGE_CACHE.MotifErreur`), ne pas redéclencher
  une nouvelle lecture OM Sage à chaque clic « Diagnostiquer » (coût, session COM Sage, cf. mémoire
  `grf-tva-perf-lecture-sage`).
- Aucun nouveau calcul de valorisation ni recalcul d'équilibre — uniquement lecture et présentation de
  données déjà su/calculables.
- Ne pas modifier `ProofModal.tsx`/`AffectationsDrill.tsx` dans leur usage actuel (TASK-078) — ajouter,
  ne pas remplacer.

## Files

- `Declaration.API/Dtos/LigneCandidateDto.cs` (`Motif` l.36/91-92).
- `Declaration.Application/Services/DeclarationWorkflowService.cs` (`MapLignesCandidates` l.649-650,
  message `FACTURE_NON_VENTILEE` l.851-864).
- `Declaration.Core/ConstructeurDeclaration.cs` (messages `FACTURE_INTROUVABLE`/`FACTURE_ILLISIBLE_OM`
  l.81-82 et suivants).
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` (requêtes `RT_ECHEANCE` existantes
  l.643/953/1011/1216 — nouvelle méthode de lecture collision à ajouter, pas à modifier ces requêtes).
- `Declaration.Orchestration/VentilationSageCacheRepository.cs` (lecture `MotifErreur` déjà persisté).
- Nouveau : accès `F_DOCREGL` (base Sage dynamique par `SO_Id`, même pattern que la résolution Sage
  de TASK-118 — voir `Declaration.Infrastructure/Factories/DbConnectionFactory.cs`).
- `declaration-tva-web/src/DomainGrid.tsx` (colonne `motif` l.84), `VerifierIntegrerPanel.tsx` (l.720),
  `ProofModal.tsx`, `AffectationsDrill.tsx` (l.441-467, référence de pattern EC_Id existant).

## Validation

- [ ] Build back (`dotnet build`) et front (`tsc -b && vite build`) OK.
- [ ] Sur une ligne `FACTURE_NON_VENTILEE` réelle (rejouer le cas `FA2600106`, ligne `EC_Id=21849`,
      `TVA1-2026-02`), le comptable voit `EC_Id`/numéro Sage/tiers + le motif d'échec OM réel, sans
      quitter l'app ni solliciter d'aide externe.
- [ ] Si une collision `DO_Numero` existe pour l'échéance consultée, le verdict `F_DOCREGL` (lequel
      des tiers a un document réel) est affiché, **distinctement** de l'explication de l'échec OM.
- [ ] **Chaque motif technique connu a une traduction en français métier + action recommandée, relue
      et validée explicitement par le PO** (pas seulement vérifiée par lecture de code par
      l'architecte) — critère de compréhensibilité réelle, pas d'exhaustivité technique.
- [ ] Aucune écriture en base (confirmé par revue du VERIFY, comme TASK-143).
- [ ] Pas de nouvelle lecture OM Sage déclenchée par le diagnostic (réutilisation du cache existant).
- [ ] Non-régression : `ProofModal`/`AffectationsDrill` gardent leur comportement TASK-078 inchangé.

## Dépendances / risques

- Dépend de la résolution dynamique de connexion Sage par `SO_Id` (TASK-118) — à réutiliser, pas à
  recréer.
- Risque de latence si la lecture `F_DOCREGL` est faite en direct à chaque clic sur une grosse volumétrie
  — mitigé par le fait que ce diagnostic est déclenché à la demande (une ligne à la fois), pas en masse.
- Risque de confusion si le contrôle collision est présenté comme *la* cause de l'anomalie alors qu'il
  peut être sans rapport (cas réel `FA2600106` confirmé par TASK-143) — l'UI doit garder les deux
  informations séparées et ne jamais affirmer un lien de causalité non vérifié.
- Cette task ne couvre pas la correction des anomalies elles-mêmes (14 lignes non valorisées, IF ZF
  FOOD, désambiguïsation tiers) — c'est un besoin de **visibilité**, pas de correctif de données. Ces
  3 correctifs n'ont pas encore leur propre TASK (à écrire séparément) — **même TASK-144 livrée et
  approuvée, les 6 déclarations resteront non signables tant que ces correctifs n'existent pas.** Ne
  pas laisser croire que TASK-144 seule « résout le problème » : elle répond à la partie
  « comprendre sans redemander à l'assistant », pas à la partie « la déclaration est correcte ».
