# VERIFY — TASK-145

Date: 2026-07-20
Agent: worker nocturne (Claude Code)

## BUILD

- Status: OK
- Erreurs: aucune
- `dotnet build Declaration.Selection/Declaration.Selection.csproj` → 0 erreur, 9 avertissements
  préexistants (nullabilité, non liés à ce correctif).
- `dotnet build Declaration.API/Declaration.API.csproj` → 0 erreur, 1 avertissement préexistant
  (`using` dupliqué `Program.cs`, non lié).

## FICHIERS MODIFIÉS

- `Declaration.Selection/GrfEnums.cs` — ajout constante `ErpDomaine_Achat = 1` (enum Sage
  `ErpDomaine`, distincte de `MV_Domaine`) + commentaires explicites distinguant les deux notions
  de "domaine" (point 3 de l'Objectif TASK-145).
- `Declaration.Selection/SelectionExpliqueeService.cs` — `GetFactureFirstSql` : ajout
  `AND E.DO_Domaine = @achatDomaine` au `WHERE` ; paramètre `achatDomaine` ajouté à l'objet `param`
  de `LireFacturesDepuisPeriodeAsync`. Aucun changement à `MapAndEvaluate`/`Sens=Achat` (le
  filtre amont garantit maintenant que seules des échéances d'achat atteignent ce point — cf.
  garde-fou "ne pas dupliquer `SensAffectation`").
  > Note : `git diff` sur ce fichier montre aussi des blocs TASK-099 (périmètre `DT_Id IS NULL`,
  > suppression de la borne basse) — modifications **déjà présentes dans l'arbre de travail avant
  > le début de cette session** (travail antérieur non commité), pas introduites par TASK-145. Non
  > touchées par ce correctif.

## DIFF RÉSUMÉ

`GetFactureFirstSql` (lecture facture-first, achat/dépense uniquement) filtrait déjà par
`SO_Id`/`EC_Type`/`DO_Date` mais jamais par `DO_Domaine` — une échéance de VENTE (`DO_Domaine=0`)
avec `EC_Type=0` pouvait donc être lue ici et évaluée avec `Sens=Achat` codé en dur (ligne 263,
inchangée), provoquant un appel Sage `docFactoryAchat.ExistPiece` sur une pièce de vente → erreur
"Facture d'achat introuvable" alors que la pièce existe réellement côté vente. Ajout du filtre
`DO_Domaine = 1` (Achat) au `WHERE` de la requête. Aucun changement à la logique d'évaluation, à
`SageTaxReaderService.cs`, ni au chemin encaissement/vente existant (`SelectionnerExpliqueeAsync`).

## VALIDATION CHECKLIST

- [x] Build back OK.
- [x] Rejeu réel (requête SQL directe, avant purge) : confirmé que
      `EC_Id` 21069 (`FA2600559`), 21070 (`FA2600564`), 21071 (`FA2600582`), 21072 (`FR2600077`),
      21073 (`FA2600600`), 21074 (`FA2600619`) portent tous `DO_Domaine=0` (vente) — ces 6 lignes
      sont désormais **hors périmètre** de `GetFactureFirstSql` après le fix (elles ne seront plus
      lues du tout par cette requête achat-only, donc plus jamais en erreur "introuvable" par ce
      chemin). Confirmé par relecture du filtre SQL modifié + requête `RT_ECHEANCE` directe.
- [x] Confirmé par requête SQL que la portée de l'ancien bug était bien plus large que les 6 `EC_Id`
      cités en session : **2 312 échéances** `RT_ECHEANCE` (`SO_Id=1`, `EC_Type IN (0,4,111)`,
      `DO_Domaine <> 1`) auraient été lues à tort par `GetFactureFirstSql` avant ce correctif, sur
      l'ensemble de la base (pas seulement la période observée) — cf. risque signalé en fin de TASK.
- [x] **Purge de cache exécutée et documentée.** Diagnostic (lecture seule) :
      `DM_VENTILATION_SAGE_CACHE` croisé avec `RT_ECHEANCE.DO_Domaine <> 1` et `MotifErreur IS NOT NULL`
      → **331 lignes** contaminées trouvées. Sur ces 331 :
      - **326 lignes** portaient le motif exact `Facture d'achat ... introuvable.` — signature
        directe du bug de cette TASK (Sens=Achat forcé sur une échéance de vente) — **purgées** par
        un `DELETE` ciblé (jointure `RT_ECHEANCE.DO_Domaine <> 1` + motif `LIKE 'Facture d''achat%introuvable%'`,
        jamais de `TRUNCATE`/reset global). Liste complète des `EC_Id`/`DO_Numero`/`DateLecture`
        purgés exportée dans `DONE_DETAIL/task145-purge-liste-ec-id.txt` (328 lignes fichier =
        1 en-tête + 1 séparateur + 326 lignes de données).
      - **5 lignes** portaient un motif différent (`Incohérence TTC : Sage=... ≠ RT_ECHEANCE.EC_MtDevise=...`
        ou `Incohérence Sage : ...`) sur des `EC_Id` 19992, 19993, 23596, 24098 (`FA2502718`), 24113
        — ces erreurs proviennent d'une **validation de cohérence de montants distincte** (la pièce a
        bien été trouvée côté Sage, mais les montants ne concordent pas), pas du bug Achat/Vente de
        cette TASK. **Non purgées délibérément** — hors périmètre de ce correctif (le garde-fou de la
        TASK exige une purge ciblée EXACTEMENT sur les entrées liées à ce bug, pas un nettoyage
        généralisé de toutes les erreurs sur `DO_Domaine<>1`). `EC_Id=24098`/`FA2502718` reste donc
        avec cette erreur d'incohérence de montants après cette TASK — c'est un point ouvert distinct,
        voir "Reste à valider" ci-dessous.
      - Vérification post-purge : la requête de diagnostic ne remonte plus aucune ligne
        `Facture d'achat ... introuvable` sur `DO_Domaine <> 1` (0 ligne).
- [~] **Preuve positive de la branche Vente réelle — PARTIELLEMENT validée, voir "Reste à valider".**
      `EC_Id=21849` (`FA2600106`, `TVA1-2026-02`) : cache déjà en état `MotifErreur IS NULL`,
      `Source='OM'`, montants non nuls (`TotalHT=69974,23`, `TotalTva=9232,56`), lecture datée
      2026-07-19 23:17 — **antérieure à cette session**, donc pas une preuve fraîche produite par
      cette TASK, mais confirme qu'une lecture Vente a déjà réussi historiquement pour ce cas exact
      (cohérent avec le diagnostic TASK-144). `EC_Id=24098` (`FA2502718`, `TVA1-2026-03`) : reste en
      erreur, mais d'un type "Incohérence Sage" (montants), pas "introuvable" — non concerné par le
      fix Sens.
- [x] Aucune duplication de la logique `SensAffectation` — `MapAndEvaluate(..., SensAffectation.Achat, null)`
      inchangé (ligne 263), le filtre SQL amont suffit à garantir la cohérence.

## RESTE À VALIDER (honnête, non silencieux)

1. **Relecture fraîche via le vrai worker Sage COM non obtenue dans cet environnement.**
   Tentative d'invocation directe du worker (`SageTaxReader.Console.exe`, build x86/net48 réussi)
   en mode single-piece pour `FA2600106 Vente` **et** pour une pièce achat connue (`FC2600001 Achat`,
   sanity check) : les deux appels échouent avec un crash natif identique
   (`STATUS_STACK_BUFFER_OVERRUN`, code sortie `-1073740791`) dès l'instanciation du COM Sage
   (`SageTaxReaderService`), **avant même** d'atteindre la logique `Sens`. Comme le crash est
   identique en mode Achat et Vente sur des pièces réelles connues, il ne s'agit **pas** d'une
   preuve que la branche Vente spécifiquement est cassée — c'est une incapacité de cette session à
   invoquer le worker COM en standalone (probablement absence d'appartement STA explicite sur
   `Main`, ou contrainte d'environnement d'automation COM hors du pipeline normal
   API→`WorkerInvoker`→process). **Ne pas interpréter ce point comme une validation positive de la
   branche Vente** — c'est un blocage distinct, à état "non prouvé", ni confirmé cassé ni confirmé
   fonctionnel par un appel frais dans cette session. La seule preuve disponible reste la lecture
   historique déjà en cache pour `EC_Id=21849` (antérieure à cette session, voir ci-dessus).
2. **`EC_Id=24098`/`FA2502718` reste en erreur** (motif "Incohérence Sage", montants). Ce n'est pas
   le bug de cette TASK, mais reste une ligne non résolue de la déclaration `TVA1-2026-03` —
   pertinent pour TASK-149 (ré-audit).
3. Le fix n'a été testé qu'au niveau SQL (requêtes directes contre `GR_EMA_DISTRIBUTION`), pas en
   relançant réellement `RafraichirValorisationAsync` via l'API (nécessiterait de démarrer l'API +
   authentification JWT + le worker COM, bloqué par le point 1 ci-dessus). La correction du filtre
   `WHERE` étant purement déclarative (un `AND` supplémentaire sur une colonne déjà présente dans le
   `SELECT`), le risque résiduel de ce point est jugé faible, mais non prouvé de bout en bout.

## IMPACTS DÉTECTÉS

- Portée réelle du bug plus large que signalé en session : 2 312 échéances vente potentiellement
  concernées sur l'ensemble de la base (pas seulement les 6 `EC_Id` de la période observée), et 326
  sentinelles `ERREUR` déjà écrites en cache à tort avant ce correctif — désormais purgées, ces
  échéances seront relues au prochain passage de `RafraichirValorisationAsync`/chargement de
  candidates via le chemin encaissement (`SelectionnerExpliqueeAsync`), pas par cette requête achat.
- TASK-149 (ré-audit des 14 lignes `FACTURE_NON_VENTILEE`) peut maintenant être exécutée — dépendance
  levée côté code, mais voir point 1 "Reste à valider" pour la limite de preuve worker réelle.

## NOTES

- Constante `ErpDomaine_Achat = 1` ajoutée à `GrfEnums.cs` plutôt qu'un magic number `1` dans le SQL,
  conformément au point 3 de l'Objectif.
- Aucune modification de `SageTaxReaderService.cs` ni du chemin encaissement — conforme aux
  garde-fous.
- La purge cache a été strictement bornée au motif exact produit par ce bug (`LIKE 'Facture d''achat%introuvable%'`),
  volontairement plus étroite que le filtre générique `MotifErreur IS NOT NULL` suggéré par la TASK,
  pour éviter de purger des erreurs métier réelles et distinctes (incohérence de montants) —
  décision documentée ici, pas silencieuse.
