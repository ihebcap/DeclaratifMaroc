# TASK-031 — Domaine manquant : Opération bancaire (frais bancaire) avec TVA

> ⚠️ **PARTIELLEMENT IMPLÉMENTÉE (03/08/2026)** — dérogation ponctuelle : Claude a codé directement,
> pas de revue par un 2ᵉ agent indépendant. **Cartographie corrigée en cours de route** (fausse
> piste de la version précédente de cette TASK) : la source réelle est **exclusivement**
> `RT_PREVISIONNELLE.PT_Domaine=6` — `RT_MOUVEMENT.MV_Domaine=6` (cité plus haut comme source) est
> en réalité le domaine **Dépense** (vérifié sur données réelles + code existant
> `GrfEnums.Domaine_Depense`), pas frais bancaire. Taux résolu via
> `P_TYPEOPBANQUE.TO_ErpTaxeNo → F_TAXE.TA_No` (Sage, connexion séparée, jamais cross-base —
> TASK-154). Vérifié bout-en-bout sur base réelle : 2 frais bancaires avec TVA (FRAIS 10%,
> COMMLEASING 14%) correctement valorisés (assiette/TVA/TTC exacts), 1 opération sans TVA (agio)
> correctement exclue. Build solution + 3 suites de tests rejouées : Core 218/218, Selection
> 60/60 (dont fixture `IntegrationRegressionTests` corrigée — connexion Sage distincte de la
> connexion GRF, cf. réserve ci-dessous), Orchestration 228/228.
>
> **Livré** : sélection (`SelectionExpliqueeService.SelectionnerFraisBancaireAsync`), valorisation
> directe (`OrchestrateurDeclaration.resoudreFactureBrute`, nouveau `SourceAffectation.FraisBancaire`),
> mode paiement Simpl-TVA fixe = 3, identité banque (IF/ICE via `RT_INFOCBANQ`, jamais de valeur
> inventée si absente — actuellement vide sur cette base, jamais configurée côté client).
>
> ✅ **Anti-double-déclaration clarifié et corrigé (03/08/2026, suite question PO)** : le tampon
> `DT_Id`/TASK-028 (`RT_AFFECTATION`) ne s'applique en effet jamais ici (pas de ligne
> `RT_AFFECTATION` pour un frais bancaire — **exactement comme pour la Dépense**, vérifié : 0/123
> dépenses réelles n'ont de ligne `RT_AFFECTATION` non plus). Mais ce n'est **pas** le mécanisme
> qui protège réellement contre la double déclaration : c'est le garde-fou d'exclusivité
> inter-déclaration (TASK-080, `DeclarationWorkflowService.AppliquerExclusiviteInterDeclarationAsync`)
> qui clé sur `(NumeroFacture, NumeroRapprochement)` contre `DM_LGTVA` de **toute autre**
> déclaration (`EnCours`/`Cloturée`) de la société — domaine-agnostique, déjà actif en production
> pour la Dépense. **Bug initial corrigé** : la clé générée était `MV_Numero` seul (ex. `"FRAIS"`,
> `"COMMLEASING"`) — le LIBELLÉ du type d'opération, pas un identifiant unique ; deux frais du même
> type dans la période auraient partagé la même clé. Corrigé en `"{MV_Numero}-{PT_Id}"`
> (`SelectionExpliqueeService.cs`) — vérifié unique sur les 2 exemples réels
> (`FRAIS-84`/`COMMLEASING-85`), build + 3 suites de tests rejouées (218/60/228).
>
> ✅ **Export Excel/XML vérifié (03/08/2026)** : entièrement générique, aucun code touché.
> - Excel (`Declaration.Export.Excel/Exporter.cs`) : aucune référence à `SourceAffectation`, aucune
>   validation bloquante — itère `RecapsParSource`/`RecapsParTaux`/`Lignes` sans distinction de
>   domaine. Fonctionne tel quel.
> - XML (`DeclarationXmlExporter.cs`) : filtre `Source != Encaissement` (déductible uniquement, PO
>   14/07/2026) — inclut `FraisBancaire` naturellement. `MapModePaiement` gère déjà explicitement
>   `mode == "3"` → `<mp><id>3</id></mp>` (code préexistant, anticipait ce cas). Vérifié par 2 tests
>   temporaires (supprimés après) : ligne avec IF/ICE renseignés → XML correct
>   (`<mp><id>3</id></mp>`, `<tx>0.1</tx>`) ; ligne sans IF/ICE → `ApplicationException` explicite
>   (`ValidationIdentiteFiscale.ValiderPourExport`), **bloque l'export entier** (pas seulement la
>   ligne). C'est le comportement voulu et déjà appliqué à tous les autres domaines (TASK-048/151,
>   jamais de valeur inventée) — **pas un bug**, mais un vrai prérequis opérationnel :
>
> 🔴 **Prérequis client avant tout export réel avec frais bancaire** : `RT_INFOCBANQ.IB_ICE`/
> `IB_IDENTIFIANT` doivent être configurés pour la banque concernée (actuellement vides sur
> `GR_EMA_DISTRIBUTION`, jamais configurés) — sinon la clôture/export bloquera dès qu'une ligne
> frais bancaire y figure. Configuration côté client (Sage/GRF), pas un correctif de code.
>
> ⚠️ **"Poste 4 interrogations" (TASK-019)** : pas une brique séparée — les interrogations
> « Rapprochement » et « Affectation » de ce poste de travail sont alimentées par
> `GetReglementsRapprochementAsync` (voir point suivant, non câblé) ; l'interrogation « Factures à
> déclarer » (4ᵉ) est pilotée par `DM_LGTVA`/le pipeline générique déjà vérifié ci-dessus — elle
> fonctionne déjà pour les frais bancaire une fois figés.
>
> ⛔ **Écran Rapprochement (① Sélection) — décision PO (04/08/2026) : PAS d'ajout.**
> `GetReglementsRapprochementAsync` (`DeclarationRepository.cs:838`) reste inchangé — ni Dépense ni
> Frais bancaire n'y sont ajoutés (ce dernier avait été évalué trop risqué à chaud : requête massive
> — 15+ filtres, tri, pagination — sans aucun test unitaire réel ; la décision PO confirme qu'il ne
> faut de toute façon pas le faire, y compris pour la Dépense qui n'y est pas non plus aujourd'hui).
> Ces deux domaines restent sélectionnés/valorisés/déclarés directement par le backend
> (`SelectionExpliqueeService`/`OrchestrateurDeclaration`), sans passer par l'écran de preuve
> Rapprochement — comportement assumé, pas un gap à combler.

> 🔎 **Mise à jour 09/07/2026 (source confirmée par le PO).** Le domaine est porté par `RT_MOUVEMENT.MV_Domaine` : **0 = Encaissement, 1 = Décaissement, 6 = Frais bancaire**. Les frais bancaires **prévisionnels** proviennent de `RT_PREVISIONNELLE` avec `PT_Domaine = 6` (table distincte de `RT_MOUVEMENT`). Ceci lève l'inconnue de l'étape 1 / du risque « localiser la source exacte ».
>
> **Déjà livré hors périmètre déclaration** (écran Rapprochement, TASK-036/037) : la colonne `Domaine` mappe désormais `MV_Domaine` (0/1/6, codes inconnus → `Autre (n)`). ⚠️ Mais le pivot rapprochement lit **uniquement `RT_MOUVEMENT`** : les frais présents **seulement** dans `RT_PREVISIONNELLE` (`PT_Domaine = 6`) **n'apparaissent pas** dans l'écran → nécessite une UNION (voir étape 5 bis ci-dessous). À traiter en même temps que cette tâche.

## Contexte
L'ancien module GRFN déclare **trois** sources de déduction, pas deux : décaissement, dépense **et frais/commissions bancaires avec TVA** via `DeclarationTvaController.GetDeclarationCommissionBancaire` (`scratch/decompiled/UIDeclarationTva/Tresorerie.UIDeclarationTva.Stuctures/DeclarationTvaController.cs:917`). Ces lignes portent le mode de paiement `<mp><id>=3` (Opération bancaire) dans le relevé de déductions Simpl-TVA.

Constat (analyse d'écart 09/07/2026) : **aucune trace** de ce domaine dans notre code. `Declaration.Selection` ne couvre que Fournisseur / Espèce / Dépense / Client ; `SourceAffectation` n'a pas de valeur Opération/Frais bancaire ; le workflow et les exports l'ignorent. C'est le seul domaine de déduction **entièrement absent**.

## Périmètre STRICT
- **Inclus** : sélection des opérations bancaires avec TVA de la période, ventilation/valorisation de leur TVA, intégration au modèle de déclaration, mapping XML `<mp><id>=3`, restitution front (poste 4 interrogations) et exports (Excel/XML) au même titre que les autres sources.
- **Exclu** : opérations bancaires **sans** TVA ; toute modification de GRFN/base (lecture seule).

## Référence legacy (comportement à reproduire, corrigé)
`GetDeclarationCommissionBancaire` (lignes ~917-1000) :
- Source : `previsionnelManager.GetAllOperationBancaireToDeclaration(exercice.Debut, declaration.DateFin)`.
- Type d'opération → taxe ERP (`typeOperation.ErpTaxeNo`) ; ignore si pas de taxe ou taxe non « à taux ».
- `AssietteDeclaration = operation.Montant`, `MontantDeclaration = operation.MontantTva * (Encaissement ? +1 : -1)`.
- Domaine = Encaissement ou Décaissement selon `typeOperation.Sens`. `EntityType = OperationBancaire`.
- **Pas de prorata facture** (calcul direct, comme la dépense).

## Objectif
```
Entrée : société + période
Traitement : sélectionner les opérations bancaires avec TVA (RT_MOUVEMENT MV_Domaine=OperationBancaire ou table prévisionnelle équivalente), valoriser assiette/TVA, sens selon type d'opération
Sortie : lignes de déclaration source=FraisBancaire, intégrées comme les autres sources, mode paiement Simpl-TVA=3
```

## Étapes
1. ~~Cartographier en base la source réelle des opérations bancaires~~ **Résolu (09/07/2026)** : `RT_MOUVEMENT.MV_Domaine = 6` **et** `RT_PREVISIONNELLE.PT_Domaine = 6`. Reste à cartographier le lien type d'opération → code taxe → taux (2 connexions comme TASK-022).
2. Ajouter la valeur `SourceAffectation.FraisBancaire` (ou `OperationBancaire`) et le mapping `MapperModePaiementSimplTVA` = 3.
3. Requête de sélection (miroir des autres domaines : garde-fous universels, `DT_Id IS NULL`, date de période, sens).
4. Valorisation **directe** (assiette = montant, TVA = montant TVA), sans prorata, avec arrondi `AwayFromZero`.
5. Brancher dans le workflow (candidates, checkup, clôture) + exports Excel/XML + poste 4 interrogations.
5. bis **Écran Rapprochement (TASK-036/037)** : élargir la projection `GetReglementsRapprochementAsync` par une **UNION** `RT_MOUVEMENT` ∪ `RT_PREVISIONNELLE (PT_Domaine = 6)` pour que les frais bancaires prévisionnels remontent dans l'interrogation. Points de vigilance : colonnes non communes (numéro, tiers, montant, affectations, EC_Type/origine potentiellement absents côté prévisionnel → valeurs explicites, jamais nulles silencieuses), impact sur `COUNT` (pagination) et sur les tris/filtres, distinction visuelle de la source.
6. Aucune ligne silencieuse : cas exclus → motif explicite (pas de TVA, taxe non à taux, non rapproché).

## Livrables
- Sélection + valorisation opération bancaire dans `Declaration.Selection` / `Declaration.Core`.
- Tests (sélection hors DB + valorisation) et intégration au pipeline.
- `VERIFY/TASK-031_verify.md` : preuve sur base réelle (ou fixture si le client n'en a pas), montants réconciliés.

## Critères de validation
- Les frais bancaires avec TVA apparaissent dans la déclaration avec `<mp><id>=3`.
- Aucune opération sans TVA remontée ; tout exclu est causé.
- Lecture seule stricte ; arrondi conforme DGI.

## Risques / dépendances
- ⏸️ **Différé** : le client n'utilise pas ce cas → aucune urgence, valeur à confirmer avant réalisation.
- ~~Localiser la source exacte des opérations bancaires en base prod~~ **Résolu** : `RT_MOUVEMENT.MV_Domaine = 6` + `RT_PREVISIONNELLE.PT_Domaine = 6`.
- UNION `RT_MOUVEMENT` ∪ `RT_PREVISIONNELLE` : risque de doublon (un frais à la fois prévisionnel et réalisé ?) et de colonnes hétérogènes → cadrer les clés d'unicité avant réalisation.
- Cohérence sens Encaissement/Décaissement avec le mapping domaines XML (TASK-014).
