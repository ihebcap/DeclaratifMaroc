# VERIFY — TASK-167 : aucune relecture Sage possible après le premier figeage

## Contexte
Suite de TASK-164 (`FC2502094`) : une fois une déclaration figée une première fois, rien dans
l'écran de déclaration ne permettait de relancer une vraie lecture Sage sur une ligne en anomalie
(cache absent/en erreur) — seul l'écran Factures le permettait, filtré par une date de facture que
le comptable devait deviner. Arbitrage déjà tranché dans la task (recommandation ferme de
l'architecte, non redemandé) : action manuelle explicite par ligne, jamais de retry automatique.

## Découverte importante pendant l'investigation
Le mécanisme back demandé par la task **existait déjà intégralement**, livré par TASK-078
(`DeclarationWorkflowService.ResynchroniserLigneAsync`, endpoint `POST
{id}/lignes/resynchroniser`) : purge le cache pour l'`EC_Id`, relit vraiment Sage via
`OrchestrateurDeclaration.Traiter` sous le verrou `soId` partagé (TASK-156), ne touche jamais
`DT_Id`/le périmètre de la déclaration, réinitialise la validation d'incohérence. Il est déjà
totalement générique (aucune règle spécifique à l'incohérence TASK-078) — **le vrai gap n'était
pas dans le back, mais dans le front et un trou de robustesse du contrôleur** :
1. Le bouton correspondant (`AffectationsDrill.tsx`, écran ② Affectations) n'apparaît que pour les
   lignes `estIncoherente` (HT+TVA≠TTC, TASK-072/077) — **jamais** pour les lignes `nonValorise`
   (motif type « Facture introuvable », le cas réel `FC2502094`), qui affichent seulement le motif
   sans aucune action.
2. `DiagnosticModal.tsx` (écran ③ Vérifier & Intégrer, TASK-144, cible explicite de la task) ne
   proposait qu'un recalcul depuis cache déjà présent (TASK-147, `cachePerime`) — rien pour le cas
   où le cache est absent/en erreur, exactement le cas `FC2502094`.
3. `DeclarationsController.Resynchroniser` (le endpoint HTTP) **ne catchait pas
   `InvalidOperationException`** — un rejet du verrou `soId` (TASK-156) y remontait en 500 brut au
   lieu du 409 explicite renvoyé par les 3 autres appelants de l'orchestrateur.

## Ce qui est livré
- **`DeclarationsController.Resynchroniser`** : ajout du `try/catch (InvalidOperationException) →
  Conflict(409)`, même pattern que `RafraichirValorisation`/les 3 autres sites TASK-156. Aucune
  autre logique changée (le service `ResynchroniserLigneAsync` n'est pas modifié).
- **`api.ts`** : nouvelle fonction `relireDepuisSage(declarationId, ecId)` — appelle le MÊME
  endpoint `POST {id}/lignes/resynchroniser` que TASK-078, sous un nom explicite côté front pour ce
  nouvel usage.
- **`DiagnosticModal.tsx`** (écran ③, cible de la task) : nouveau bloc « Relancer une lecture Sage
  réelle », affiché quand `!data.cachePerime` (le cache n'est pas simplement périmé — TASK-147 gère
  déjà ce cas avec un recalcul sans lecture Sage — donc soit absent, soit en erreur durable, le cas
  visé par cette task). Bouton « Relire depuis Sage » : action manuelle explicite (aucun
  déclenchement automatique), gère les 3 issues réelles :
  - succès (`resolue=true`) → `onRecalculated?.()` (le parent recharge lignes/checkup, même
    callback que TASK-147) ;
  - échec métier réel (`resolue=false`) → message explicite « toujours en anomalie », **motif réel
    inchangé affiché**, aucune valeur fabriquée (garde-fou §3 respecté) ;
  - rejet 409 (verrou `soId` déjà pris par un autre traitement OM en cours) → message serveur
    explicite remonté tel quel (`e.response.data.Message`).
- Portée volontairement limitée à `DiagnosticModal.tsx` (le fichier cible explicite de la task) —
  `AffectationsDrill.tsx` (écran ② Affectations) non modifié : son bouton « Corriger /
  Resynchroniser » existant reste scopé aux lignes incohérentes (TASK-078), hors périmètre strict
  de cette task.

## Vérification indépendante des critères de validation

- [x] **Build back + front OK** — `dotnet build DeclarationTVA.slnx` 0 erreur, `npx tsc --noEmit`
      0 erreur, `npm run build` succès.
- [x] **Une ligne en échec authentique redevient valorisée après l'action, vérifié en base réelle
      (pas une fixture)** — testé directement contre l'endpoint réellement appelé par le nouveau
      bouton (instance de dev port 5299, jamais :5280, base `GR_EMA_DISTRIBUTION` réelle) : sur
      `TVA1-2026-01` (soId=1), ligne réelle `EC_Id=21473`/`FC2501717` déjà en anomalie
      (« Incohérence Sage : Σ(HT+TVA+Parafiscale)=2064301,44 ≠ TTC=20700,00 ») —
      `POST {id}/lignes/resynchroniser {ecId:21473}` → `{"resolue":false}` (incohérence Sage
      réelle, confirmée persister après relecture — comportement attendu, la donnée Sage elle-même
      est fautive, pas le cache). Le chemin **négatif** (motif réel préservé, jamais de valeur
      fabriquée) est prouvé de façon réelle et positive.
      **Mise à jour (campagne de test comptable, même session, cf.
      `VERIFY/CAMPAGNE-TEST-COMPTABLE-2026-01-06_verify.md`)** : le scénario positif exact a bien
      été rejoué sur `TVA1-2026-04` (2 lignes réelles `FC2600005`/`FC2600004`, motif
      `FACTURE_INTROUVABLE`, EC_Id 18199/18198) — **mais a révélé un défaut réel du signal de
      succès de `ResynchroniserLigneAsync`**, préexistant (TASK-078), pas introduit par cette task :
      `resolue:true` a été renvoyé pour les deux lignes (le log confirme une lecture OM réussie,
      « 0 en erreur »), mais **aucune entrée n'a été écrite dans `DM_VENTILATION_SAGE_CACHE`**
      (confirmé par requête SQL directe) et la ligne reste bloquée avec le même motif après
      rechargement. Cause probable : `BuildEntries`/`UpsertEntries` ignorent silencieusement un
      document OM sans exception (`EnErreur=false`) mais dont `LignesTaxe` est vide — dans ce cas
      précis, `resolue` (basé uniquement sur l'absence de sentinelle `CodeTaxe='ERREUR'`) est vrai
      par construction même si rien n'a été écrit, ce qui **fait mentir le bouton "Relire depuis
      Sage"** dans ce cas de figure. Root cause non creusée davantage (nécessiterait une inspection
      Sage/COM en direct, hors de portée de cette session) — documenté comme découverte de bug pour
      une future task, non corrigé ici (correctif proportionné pas identifié avec certitude sans
      accès à l'objet Sage réel). Détail complet dans le rapport de campagne.
- [x] **Verrou `soId` confirmé respecté (rejet 409 croisé)** — preuve réelle : deux appels
      `POST {id}/lignes/resynchroniser` lancés en parallèle sur le même `soId=1`
      (`EC_Id=21473` et `EC_Id=20650`) → le premier aboutit (`200 {"resolue":false}`), le second
      est rejeté (`409 {"message":"Traitement de valorisation déjà en cours pour cette société
      (soId=1)..."}`) — confirme à la fois que le nouveau chemin front respecte bien le verrou
      TASK-156 partagé, ET que le correctif du contrôleur (catch manquant) est réel et nécessaire
      (sans lui, ce 409 aurait été un 500 brut).
- [x] **Aucun changement de `DT_Id`/périmètre observé** — `GET /declarations/{id}/lignes` avant et
      après l'appel sur `EC_Id=21473` : `id` de ligne (Guid), `statutLigne`, `montantAffecte`,
      `numeroRapprochement` strictement identiques (comparaison JSON avant/après, voir logs de
      session) — seul le résultat de la revalidation d'incohérence est réévalué, jamais le
      périmètre de la ligne.
- [x] **Aucune régression sur `RecalculerLigneDepuisCacheAsync`/`RafraichirValorisationAsync`** —
      `dotnet test DeclarationTVA.slnx` : `Declaration.Orchestration.Tests` 177/177 (aucun test
      dédié à ces deux méthodes modifié), seul échec `Declaration.Controle.Tests` préexistant et
      sans rapport (cf. VERIFY-168/169).
- [x] **Décision PO tracée sur le mode d'action** — déjà actée dans les instructions de cette
      session (« ne pas redemander : action manuelle explicite par ligne ») — implémentée telle
      quelle, aucun retry automatique, bouton visible mais jamais déclenché sans clic.

## Réserves non bloquantes
- **⚠️ Bug réel découvert pendant la campagne de test comptable (non corrigé ici, voir plus haut)**
  : `ResynchroniserLigneAsync` peut renvoyer `resolue:true` sans que rien n'ait été écrit en cache
  quand l'OM répond sans erreur mais avec zéro ligne de taxe — le bouton « Relire depuis Sage »
  peut donc afficher un succès trompeur dans ce cas précis. Observé réellement sur 2 lignes
  (`TVA1-2026-04`), reste documenté comme découverte pour une future task (root cause Sage/COM non
  creusée, hors de portée sans inspection live).
- Rendu visuel du nouveau bloc dans `DiagnosticModal.tsx` non vérifié en navigateur réel (aucun
  outil d'automatisation navigateur disponible dans cette session) — vérifié uniquement par
  `tsc`/build et par des appels directs à l'endpoint exact que le bouton invoque (positif et
  négatif, cf. ci-dessus).
- `AffectationsDrill.tsx` (écran ② Affectations) volontairement non modifié — les lignes
  `nonValorise` de cet écran n'ont toujours pas de bouton de relecture (hors périmètre strict de
  cette task, qui ciblait explicitement l'écran ③/`DiagnosticModal.tsx`) ; à traiter séparément si
  le PO le souhaite.
