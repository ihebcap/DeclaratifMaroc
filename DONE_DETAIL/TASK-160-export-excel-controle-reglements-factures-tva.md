# TASK-160 — Export Excel de contrôle (règlements sélectionnés + factures à déclarer + détail TVA)

## Contexte
Demande PO (23/07/2026) : pouvoir exporter en Excel, **en cours de traitement** (avant clôture), la liste
des règlements sélectionnés (étape ① Sélection) et les factures qui seront déclarées avec le détail de
TVA — pour vérification, notamment partage externe (comptable, tiers) avant dépôt.

**Distinct de l'export existant (TASK-010/TASK-155)** : celui-ci ne fonctionne que sur une déclaration
`Clôturée` (`ConstruireModeleExportAsync` reconstruit la `DeclarationModele` depuis les lignes `Integree`
uniquement) et sert d'**artefact de dépôt légal** (archive figée, chemin disque déterministe). Ce que
demande le PO est un usage différent : un export **ad-hoc, réexécutable à tout moment avant clôture**, à
titre de contrôle — **ne remplace pas** les grilles interactives du front qui restent le moyen de
contrôle principal (décision actée TASK-010, §1sexies : "le moyen de contrôle avant dépôt est assuré par
les grilles, pas par l'Excel"). Ce nouvel export est un **complément**, pas une réouverture de cette
décision.

**Constat code (cartographie architecte)** :
- Les règlements sélectionnés existent déjà en base via `SaveSelectionReglementsAsync`/
  `GetSelectionReglementsAsync` (`IDeclarationRepository.cs:216-217`) — mais cette méthode ne retourne
  qu'une **liste de numéros** (`List<string>`), aucun détail (montant, date, tiers). Le détail existe
  côté `GetReglementsRapprochementAsync` (interrogation "Rapprochement bancaire", TASK-036) — c'est le
  même pattern que le front (`ReglementsSelection.tsx`) : récupérer la sélection, puis filtrer les lignes
  de rapprochement par ces numéros.
- Les factures à déclarer + détail TVA sont déjà valorisées **avant clôture** dès que le calcul a été
  lancé (lignes à l'état `Proposee`, cf. commentaire `DeclarationsController.cs:276-278` — "les lignes
  valorisées sont encore Proposee" tant que non clôturée) et déjà agrégées par `GetCheckupAsync`/
  `GetCheckup` (`recapSource`, `recapTaux`, `controleEquilibre`) sur l'ensemble `Integree||Proposee`
  (invariant TASK-108). **Aucune donnée manquante** — uniquement un nouvel export à assembler sur des
  lectures déjà exposées.
- `Declaration.Export.Excel.Exporter` (TASK-010, ClosedXML) sait déjà écrire un classeur multi-feuilles
  à partir d'un modèle en mémoire — réutilisable directement pour la feuille "Factures + TVA" (mêmes
  colonnes que la feuille Détail actuelle) et la feuille "Détail TVA" (mêmes agrégats que la feuille
  Récap actuelle, restreints aux totaux par taux/activité + contrôle d'équilibre).

## Périmètre STRICT
- **Nouveau** endpoint `GET /api/declarations/{id}/export-controle` : génère et retourne **à la volée**
  (flux, pas de fichier persisté sur disque comme `/generation`) un classeur `.xlsx` à **3 feuilles** :
  1. **Règlements sélectionnés** : numéro, date, montant, tiers, mode, état de pointage — jointure
     `GetSelectionReglementsAsync` (numéros) + `GetReglementsRapprochementAsync` (détail, filtré sur ces
     numéros).
  2. **Factures à déclarer** : mêmes colonnes que la feuille "Détail" existante (n° facture, désignation,
     tiers nom/IF/ICE, code activité, HT, taux, TVA, TTC, mode paiement, dates, source) — lignes
     `Integree||Proposee` de la déclaration (même ensemble que `GetCheckupAsync`, domaines Decaissement +
     Encaissement), pas seulement `Integree`.
  3. **Détail TVA** : totaux par taux et par code activité + contrôle d'équilibre (mêmes agrégats que le
     `checkup` actuel : `recapTaux`, `controleEquilibre`).
- Disponible dès que la déclaration existe (statut `EnCours` **ou** `Clôturée`) — **aucun blocage sur
  l'état** : si le calcul n'a pas encore été lancé, la feuille "Factures/TVA" est simplement vide (jamais
  une erreur, cohérent avec le principe déjà en place "aucune ligne silencieuse").
- **Exclu** : toute modification de l'export de dépôt existant (`/generation`, `/fichiers/{type}`,
  TASK-010/155) — endpoint et méthode d'assemblage strictement séparés, aucun fichier écrit sur disque
  pour ce nouvel export.
- **Exclu** : tout nouveau calcul métier — uniquement des lectures déjà exposées par
  `IDeclarationRepository`/`DeclarationWorkflowService`.
- **Exclu** : décision sur l'emplacement exact du bouton front (voir Étapes, à valider avec le PO avant
  le développement front).

## Positionnement / architecture
- `DeclarationWorkflowService` : nouvelle méthode `ConstruireModeleControleAsync(Guid declarationId)` —
  assemble un modèle dédié (règlements sélectionnés + `DeclarationModele`-like Lignes/RecapsParTaux/
  RecapsParActivite/ControleEquilibre) à partir des lectures déjà existantes. **Ne réutilise pas**
  `ConstruireModeleExportAsync` (celui-ci exige `Clôturée` et ne prend que les lignes `Integree`).
- `Declaration.Export.Excel.Exporter` : nouvelle méthode publique (ex. `ExporterExcelControle(modele,
  stream)`) écrivant dans un `Stream` (pas un chemin disque, cf. génération à la volée) — 3 feuilles.
  Aucune dépendance Sage/SQL ajoutée (même contrainte que TASK-010).
- `DeclarationsController` : nouvel endpoint `GET {id}/export-controle`, même garde d'autorisation
  société (`EstSocieteAutorisee`) que les autres endpoints de la déclaration.
- Front : nouveau bouton "Export de contrôle (Excel)" — **décision PO actée (23/07/2026) : les deux
  écrans** (① Sélection **et** ② Vérifier & Intégrer), pointant vers le même endpoint (le contenu du
  classeur est identique dans tous les cas). Reste à développer.

## Étapes
1. ~~Confirmer avec le PO l'emplacement du bouton front~~ — **TRANCHÉ (23/07/2026)** : PO a choisi
   **les deux écrans** (① Sélection **et** ② Vérifier & Intégrer), bouton dupliqué pointant vers le
   même endpoint `GET {id}/export-controle`, contenu identique dans les deux cas. Développement
   front désormais débloqué.
2. `DeclarationWorkflowService.ConstruireModeleControleAsync` : assembler règlements sélectionnés
   (jointure sélection + rapprochement) + lignes `Integree||Proposee` + agrégats taux/activité + contrôle
   d'équilibre.
3. `Declaration.Export.Excel.Exporter.ExporterExcelControle` : 3 feuilles, écriture dans un `Stream`.
4. `DeclarationsController.GET {id}/export-controle` : appelle le service, retourne le flux
   (`FileContentResult`, content-type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`).
5. Bouton front (selon décision étape 1) : appel `api.get(url, { responseType: 'blob' })` (même pattern
   que `DeclarationFinalePanel.tsx` pour l'export de dépôt).
6. **Tests** : génération depuis une déclaration `EnCours` avec règlements sélectionnés + lignes
   `Proposee` (sans clôture) — vérifier les 3 feuilles (nb lignes, valeurs) ; cas déclaration sans calcul
   lancé (feuille Factures/TVA vide, pas d'erreur) ; non-régression de l'export de dépôt existant
   (`/generation`, `/fichiers/{type}` inchangés).

## Livrables
- `Declaration.Export.Excel.Exporter.ExporterExcelControle(...)`.
- `DeclarationWorkflowService.ConstruireModeleControleAsync(...)`.
- Endpoint `GET /api/declarations/{id}/export-controle`.
- Bouton front (emplacement selon décision PO).
- Tests de génération (fixtures déclaration `EnCours`).
- `VERIFY/TASK-160_verify.md` : un `.xlsx` d'exemple (3 feuilles) + description.

## Critères de validation
- 3 feuilles conformes au périmètre ci-dessus, valeurs cohérentes avec la sélection/les lignes réelles.
- Fonctionne sur une déclaration `EnCours` (pas seulement `Clôturée`) — vérifié explicitement par un test.
- Aucun fichier écrit sur disque pour ce nouvel export (flux direct).
- Aucune régression sur l'export de dépôt existant (`/generation`/`/fichiers/{type}`, TASK-010/155).
- Aucun bypass de l'autorisation société (`EstSocieteAutorisee`).

## Risques / dépendances
- **Non bloquant** : réutilise intégralement l'infrastructure existante (ClosedXML déjà en place,
  lectures repository déjà exposées) — aucune nouvelle dépendance externe.
- ~~Dépendance de cadrage~~ — **levée (23/07/2026)** : PO a tranché, bouton sur les deux écrans (① et
  ②), même endpoint. Développement front à faire (étape 5).
- **Point d'attention VERIFY** : bien vérifier que la feuille "Règlements sélectionnés" ne fuite pas de
  règlement hors du périmètre société de l'utilisateur (même garde que le reste de l'API) et que le
  détail TVA reste calculé sur le même ensemble `Integree||Proposee` que le `checkup` (pas de double
  définition divergente de "les lignes qui comptent", cf. l'historique des écarts TASK-103/108/112).
