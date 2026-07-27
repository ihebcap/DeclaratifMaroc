Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md` (section
« ⚠️ Suivi opérationnel (27/07/2026, après-coup) », en tête de fichier), les VERIFY de TASK-186/189
(`DONE_DETAIL/TASK-186_verify.md`, `DONE_DETAIL/TASK-189_verify.md`), puis le fichier TASK ci-dessous
en entier. Ne suppose jamais un contexte manquant.

## Contexte

Les 4 correctifs d'aujourd'hui (TASK-186/187/188/189) sont corrects et testés, mais ils ne s'appliquent
qu'aux **nouvelles** lignes figées à partir de maintenant. Les 6 déclarations réelles déjà en base
(`TVA1-2026-01` à `06`, **toutes `Statut=EnCours`**, ~5148 lignes `DM_LGTVA` au total) gardent gravées
les valeurs fausses/vides d'origine : `DateFacture` = ancien bug (`AF_Date` au lieu de `DO_Date`),
`Reference` = toujours `NULL` (colonne inexistante avant TASK-189). Le PO a explicitement demandé un
**backfill rétroactif**, limité aux déclarations `EnCours` (jamais Clôturée/Déposée — décision PO
27/07/2026, cohérente avec la doctrine déjà appliquée sur ce projet, cf. TASK-094).

**⚠️ Cette TASK est différente des précédentes : c'est une écriture de masse sur des données réelles**
(~5148 lignes potentiellement concernées), pas une simple extension de projection en lecture seule. Sois
particulièrement prudent : dry-run avant toute écriture réelle, rapport complet, jamais un `UPDATE`
sans comparaison préalable ligne par ligne.

## Ta mission

Traiter **TASK-190**
(`D:\_vibe\GRF\TASKS\TASK-190-backfill-datefacture-reference-lignes-encours-existantes.md`).

Vérifie d'abord que les champs Objectif/Périmètre/Livrables/Critères de validation sont bien remplis
(ils le sont). Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les
erreurs, puis écris `VERIFY/TASK-190_verify.md` en suivant le même niveau de détail que
`DONE_DETAIL/TASK-189_verify.md` (sers-t'en de modèle) : section Périmètre livré, Fichiers modifiés,
Checklist, et une section « Reste à valider » honnête si tout n'a pas pu être vérifié.

## Règles de travail (non négociables)

- **Portée exacte** (§Périmètre STRICT de la TASK) :
  1. `GetDatesFacturesEtReferencesAsync(soId, ecIds)` (nouvelle méthode repository, connexion GRF,
     **lecture seule**, batchée par `EC_Id` — jamais un aller-retour par ligne) : `SELECT EC_Id,
     DO_Date, DO_Reference FROM RT_ECHEANCE WHERE SO_Id = @so AND EC_Id IN @ecIds`.
  2. `MettreAJourDateFactureEtReferenceAsync(...)` (nouvelle méthode repository, connexion
     persistance, **écriture ciblée uniquement sur `DateFacture`/`Reference`** — jamais une autre
     colonne dans le `SET`).
  3. `DeclarationWorkflowService.BackfillDateFactureEtReferenceAsync` : filtre les déclarations
     `Statut == EnCours` (`GetToutesDeclarationsAsync`, déjà existante depuis TASK-094), collecte les
     `EC_Id` distincts valides (`> 0`) de leurs lignes, lit en un seul batch via GRF, compare, réécrit
     **uniquement** les lignes dont `DateFacture` ou `Reference` diffère réellement de la valeur
     actuelle (garde-fou d'idempotence — ne jamais réécrire une ligne déjà correcte).
  4. Endpoint admin (`UT_Admin=1`, même garde que TASK-094/073/079) — déclenché manuellement, **jamais
     automatique** au chargement d'une déclaration.
  5. Rapport retourné : nb lignes scannées / nb mises à jour / nb déjà correctes / nb `EC_Id`
     introuvables dans `RT_ECHEANCE` (avec la liste de ces `EC_Id`, jamais un silence).
- Ne touche à aucune autre colonne, aucune déclaration `Clôturée`/`Déposée` (filtre explicite sur
  `Statut`, jamais par omission), aucun recalcul financier (HT/Taux/TVA/TTC hors sujet).
- **Dry-run obligatoire AVANT toute écriture réelle** : produis d'abord un rapport de comparaison
  (combien de lignes seraient modifiées, avec un échantillon des valeurs avant/après) sans exécuter le
  moindre `UPDATE`, documente ce rapport dans le VERIFY, **puis seulement** lance l'écriture réelle.
- **Tu as un accès réel à la base de données** : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d
  GR_EMA_DISTRIBUTION -Q "..."` (identifiants complets dans `D:\_vibe\GRF\connections.json`). Utilise-le
  pour :
  - Confirmer AVANT exécution que les 6 déclarations réelles sont bien `EnCours` (revérifie, ne
    suppose pas que ça reste vrai au moment où tu lances la TASK).
  - Après exécution : confirmer sur `FC2501193` (`EC_Id=21466`, `TVA1-2026-01`) que `DateFacture` passe
    à `2025-07-18` et que `Reference` reste `NULL` (attendu — `DO_Reference` est vide pour ce cas
    précis, déjà confirmé par TASK-187/189 ; ce n'est pas un échec du backfill).
  - Vérifier qu'aucune ligne d'une déclaration `Clôturée`/`Déposée` n'a été touchée (à ce jour aucune
    des 6 ne l'est, mais vérifie explicitement dans ton rapport que le filtre a bien fonctionné, pas
    juste que le résultat est cohérent par coïncidence).
  - **Rejouer l'opération une seconde fois** après le premier passage réussi : le rapport doit annoncer
    0 ligne mise à jour (preuve d'idempotence).
- Build back (`dotnet build DeclarationTVA.slnx` — devrait maintenant fonctionner sans blocage,
  `Declaration.API.exe` n'est plus verrouillant au moment de la rédaction de ce prompt, mais revérifie)
  ET tests (`Declaration.Core.Tests`, `Declaration.Orchestration.Tests`, `Declaration.Selection.Tests`)
  doivent passer avant d'écrire le VERIFY.
- Un seul commit pour TASK-190, message clair, jamais `--no-verify`, **ne jamais pousser (`git
  push`)** — le commit reste local pour revue architecte.
- Respecte les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors couche repository,
  pas de secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse).
- Si un point est réellement ambigu (notamment un `EC_Id` introuvable dans `RT_ECHEANCE`, ou une
  déclaration qui changerait de statut entre le dry-run et l'exécution réelle) : **arrête-toi sur ce
  point précis, documente-le clairement dans le VERIFY** plutôt que d'inventer une réponse.

## À la fin

Laisse TASK-190 dans `VERIFY/` (ne la déplace pas toi-même vers `DONE_DETAIL/`). Termine par un résumé
clair incluant explicitement : nombre réel de lignes mises à jour sur `GR_EMA_DISTRIBUTION`, preuve
d'idempotence (2ᵉ passage = 0 ligne), et confirmation qu'aucune déclaration Clôturée/Déposée n'a été
touchée.
