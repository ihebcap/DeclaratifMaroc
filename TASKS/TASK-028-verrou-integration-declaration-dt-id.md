# TASK-028 — Verrou d'intégration déclaration (tampon `DT_Id` + triggers d'immuabilité)

## Contexte
Décision PO (08-09/07/2026) : **on ne bloque rien côté Sage**. La facture est déjà verrouillée sur GRF à
l'import ; le **règlement/affectation** doit être verrouillé dès qu'il est **intégré à une déclaration TVA**.
La GRF legacy (GRFN, boîte noire) fait déjà ce blocage — la décompilation le confirme :

- Le lien règlement↔déclaration est porté au grain **affectation** (règlement↔échéance), pas la pièce entière.
- Champ décompilé : `Affectation.DeclarationTvaEncaissementNo` (`int?`) — équivalent schéma GRF =
  **`RT_AFFECTATION.DT_Id`** (déjà **lu** par le module, TASK-008/021 : `DT_Id IS NULL` = non déclaré).
- Sélection legacy (`DeclarationTvaController`) : on ne retient que les affectations `DT_Id IS NULL` ; si
  toutes les affectations d'un règlement sont tamponnées → règlement ignoré.

Aujourd'hui le module **lit** `DT_Id` mais ne l'**écrit pas**, et rien ne **garantit** l'immuabilité (le
filtre applicatif est contournable : sync Sage, SQL direct, GRFN lui-même). Cette tâche pose le tampon **et**
le garde-fou base.

## Décisions figées (PO)
- **Portée trigger = globale** : s'applique à toute écriture sur `RT_MOUVEMENT`/`RT_AFFECTATION`, **GRFN inclus**
  (base partagée). C'est ce qui donne l'« intégrité prouvée ».
- **Réversibilité par réouverture** : rouvrir/supprimer une déclaration remet `DT_Id → NULL` sur ses
  affectations, ce qui ré-autorise dérapprochement/modif. Le trigger **doit autoriser la transition `DT_Id → NULL`**.
- **Rien n'est écrit dans Sage.** Le verrou vit exclusivement dans les tables `RT_*`.

## Périmètre STRICT
1. **Écriture du tampon (module)** : à l'intégration d'une affectation dans une déclaration → `RT_AFFECTATION.DT_Id = <No déclaration>`. À la réouverture/suppression → `DT_Id = NULL` pour toutes les affectations de la déclaration.
2. **Trigger `RT_AFFECTATION`** (immuabilité affectation déclarée) :
   - Interdire **DELETE** d'une ligne `DT_Id IS NOT NULL`.
   - Interdire **UPDATE** des colonnes financières/structurantes (montant, `MV_Id`, `EC_Id`, taux…) d'une ligne `DT_Id IS NOT NULL`.
   - **Autoriser** la transition `DT_Id : valeur → NULL` (dé-tamponnage = réouverture) et `NULL → valeur` (intégration).
3. **Trigger `RT_MOUVEMENT`** (immuabilité règlement engagé) : si le mouvement a **≥1 affectation `DT_Id IS NOT NULL`** :
   - Interdire le **dérapprochement** : `MV_Point` `1 → 0` (`Point_Oui = 1`).
   - Interdire la **décomptabilisation** : `MV_Compta` `1 → 0` (`Compta_Comptabilise = 1`).
4. **Message d'erreur propre** (`THROW`/`RAISERROR`) remonté aux deux applis : « Règlement inclus dans la déclaration TVA n° X — opération interdite ».
- **Exclu** : logique de cache (TASK-024), front (l'UI 4 interrogations affiche l'état, TASK-019).

## Positionnement / architecture
- Colonne : `RT_AFFECTATION.DT_Id` **existe déjà** (lue par la sélection). Vérifier son type/FK réel et si un
  index sur `DT_Id` est utile pour le trigger `RT_MOUVEMENT` (EXISTS par `MV_Id`).
- Triggers `AFTER`/`INSTEAD OF UPDATE,DELETE` sur `RT_AFFECTATION` et `AFTER UPDATE` sur `RT_MOUVEMENT`.
  Comparer `inserted`/`deleted` pour cibler précisément les transitions interdites (ne pas bloquer les updates neutres).
- Écriture du tampon côté module : au moment du **figeage/clôture** de la déclaration (cohérent avec la clôture
  gardée existante, TASK-012/017) ; dé-tamponnage à la réouverture.

## Objectif
```
Intégrer   : RT_AFFECTATION.DT_Id = No déclaration        (module)
Sélection  : DT_Id IS NULL sélectionnable, DT_Id=@dtId = lignes de la déclaration  (déjà en place)
Garde base : affectation DT_Id NOT NULL → DELETE/UPDATE financier interdits
             mouvement avec ≥1 affectation déclarée → MV_Point 1→0 et MV_Compta 1→0 interdits
Réouvrir   : DT_Id → NULL autorisé → dérapprochement/modif de nouveau permis
```

## Contraintes techniques
- Isolation `RT_*` respectée (aucune écriture Sage). SQL Server (T-SQL).
- Les triggers doivent être **set-based** (gérer un batch multi-lignes), pas ligne à ligne.
- Ne pas casser un flux GRFN légitime : seules les transitions listées sont bloquées ; tout le reste passe.
- Confirmer sur base (`GR_EMA_DISTRIBUTION`) : type/FK de `DT_Id`, valeurs `MV_Point`/`MV_Compta`
  (`GrfEnums` : `Point_Oui=1`, `Compta_Comptabilise=1`).

## Étapes
1. Confirmer schéma réel `RT_AFFECTATION.DT_Id` (+ index) et colonnes `RT_MOUVEMENT.MV_Point`/`MV_Compta` sur la base.
2. Écrire le tampon à l'intégration + dé-tampon à la réouverture (module, opération transactionnelle).
3. Trigger `RT_AFFECTATION` (DELETE + UPDATE financier interdits si `DT_Id NOT NULL` ; transitions `DT_Id` autorisées).
4. Trigger `RT_MOUVEMENT` (dérapprochement + décomptabilisation interdits si ≥1 affectation déclarée).
5. Messages `THROW` explicites (n° déclaration).
6. Tests : intégration→tamponnage, tentative dérapprochement bloquée, tentative DELETE affectation bloquée,
   réouverture→`DT_Id NULL`→dérapprochement de nouveau permis, batch multi-lignes, non-régression sélection.

## Livrables
- Scripts triggers (`RT_AFFECTATION`, `RT_MOUVEMENT`) + opération tampon/dé-tampon côté module.
- `VERIFY/TASK-028_verify.md` : preuves réelles — dérapprochement d'un règlement déclaré **refusé** avec message,
  DELETE/UPDATE d'affectation déclarée **refusé**, réouverture qui rend la main, exécution set-based prouvée.

## Critères de validation
- Une affectation `DT_Id NOT NULL` ne peut être **ni supprimée ni modifiée** (colonnes financières) — DELETE/UPDATE rejetés.
- Un règlement avec ≥1 affectation déclarée ne peut être **ni dérapproché ni décomptabilisé** (message explicite).
- La **réouverture** (`DT_Id → NULL`) est autorisée et rend de nouveau l'affectation sélectionnable/modifiable.
- Triggers set-based (batch OK), aucun flux GRFN neutre cassé. Build + tests verts.

## Risques / dépendances
- **Débloque** partiellement **TASK-024** : le tampon `DT_Id` immuable est la **garantie d'immuabilité** que sa
  condition d'activation exigeait (« verrouillage du règlement reste à définir »). Le cache de ventilations
  d'une affectation `DT_Id NOT NULL` devient permanent tant que la déclaration existe.
- **Risque legacy** : trigger global → valider qu'aucun flux GRFN légitime post-déclaration n'est cassé
  (dérapprochement/décomptabilisation devenant interdits). Prévoir communication + message clair.
- **Risque réversibilité** : si la transition `DT_Id → NULL` n'est pas correctement exclue du blocage, plus
  aucune correction de déclaration possible — cas de test **obligatoire**.
- Cohérence avec la clôture gardée (TASK-012/017) : le tamponnage se fait au figeage/clôture.
