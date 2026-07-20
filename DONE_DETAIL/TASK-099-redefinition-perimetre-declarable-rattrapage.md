# TASK-099 — Redéfinir le périmètre déclarable de l'écran ① (rattrapage, pas de fenêtre mensuelle stricte)

Status: TODO
Priority: HIGH
Risk: CRITICAL
Module: Declaration.Selection / Declaration.Infrastructure (RegleDatePeriode, requêtes de sélection/rapprochement)

## OBJECTIF
Remplacer la fenêtre mensuelle stricte (`DateReference >= debut AND < finExclude`) utilisée
aujourd'hui pour déterminer quels règlements appartiennent à la période d'une déclaration, par la
règle métier suivante (confirmée PO 14/07/2026) :

```
Un règlement (espèce OU hors-espèce) est proposé à la sélection de l'écran ① si et seulement si :
  1. DT_Id IS NULL                          (non encore déclaré, quelle que soit la déclaration)
  2. DateReference < fin de période         (<= 30/06/2026 pour une déclaration de juin — PAS de
                                              borne basse : rattrape tout l'arriéré déclarable
                                              jamais déclaré, pas seulement ce qui tombe dans le mois)
  3. EstDeclarable(mvType, mvPoint)          (espèce, auto-déclarable — OU rapproché MV_Point=1 ;
                                              un hors-espèce NON rapproché n'est ni affiché ni
                                              sélectionnable dans cet écran — condition inapplicable
                                              à l'espèce, toujours considérée déclarable)

DateReference reste dérivée comme aujourd'hui (RegleDatePeriode.DateReferencePour) :
  espèce            → MV_Date
  hors-espèce rapproché → MV_PointDate
  (hors-espèce non rapproché : exclu par la règle 3, n'a plus besoin de DateReference)
```

## BUSINESS VALUE
Avec la fenêtre stricte actuelle, un règlement rapproché (ou payé en espèce) un mois donné mais
jamais inclus dans la déclaration de ce mois (oubli, déclaration déjà clôturée, etc.) devient
**définitivement impossible à déclarer** — la fenêtre du mois suivant ne le recouvre jamais. La
nouvelle règle transforme la période en simple **date de coupure** (« tout ce qui est déclarable et
pas encore déclaré jusqu'à cette date ») : aucun règlement déclarable ne peut plus se perdre entre
deux mois. Corrige aussi le signalement PO sur `RF26060125` (affiché comme « Éligible » alors que
non rapproché) — avec cette règle, un tel règlement n'apparaît simplement plus dans l'écran ①.

## CONTRAINTES
- `RegleDatePeriode` (`RegleDatePeriode.cs`) est la **source unique** documentée (TASK-062),
  répliquée à l'identique entre la dérivation C# (`DateReferencePour`) et le fragment SQL
  (`DateReferenceSqlM`), avec un test anti-divergence dédié (`Task062DateReferenceTests.cs`) — à
  maintenir strictement en phase, ne pas introduire une 2ᵉ logique de date parallèle.
- **Périmètre à confirmer avec le PO avant implémentation** (hypothèse assumée ci-dessous, sur le
  modèle « premier jet assumé » de TASK-021) : cette nouvelle règle (masquage des non-rapprochés,
  borne basse supprimée) s'applique-t-elle uniquement à l'écran ① Sélection (panier de la
  déclaration en cours, `GET /api/rapprochement` scopé période — TASK-036/039/040/054) ou
  également à l'écran « Interrogation Rapprochement » (TASK-037, exploration libre indépendante de
  toute déclaration, qui affiche aujourd'hui volontairement tout, y compris les non-rapprochés) ?
  **Tranché PO (14/07/2026) : uniquement l'écran ①.** L'Interrogation Rapprochement (TASK-037)
  reste une vue libre inchangée, jamais gated — confirmation explicite, plus une hypothèse.
- Ne pas modifier le comportement de `EstDeclarable`/`EstDeclarableSqlM` eux-mêmes (déjà corrects,
  espèce OU rapproché) — seule la fenêtre de date et l'ajout du gate de visibilité sur l'écran ①
  changent.
- Aucune régression sur les tests existants de date/période (`Task062DateReferenceTests.cs`,
  TASK-021 reportées, TASK-008/015/017).
- Dépendance directe avec TASK-097 (scope figeage à la sélection) : TASK-097 doit s'appuyer sur le
  périmètre déclarable **corrigé** par cette task, pas sur l'ancien. Séquencer TASK-099 → TASK-097.
- Impact sur TASK-021 (`Reportee`) : les non-rapprochés hors-espèce ne remontent plus du tout dans
  le jeu de candidates de l'écran ① avec cette nouvelle règle — vérifier si le bucket `Reportee`
  (stock à part, TASK-021) doit être conservé ailleurs (traçabilité) ou peut être supprimé du
  périmètre écran ① sans perte d'information (l'Interrogation Rapprochement, si hors périmètre,
  resterait la source de visibilité sur les non-rapprochés).

## FILES
- Declaration.Selection/RegleDatePeriode.cs (`DateReferencePour`, `DateReferenceSqlM`, gate de
  borne basse à retirer)
- Declaration.Selection/SelectionnerAffectationsService.cs (4 requêtes : Décaissement Fournisseur,
  Espèce Fournisseur, Dépense, Encaissement Client — toutes à aligner sur la nouvelle fenêtre)
- Declaration.Selection/SelectionExpliqueeService.cs (miroir, mêmes requêtes côté explication)
- Declaration.Selection/SelectionExpliqueeEvaluator.cs (le gate `EstDeclarable` déjà correct — à
  vérifier qu'il n'a pas de logique de fenêtre dupliquée)
- Declaration.Infrastructure/Repositories/DeclarationRepository.cs (`RapprochementFromWhere` —
  distinguer clairement la requête générale TASK-037 de celle consommée par l'écran ①, si le
  périmètre reste limité à ①)
- declaration-tva-web/src/ReglementsSelection.tsx (si un nouveau paramètre/DTO distingue les deux
  usages de l'endpoint `/rapprochement`)
- Declaration.Orchestration.Tests/Task062DateReferenceTests.cs (tests à étendre, pas remplacer)

## VALIDATION
- [ ] Build OK
- [ ] Tests passés (dont `Task062DateReferenceTests.cs` étendu)
- [ ] Règlement hors-espèce rapproché en avril, `DT_Id IS NULL`, jamais déclaré → apparaît dans
      l'écran ① d'une déclaration de juin 2026 (preuve réelle sur base).
- [ ] Règlement hors-espèce NON rapproché (peu importe la date) → n'apparaît PLUS dans l'écran ①
      (ex. `RF26060125` si non rapproché sur `GR_EMA_DISTRIBUTION`).
- [ ] Règlement espèce payé en mai, jamais déclaré → apparaît dans une déclaration de juin (borne
      basse bien supprimée pour l'espèce aussi).
- [ ] Règlement déjà déclaré (`DT_Id` posé) → n'apparaît plus, quelle que soit sa date.
- [ ] Aucune régression sur l'Interrogation Rapprochement (TASK-037) si le périmètre reste limité à
      l'écran ① (hypothèse par défaut ci-dessus) — confirmé avec le PO avant de coder.

## ARCHITECTURE RULES APPLICABLES
- Pas de duplication de logique existante — une seule source de la règle de date/déclarabilité
  (`RegleDatePeriode`), pas de réimplémentation locale par écran.
- Ne jamais improviser un contexte manquant : le périmètre Interrogation Rapprochement (TASK-037)
  reste une question ouverte à trancher par le PO avant le début de l'implémentation, pas une
  hypothèse silencieuse.

## NOTES
Découvert en répondant à un signalement PO sur `RF26060125` (règlement affiché « Éligible » à
l'écran ① alors que non rapproché, 14/07/2026). Confirmé et précisé par le PO : suppression de la
borne basse de période pour tous les modes de paiement (espèce inclus), la notion de « non
rapproché » restant inapplicable à l'espèce. Séquencer avant TASK-097 (le scope de sélection de
TASK-097 doit porter sur le bon périmètre de candidats).

**Décision PO complémentaire (14/07/2026) — notion de « report » différée** : ne pas cocher un
règlement à l'écran ① suffit à ne pas le déclarer ; il redevient naturellement sélectionnable à la
période suivante via `DT_Id IS NULL` (aucune borne basse). Pas besoin d'un statut/mécanisme
« Reportee » dédié pour l'instant — le bucket `Reportee` de TASK-021 n'est **pas** à généraliser
dans le cadre de cette task ; sujet rouvert plus tard si besoin.
