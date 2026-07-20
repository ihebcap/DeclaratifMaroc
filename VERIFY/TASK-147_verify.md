# VERIFY — TASK-147

Date: 2026-07-20
Agent: worker nocturne (Claude Code)

## BUILD

- Status: OK
- Erreurs: aucune.
- Back : `dotnet build Declaration.API/Declaration.API.csproj` → 0 erreur.
- Front : `npx tsc -b` → 0 erreur ; `npx vite build` → build réussi (1 avertissement préexistant
  `INEFFECTIVE_DYNAMIC_IMPORT`, non lié).

## FICHIERS MODIFIÉS

- `Declaration.Application/Entities/DiagnosticLigne.cs` — nouveaux records `CacheLectureRow`,
  `CacheBucketRow` ; `DiagnosticLigneResultat` étendu (bloc 4 : `CachePerime`, `CacheDateLecture`,
  `CachePerimeCommentaire`).
- `Declaration.Application/Interfaces/IDeclarationRepository.cs` — 3 nouvelles méthodes :
  `GetDerniereLectureCacheAsync`, `GetBucketsCacheAsync`, `SupprimerLignesParEcIdAsync`.
- `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` — implémentation des 3
  méthodes ci-dessus (lecture seule pour les deux premières, DELETE ciblé pour la troisième — jamais
  appelée seule, toujours suivie d'une réinsertion dans le même appelant).
- `Declaration.Application/Services/DeclarationWorkflowService.cs` :
  - `DiagnostiquerLigneAsync` étendu (bloc 4, staleness) — compare `GetDerniereLectureCacheAsync`
    à `declaration.DateCreation`, sans nouvelle lecture Sage.
  - Nouvelle méthode `RecalculerLigneDepuisCacheAsync(declarationId, ecId)` — reconstruit la ligne
    depuis les buckets déjà en cache (même forme que la branche "taxesLines" de
    `MapLignesCandidates`), sans redéclencher de lecture OM. Garde-fous : borné aux lignes encore
    `Proposee`, no-op si le cache n'est pas effectivement plus récent/sans erreur.
- `Declaration.API/Dtos/DiagnosticLigneDto.cs` — champs `cachePerime`/`cacheDateLecture`/
  `cachePerimeCommentaire` ajoutés à la projection JSON.
- `Declaration.API/Controllers/DeclarationsController.cs` — nouvel endpoint
  `POST {id}/lignes/recalculer-depuis-cache` (404 si ligne introuvable, 409 si les conditions de
  staleness ne sont plus réunies — rien écrit dans ce cas).
- `declaration-tva-web/src/api.ts` — champs staleness sur `DiagnosticLigneDto` + fonction
  `recalculerLigneDepuisCache`.
- `declaration-tva-web/src/DiagnosticModal.tsx` — bloc 4 (message + bouton « Recalculer cette
  ligne »), prop `onRecalculated`.
- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` — `reloadToken` (state) ajouté aux
  dépendances des deux `useEffect` de chargement (lignes/checkup) + câblage `onRecalculated`.
- `scratch/TestTask147/` (nouveau, non livré en production) — script de preuve réelle, voir
  section Validation.

## DIFF RÉSUMÉ

Une ligne `Proposee` figée avec un motif de rejet (ex. "Facture introuvable ou non ventilée")
n'était jamais réévaluée si le cache Sage était relu avec succès APRÈS la création de la
déclaration (cas réel signalé : `TVA1-2026-02`/`FA2600106`, cache lu à 23:17:36 avec succès,
déclaration créée à 17:41:35 — 5h30 plus tôt — motif "introuvable" resté affiché). Ajout d'un
diagnostic de staleness (bloc 4, indépendant du bloc 2 motif technique) + d'une action « Recalculer
cette ligne » qui reconstruit directement la ligne depuis le cache déjà bon, sans redéclencher de
lecture OM Sage (coût quasi nul, conforme au garde-fou).

## VALIDATION CHECKLIST

- [x] Build back + front OK.
- [~] **Rejeu réel sur `TVA1-2026-02`/`FA2600106`/`EC_Id=21849` — cas d'origine devenu non
      reproductible tel quel.** Cette déclaration a été **recréée pendant cette même nuit de
      travail** (`DateCreation` désormais `2026-07-20 00:56:10`, **postérieure** à la lecture de
      cache de 23:17:36) — probablement par une autre activité de test/rejeu de la session
      (cf. TASK-146, qui utilise la même `DeclarationId=054a3ed1-...` pour son propre cas
      `FC2501667`). La ligne `EC_Id=21849` porte désormais déjà des montants HT/TVA/TTC valorisés
      (`MotifRejet` vide) — le cache n'est donc plus "périmé" pour cette instance précise,
      confirmé par requête directe. **Ce n'est pas un échec du correctif** : le diagnostic
      (`CachePerime=false` attendu ici) serait cohérent avec l'état réel actuel — mais je ne
      peux plus démontrer le cas positif *sur ce cas précis*.
- [x] **Preuve réelle de bout en bout obtenue sur un cas équivalent construit à partir de données
      réelles** (`scratch/TestTask147/Program.cs`, exécuté avec `dotnet run`, contre
      `GR_EMA_DISTRIBUTION` réelle) : ligne réelle `EC_Id=18198` (`FC2600004`,
      `TVA1-2026-04`, `DeclarationId=663948df-5201-45cd-a288-607120c43664`,
      `DateCreation=2026-07-19 23:18:42`), actuellement `MotifRejet='Facture introuvable ou non
      ventilée'`, HT=1152/TVA=0/TTC=0. Une lecture de cache **synthétique** (simulant une vraie
      relecture OM réussie, forme identique à une entrée réelle) a été insérée avec
      `DateLecture` postérieure à la déclaration. Résultat mesuré (sortie console reproduite) :
      - `DiagnostiquerLigneAsync` → `CachePerime=True`, message staleness correct.
      - `RecalculerLigneDepuisCacheAsync` → `Trouvee=True, Recalculee=True`, 1 bucket appliqué.
      - `DM_LGTVA` après recalcul : `Etat=Proposee, MotifRejet='', HT=960, Taux=20, TVA=192,
        TTC=1152` — exactement les valeurs du bucket de cache, aucune nouvelle lecture Sage
        déclenchée (le cache n'a pas été retouché par le recalcul, seule la ligne LGTVA a changé).
      - État original **restauré** après le test (ligne + purge de la lecture synthétique) —
        vérifié : `DM_VENTILATION_SAGE_CACHE` total = 5382 lignes (identique à l'état
        post-purge TASK-145, aucune fuite de donnée de test).
- [x] Non-régression : `ResynchroniserLigneAsync` (TASK-078, `AffectationsDrill.tsx`) **non
      modifiée** — vérifié par diff (aucune ligne touchée dans cette méthode).
- [x] Cas cache réellement en erreur (`MotifErreur` non nul) : le garde `derniereLecture.MotifErreur
      != null` dans `RecalculerLigneDepuisCacheAsync` et la condition équivalente dans le bloc 4 du
      diagnostic empêchent `CachePerime=true` dans ce cas — vérifié par lecture de code (logique
      symétrique testée positivement dans le script ci-dessus, la branche négative n'a pas été
      rejouée séparément mais découle directement de la même condition booléenne).

## RESTE À VALIDER (honnête, non silencieux)

1. Le cas exact cité en session (`FA2600106`/`EC_Id=21849`) n'a pas pu être rejoué car son état a
   changé entre le moment où la TASK a été rédigée et l'exécution de cette TASK (déclaration
   recréée par une autre activité de test la même nuit). La preuve a été apportée sur un cas
   équivalent construit à partir de données réelles (voir ci-dessus) — le mécanisme est démontré
   fonctionnel, mais pas sur le cas nommément cité par la TASK.
2. Le bouton front (`DiagnosticModal.tsx`) n'a pas été cliqué dans un navigateur réel dans cet
   environnement (pas de session UI disponible) — seul le contrat API sous-jacent a été exercé
   directement (script `dotnet run`). Build front (`tsc`+`vite`) passe, code relu ligne à ligne.
3. Le script `scratch/TestTask147/` reste dans le dépôt (dossier `scratch/`, cohérent avec les
   scripts `TestTaskXXX` déjà présents pour d'autres TASKs) — pas un artefact de production,
   documenté ici pour traçabilité de la preuve.

## IMPACTS DÉTECTÉS

- Aucun endpoint/comportement existant modifié (uniquement des ajouts : nouveaux champs DTO,
  nouvelle méthode service, nouvel endpoint, nouveau bloc UI conditionnel).
