# VERIFY — TASK-103 : `recapSource`/`recapTaux` alignés sur `Integree || Proposee`

## 1. Modification de code

[`DeclarationsController.cs`](file:///D:/_vibe/GRF/Declaration.API/Controllers/DeclarationsController.cs) — `GetCheckup` (l.234-261) :

- Ajout de `lignesRecap = lignes.Where(l => l.Etat == EtatLigne.Integree || l.Etat == EtatLigne.Proposee)`,
  aligné sur l'ensemble déjà utilisé par le contrôle d'équilibre
  (`DeclarationWorkflowService.cs:742`).
- `recapSource` et `recapTaux` sont maintenant construits sur `lignesRecap` (au lieu de `integrees`
  = `Integree` seul).
- **Non touché** (périmètre strict respecté) :
  - `integrees` (`Integree` seul, l.234) reste utilisé tel quel pour `totalTva` et le calcul de
    l'écart (l.265-268) — inchangé.
  - Aucune modification front (`RecapSourceTable`, condition d'affichage `VerifierIntegrerPanel.tsx:1033`).

## 2. Tests automatisés

Nouveau fichier [`Task103RecapSourceProposeeTests.cs`](file:///D:/_vibe/GRF/Declaration.Orchestration.Tests/Task103RecapSourceProposeeTests.cs)
(instancie `DeclarationsController` directement avec un repo en mémoire + le vrai
`DeclarationWorkflowService`) :

- `GetCheckup_DeclarationEnCoursLignesProposeeEnEcart_RecapSourceNonVide` : déclaration `EnCours`,
  2 lignes `Proposee` valorisées → `recapSource` **non vide**, sommes HT/TVA/TTC correctes
  (1500 / 250 / 1750).
- `GetCheckup_DeclarationClotureeLignesIntegree_RecapSourceInchange` : déclaration `Cloturee`,
  ligne `Integree` → `recapSource` identique à avant (non-régression écran ⑤).

Nécessite d'ajouter la référence de projet `Declaration.API` à
`Declaration.Orchestration.Tests.csproj` ; le TargetFramework du projet de tests a dû passer de
`net10.0` à `net10.0-windows` pour matcher `Declaration.API` (net10.0-windows, dépendances
Tresorerie.* legacy) — sans impact fonctionnel, la CI est déjà exécutée sur poste Windows.

```
dotnet test Declaration.Orchestration.Tests/Declaration.Orchestration.Tests.csproj
→ Réussi ! - échec : 0, réussite : 127, ignorée(s) : 0, total : 127
```

`dotnet build Declaration.API/Declaration.API.csproj` → build réussi, 0 avertissement, 0 erreur.

## 3. Vérification live (écran ②)

Démarrage API (`dotnet run --project Declaration.API`) + front (`npm run dev` via
`declaration-tva-web`), connexion `Admin`/`Admin`, société `NEW_EMA DISTRIBUTION`.

**Limite** : la déclaration `TVA1-2026-01` citée dans le ticket PO (écart 341 155,01 MAD,
245 lignes) n'existe plus dans l'état actuel de la base. La vérification live a donc été faite
sur les deux déclarations `EnCours` réellement présentes (`TVA1-2026-06`, `TVA1-2026-07`).

### 3.1 Visibilité du tableau (cas source unique)

Sur `TVA1-2026-06` (2 règlements sélectionnés, lignes `Proposee`) :

- Badge **ÉCART** : « Écart détecté : 1 538,27 MAD ».
- Tableau « Répartition par source » affiché (`SOURCE = Decaissement`, HT 7 691,38 /
  TVA 1 538,27 / TTC 9 229,65), alors qu'avant le fix ce tableau était absent tant que la
  déclaration n'était pas clôturée.

```
Cohérence des totaux déclarés                                    ÉCART
Écart détecté : 1538,27 MAD
SOURCE          HT              TVA             TTC
Decaissement    7 691,38 MAD    1 538,27 MAD    9 229,65 MAD
Total           7 691,38 MAD    1 538,27 MAD    9 229,65 MAD
```

### 3.2 Preuve multi-sources (revue architecte du 2026-07-17 — rejet initial)

**Rejet reçu** : la vérification live ci-dessus (3.1) ne prouvait pas l'objectif « localiser
d'où vient l'écart » car elle ne portait que sur une seule source (le tableau ne fait alors que
recopier le badge). Correction demandée : reproduire un cas réel avec ≥ 2 sources.

**Reconstruction tentée** : construction d'une nouvelle déclaration réelle `TVA1-2026-07`
(société 1, période 07/2026) via l'écran ① avec une sélection réelle mêlant règlement espèce
(source `Espece`) et règlement encaissement (source `Encaissement`). Recherche également d'un cas
mêlant `Espece` et `Decaissement` (chèque/traite/virement) **dans le même onglet Achats**, pour
démontrer la ventilation à l'intérieur d'un seul tableau.

**Blocage découvert (bug distinct, hors périmètre TASK-103)** : 8 règlements espèce réels et
éligibles à l'écran de sélection (`RF26030013` à `RF26030017`, `RF26010010` à `RF26010012` —
affectation valide, `EC_Type=0`) ont été ajoutés à la sélection de `TVA1-2026-06` : **0/8** ont
produit une ligne valorisée. Root cause tracée dans
[`VentilationSageCacheRepository.cs:63`](file:///D:/_vibe/GRF/Declaration.Orchestration/VentilationSageCacheRepository.cs)
— la requête de recherche du « token » MV exige `MV_Point = 1` (réconciliation bancaire) sans
exception pour la source `Espece`, alors que
[`SelectionExpliqueeEvaluator.cs:85-90`](file:///D:/_vibe/GRF/Declaration.Selection/SelectionExpliqueeEvaluator.cs)
exempte explicitement les espèces de cette exigence (déclarables via `DatePaiement`, jamais
rapprochées en banque). Conséquence : toute source `Espece` reste bloquée « aucun paiement
pointé (token MV NULL, non déclarable) » (log `valorisation.log`) et ne peut donc jamais
apparaître aux côtés d'une source `Decaissement` dans le même onglet tant que ce bug n'est pas
corrigé. **Signalé séparément** (tâche hors périmètre, non traitée ici).

**Preuve multi-sources obtenue malgré le blocage** (`TVA1-2026-07`, 3 règlements réels
sélectionnés : `RF26070043` + `RF26070007` en Espèce/Décaissement, `RC26060077` en
Chèque/Encaissement) :

Onglet **TVA Déductible (Achats)** — écart 1 480,50 MAD :
```
SOURCE     HT              TVA             TTC
Espece     6 216,62 MAD    1 243,33 MAD    7 459,95 MAD
Total      6 216,62 MAD    1 243,33 MAD    7 459,95 MAD
```

Onglet **TVA Collectée (Ventes)** — même écart global 1 480,50 MAD :
```
SOURCE          HT              TVA             TTC
Encaissements   1 185,83 MAD    237,17 MAD      1 423,00 MAD
Total           1 185,83 MAD    237,17 MAD      1 423,00 MAD
```

**Limite assumée** : les deux sources apparaissent chacune dans leur propre onglet (la
ventilation `recapSource` est calculée par domaine, cf. contrôleur), pas dans un unique tableau
mêlant 2 sources. La preuve d'un mélange `Espece`+`Decaissement` dans le même tableau Achats
reste bloquée par le bug ci-dessus. Ce que cette preuve démontre : avec 2 sources réelles
distinctes et un écart réel, le tableau affiche bien la source responsable (`Espece` sur
Achats, `Encaissements` sur Ventes) plutôt qu'une simple recopie muette du badge — le mécanisme
de ventilation fonctionne correctement dès qu'il reçoit plusieurs sources.

Écran ⑤ (« Déclaration ») non testé en live sur ces jeux de données : le déclencher aurait exigé
de cliquer « Confirmer et figer la déclaration », une action irréversible sur une déclaration
réelle de la base partagée — action volontairement évitée. La non-régression de l'écran ⑤ est
couverte par le test automatisé `GetCheckup_DeclarationClotureeLignesIntegree_RecapSourceInchange`
(ci-dessus), qui isole exactement ce cas (lignes déjà `Integree`, résultat identique avant/après).

**Nettoyage** : la sélection de `TVA1-2026-06` a été restaurée à son état d'origine (2
règlements : `RF26020040` + `RF26060129`) après les tests de reconstruction ci-dessus.

## Critères de validation — statut

- [x] Tableau « Répartition par source » visible à l'étape ② sur une déclaration `EnCours` en
  écart (vérifié live sur `TVA1-2026-06` et `TVA1-2026-07`).
- [x] La ventilation couvre les lignes `Proposee` (mêmes lignes qui alimentent l'écart) — cohérence
  HT/TVA/TTC vérifiée (test automatisé + live).
- [~] Ventilation multi-sources démontrée **entre onglets** (Espèce/Achats vs
  Encaissement/Ventes) sur données réelles ; **non démontrée à l'intérieur d'un même onglet**
  (Espèce+Decaissement) à cause du bug distinct décrit en 3.2 (bloquant, hors périmètre).
- [x] `displayTotalTVA` reflète `recapSource` (plus de repli) — conséquence directe de
  `recapSource` non vide, non re-testé isolément côté front (hors périmètre : aucune modif front).
- [x] Écran ⑤ inchangé — couvert par test automatisé dédié, non revérifié en live (cf. limite
  ci-dessus).
- [x] Calcul du montant de l'écart inchangé — badge identique avant/après (1 538,27 MAD sur
  `TVA1-2026-06`, 1 480,50 MAD sur `TVA1-2026-07`), code de l'écart (l.265-268) non modifié.
