# TASK-190 — Backfill rétroactif `DateFacture`/`Reference` sur les lignes `DM_LGTVA` déjà figées (déclarations EnCours)

## Contexte
Demande PO explicite (27/07/2026), suite aux TASK-186/187/189 : les 6 déclarations réelles actuellement
en base (`TVA1-2026-01` à `06`, **toutes `Statut=EnCours`**, vérifié) portent au total **5148 lignes
`DM_LGTVA`** figées **avant** les correctifs d'aujourd'hui. Ces lignes gardent gravées les valeurs
fausses/vides d'origine :
- `DateFacture` = `RT_AFFECTATION.AF_Date` (bug TASK-186), au lieu de la vraie `RT_ECHEANCE.DO_Date`.
- `Reference` = toujours `NULL` (n'existait pas avant TASK-189).

Le code est corrigé pour toute **nouvelle** ligne figée à partir de maintenant (TASK-186/189), mais
**aucune ligne déjà persistée n'est retouchée automatiquement** — confirmé par lecture code : ni le
chargement (`ChargerCandidatesSiNecessaireAsync`), ni la revalidation (`RevaliderLignesFigeesAsync`),
ni la resynchronisation (`ResynchroniserLigneAsync`, qui ne touche que HT/Taux/TVA/TTC via l'OM Sage)
ne relisent/écrivent `DateFacture`/`Reference` sur une ligne déjà en base.

**Décision PO actée (27/07/2026)** : le backfill porte **uniquement sur les déclarations `EnCours`**
— jamais sur une déclaration déjà `Clôturée`/`Déposée` (cohérent avec la doctrine déjà appliquée sur ce
projet, cf. TASK-094 : correction rétroactive de données seulement sur demande PO explicite et
distincte, jamais sur de l'historique déjà transmis à la DGI). Dans l'état réel actuel de la base, les
6 déclarations existantes sont toutes `EnCours` — ce backfill couvrira donc l'intégralité du parc
existant à ce jour, mais le code doit filtrer sur le statut, pas sur la liste figée des 6 déclarations
actuelles.

## Objectif
```
Pour chaque ligne DM_LGTVA appartenant à une déclaration EnCours et portant un EC_Id valide (>0) :
  1. Lire RT_ECHEANCE.DO_Date / DO_Reference pour cet EC_Id (connexion GRF)
  2. Si DateFacture ou Reference diffère de la valeur actuellement persistée → UPDATE ciblé
     (uniquement ces 2 colonnes, jamais HT/Taux/TVA/TTC/Etat/MotifRejet/CodeActivite)
  3. Si l'EC_Id n'existe plus dans RT_ECHEANCE (facture supprimée entre-temps) → ne rien écraser,
     signaler explicitement dans le rapport (jamais un silence)
Sortie : rapport (nb lignes scannées / nb mises à jour / nb déjà correctes / nb EC_Id introuvables)
```

## Périmètre STRICT
- **Inclus** :
  1. Une méthode dédiée (`DeclarationWorkflowService.BackfillDateFactureEtReferenceAsync`, ou nom
     équivalent) : récupère les déclarations `Statut == EnCours` (`GetToutesDeclarationsAsync`,
     TASK-094, déjà existante — filtrer côté C#), pour chacune lit ses lignes (`GetLignesAsync`, les
     deux domaines), regroupe les `EC_Id` distincts valides (`> 0`) **toutes déclarations confondues**
     (un `EC_Id` peut apparaître dans plusieurs lignes/déclarations — un seul aller-retour GRF par
     `EC_Id` distinct, même principe que `EnrichirTiersDepuisSageAsync`/`GetDernieresDatesRapprochementAsync`
     TASK-135 : lecture batchée, jamais un aller-retour par ligne).
  2. Nouvelle méthode repository côté GRF (lecture seule) : `GetDatesFacturesEtReferencesAsync(soId,
     ecIds)` → `Dictionary<int, (DateTime? DoDate, string? DoReference)>`, `SELECT EC_Id, DO_Date,
     DO_Reference FROM RT_ECHEANCE WHERE SO_Id = @so AND EC_Id IN @ecIds`.
  3. Nouvelle méthode repository côté persistance (écriture ciblée) :
     `MettreAJourDateFactureEtReferenceAsync(ligneId, dateFacture, reference)` ou variante batchée —
     `UPDATE DM_LGTVA SET DateFacture = @DateFacture, Reference = @Reference WHERE Id = @Id`. **Aucune
     autre colonne dans le `SET`.**
  4. **Garde-fou obligatoire** : ne réécrire que les lignes dont au moins une des 2 valeurs diffère
     réellement de la valeur actuelle (évite un `UPDATE` inutile sur les lignes déjà correctes —
     rend l'opération idempotente et son rapport significatif si rejouée).
  5. Exposition : endpoint admin (`UT_Admin=1`, même garde que TASK-094/073/079) déclenché
     manuellement — **jamais automatique au chargement d'une déclaration** (une opération d'écriture
     de masse doit rester un geste explicite, pas un effet de bord silencieux d'un `GET`).
  6. Rapport détaillé retourné (et si possible loggé) : nb lignes scannées, nb mises à jour, nb déjà
     correctes, nb `EC_Id` introuvables dans `RT_ECHEANCE` (avec la liste de ces `EC_Id` pour
     investigation).
- **Exclu** : toute déclaration `Clôturée`/`Déposée` (filtrée explicitement, jamais par omission) ;
  tout recalcul financier (HT/Taux/TVA/TTC) — hors sujet, ces valeurs ne sont pas concernées par
  TASK-186/187/189 ; toute modification du mécanisme de figeage lui-même (déjà corrigé, non touché).

## Étapes
1. Repository : `GetDatesFacturesEtReferencesAsync` (GRF, lecture seule, batchée par `EC_Id`).
2. Repository : `MettreAJourDateFactureEtReferenceAsync` (persistance, écriture ciblée 2 colonnes).
3. Service : `BackfillDateFactureEtReferenceAsync` — orchestration (filtre `EnCours`, collecte
   `EC_Id` distincts, lecture batchée, comparaison, écriture ciblée, rapport).
4. Endpoint admin (`DeclarationsController` ou contrôleur dédié), gardé `UT_Admin=1`.
5. Tests unitaires (repository fake) : ligne déjà correcte → pas d'`UPDATE` ; ligne à corriger →
   `UPDATE` avec les bonnes valeurs ; `EC_Id` introuvable → signalé, aucune exception ; déclaration
   `Clôturée`/`Déposée` → jamais incluse dans le périmètre scanné.
6. **Dry-run obligatoire avant écriture réelle** : le worker doit d'abord exécuter en mode
   lecture-seule (ou équivalent : requêtes `SELECT` de comparaison) sur `GR_EMA_DISTRIBUTION` pour
   produire un compte AVANT/APRÈS attendu, **avant** de lancer l'écriture réelle — même prudence
   qu'une migration de données, documentée dans le VERIFY.
7. Exécution réelle sur `GR_EMA_DISTRIBUTION` (les 6 déclarations `EnCours` actuelles, ~5148 lignes) +
   preuve : au moins `FC2501193` (`EC_Id=21466`, dans `TVA1-2026-01`) confirmé passer de
   `DateFacture=2026-04-15`/`Reference=NULL` à `DateFacture=2025-07-18`/`Reference=NULL` (référence
   vide confirmée pour CE cas précis dès TASK-187, donc `NULL` attendu est correct, pas un échec).

## Livrables
- Code (repository + service + endpoint admin) + tests.
- Rapport réel d'exécution sur `GR_EMA_DISTRIBUTION` (nb scannées/mises à jour/déjà correctes/introuvables).
- Preuve ciblée sur `FC2501193` (et idéalement 2-3 autres cas de l'échantillon déjà utilisé par
  TASK-186/187, pour rester cohérent avec les cas déjà cités au PO).

## Critères de validation
- Toutes les lignes `DM_LGTVA` des déclarations `EnCours` reflètent la vraie `DO_Date`/`DO_Reference`
  après exécution (sauf `EC_Id` introuvable, explicitement signalé).
- Aucune déclaration `Clôturée`/`Déposée` touchée (vérifié explicitement dans le rapport : 0 ligne de
  ce statut dans le périmètre scanné).
- Aucune colonne autre que `DateFacture`/`Reference` modifiée.
- Ré-exécution immédiate après un premier passage réussi → 0 ligne mise à jour (idempotence).
- Build 0 erreur, tests verts.

## Risques / dépendances
- Dépend de TASK-186 (colonne `DateFacture` corrigée) et TASK-189 (colonne `Reference` existante).
- Opération d'**écriture de masse** sur une table de persistance réelle (~5148 lignes concernées) —
  à traiter avec la même prudence qu'une migration de données : dry-run avant écriture, rapport
  complet, jamais un `UPDATE` sans clause de comparaison préalable.
- Si une déclaration passe de `EnCours` à `Clôturée` entre le dry-run et l'exécution réelle (fenêtre de
  course théorique) : le filtre `Statut == EnCours` doit être réévalué au moment de l'écriture, pas
  seulement au moment du dry-run — à couvrir explicitement par un test si réalisable simplement, sinon
  documenter le risque résiduel dans le VERIFY.
