# TASK-102 — Tracer le n° de règlement (RC) dans les anomalies de facture (écran ② Vérifier & Intégrer)

> **Origine** : question PO 17/07/2026 sur l'écran ② « Vérifier & Intégrer », bloc « Anomalies
> bloquantes ». Les anomalies de facture (ex. « Ligne en anomalie de recalcul : Incohérence Sage …
> — pièce FC2501717 exclue de la valorisation ») n'affichent que le **numéro de pièce/facture**
> (`FC…`), jamais le/les **règlement(s) qui l'ont réglée** (`RC…`). Contredit le pivot
> **règlement-first** et le principe « aucune ligne silencieuse » : une anomalie sur une facture
> doit permettre de remonter au règlement d'affectation qui l'a fait entrer dans la déclaration.

## Contexte

Le lien facture↔règlement est **déjà présent en base** sur chaque `LigneCandidate` via le champ
`NumeroRapprochement` (= le `RC…`). Il est même déjà exposé dans certains messages d'alerte, ex.
[DeclarationWorkflowService.cs:396](../Declaration.Application/Services/DeclarationWorkflowService.cs#L396) :

```csharp
Message = $"Ligne déjà figée (facture {l.NumeroFacture}, règlement {l.NumeroRapprochement}, MV_Id={l.MV_Id}) : …"
```

En revanche, les alertes d'anomalie de **valorisation** ne le portent pas :

- **Anomalie bloquante** `FACTURE_NON_VENTILEE`
  ([DeclarationWorkflowService.cs:842-848](../Declaration.Application/Services/DeclarationWorkflowService.cs#L842)) :
  ```csharp
  Message = $"Ligne en anomalie de recalcul : {l.MotifRejet}",
  RefLigne = l.NumeroFacture   // ← pas de NumeroRapprochement
  ```
- **Avertissement** d'incohérence Sage sur ligne exclue dès le figeage
  ([DeclarationWorkflowService.cs:412-418](../Declaration.Application/Services/DeclarationWorkflowService.cs#L412)) :
  ```csharp
  Message = $"Facture {l.NumeroFacture} exclue de la valorisation (EC_Id={l.EC_Id}) : …",
  RefLigne = l.NumeroFacture   // ← pas de NumeroRapprochement
  ```

Côté API, le contrôleur renvoie déjà `refLigne` + `filtre.numeroRapprochement` quand un
rapprochement existe ([DeclarationsController.cs:273-285](../Declaration.API/Controllers/DeclarationsController.cs#L273)),
donc le drill-down est déjà câblable — mais le **texte de l'anomalie** reste muet sur le règlement.

**Note métier (multi-règlement)** : une `LigneCandidate` est **par affectation** (facture ×
règlement). Une facture réglée par plusieurs règlements produit donc **plusieurs lignes**, chacune
portant son propre `NumeroRapprochement` : chaque anomalie affichera le RC de sa ligne. Il n'y a
donc pas de concaténation multi-RC à gérer dans une même alerte — un RC par ligne d'anomalie.

## Périmètre STRICT

- **Inclus** :
  1. Ajouter le n° de règlement (`l.NumeroRapprochement`) dans le **message** des deux alertes
     d'anomalie de facture ci-dessus (`FACTURE_NON_VENTILEE` et l'incohérence Sage sur ligne
     exclue), en reprenant **exactement** la formulation déjà en place ligne 396
     (« facture {…}, règlement {…} »).
  2. Repli propre si `NumeroRapprochement` est vide/nul : conserver le message actuel sans mention
     de règlement (aucune régression, pas de « règlement (vide) »).
- **Exclu** :
  - Toute modification du **calcul** de la valorisation ou du contrôle d'équilibre (la valeur de
    l'écart, la sélection des lignes exclues : inchangés).
  - Le drill-down front vers la ligne source : déjà couvert (mécanisme TASK-016 /
    `filtre.numeroRapprochement`) — hors périmètre sauf si le PO constate qu'il ne fonctionne pas
    sur ces anomalies précises (à traiter alors séparément).
  - L'écran ① / la grille : périmètre limité au bloc « Anomalies / Avertissements » de l'écran ②.

## Objectif

```
Entrée  : LigneCandidate en anomalie (NumeroFacture + NumeroRapprochement déjà peuplés)
Traitement : inclure NumeroRapprochement dans le texte de l'alerte, avec repli si absent
Sortie  : chaque anomalie de facture affiche « facture FC…, règlement RC… » → la pièce ET le
          règlement qui l'a fait entrer dans la déclaration sont tracés
```

## Livrables

- `DeclarationWorkflowService.cs` modifié : messages `FACTURE_NON_VENTILEE` (l.846) et incohérence
  Sage ligne exclue (l.416) enrichis du règlement, avec repli si `NumeroRapprochement` vide.
- Test(s) unitaire(s) dans `Declaration.Orchestration.Tests` : au moins un cas anomalie avec
  règlement peuplé (RC présent dans le message) et un cas sans règlement (message inchangé, pas de
  mention parasite).
- `VERIFY/TASK-102_verify.md` : build OK, tests verts, et capture de l'écran ② montrant une
  anomalie réelle avec son `RC…` affiché (cas `FC2501717` / `FC2501667` déjà documentés).

## Critères de validation

- Les anomalies de facture affichent le n° de règlement `RC…` quand la ligne en porte un.
- Aucune régression sur les anomalies sans règlement (message actuel conservé à l'identique).
- Aucun changement des montants, de l'écart d'équilibre, ni de la liste des lignes exclues.
- Cohérence de formulation avec l'alerte existante (l.396) — pas de nouveau vocabulaire.

## Risques / dépendances

- Aucun risque backend de calcul : enrichissement **texte** d'un contrat déjà exposé, lecture
  seule d'un champ déjà peuplé.
- Vérifier que `NumeroRapprochement` est bien renseigné sur les lignes **exclues dès le figeage**
  (`lignesExcluesIncoherence`) comme sur les lignes intégrées — sinon repli (mention omise), sans
  bloquer.
