# TASK-166 — Écran ④ Déclaration : montants mal formatés dans les avertissements + lisibilité de la liste

## Contexte
Signalement PO (24/07/2026, capture écran ④ Déclaration, `TVA1-2026-06`, onglet TVA Déductible, 32
avertissements) : demande de correction du même ordre que TASK-165 sur cet écran.

**Constat code (cartographie architecte)** : contrairement à l'écran ③ Vérifier & Intégrer
(`VerifierIntegrerPanel.tsx`, cf. TASK-165), l'écran ④ (`DeclarationFinalePanel.tsx`) est **déjà bien
structuré** : verdict/badges en tête (Verrou posé / bloquants / avertissements), un seul bloc de
synthèse chiffrée (`Stat`), sections repliables (`Section`, `open`/`onToggle`, résumé replié) pour
Répartition par source / Vue par taux / Anomalies bloquantes / Avertissements / Récap lignes. C'est
exactement le pattern recommandé pour TASK-165 (verdict unique + détail replié) — **aucune redondance
structurelle équivalente identifiée ici**.

En revanche, un **vrai défaut** est confirmé dans le contenu de la liste « Avertissements » :

### Défaut confirmé — montants non formatés dans les messages d'avertissement
Capture : `0,140000 MAD`, `0,330000 MAD`, `4973,380000 MAD`, `0,010000 MAD`, `2,960000 MAD` — 6 décimales
brutes, alors que **tout le reste de l'écran** (les 5 `Stat` en tête, les tableaux Source/Taux) affiche
des montants sur 2 décimales via `formatMoney()` (`utils.tsx:24-26`,
`Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'MAD' })`).

Cause : les messages d'avertissement sont des **chaînes déjà construites côté back** (pas des nombres
que le front pourrait reformater) — `alerte.message` est affiché tel quel (`DeclarationFinalePanel.tsx`
→ `AnomalieRow`, l.613). Les 6 templates concernés interpolent `MontantAffecte` (type `decimal`) sans
aucun format spécifié :
```
Declaration.Application/Services/DeclarationWorkflowService.cs:990-996
$"... {c.Affectation.MontantAffecte} MAD"   ← .ToString() par défaut, garde l'échelle brute de la valeur
```
6 occurrences, toutes dans le même bloc (switch construisant `message` selon `MotifRejet` : EcTypeHorsPerimetre ×2, Impaye, Annule, NonComptabilise, NonAffecte, cas générique). Confirmé isolé à ce
seul bloc (recherche du même motif ailleurs dans le repo : aucune autre occurrence).

## Décision de périmètre
Correction de bug pure (cohérence d'affichage, aucune valeur métier fausse) — pas besoin d'arbitrage PO
sur le principe, contrairement à TASK-165. Seul point mineur laissé ouvert : la convention exacte de
séparateur (le reste de l'écran utilise la virgule décimale française via `Intl.NumberFormat('fr-FR')`
côté front — reproduire cette convention côté back suppose soit un formatage `CultureInfo` dédié, soit
un arrondi simple `:N2` en culture invariante qui donnerait un point (`0.14`) au lieu d'une virgule
(`0,14`), incohérent avec le reste de l'écran). **Recommandation architecte** : arrondir à 2 décimales
au minimum (correction du défaut principal — l'échelle à 6 décimales illisible), avec séparateur virgule
si le coût est nul (`CultureInfo` française déjà nécessaire ailleurs dans l'export, cf.
`DeclarationXmlExporter.cs:104` qui utilise déjà `CultureInfo.InvariantCulture` pour un besoin différent
— pas de précédent direct à réutiliser tel quel).

## Périmètre STRICT
- **Inclus** : `Declaration.Application/Services/DeclarationWorkflowService.cs:987-997` — les 6
  templates de message, formater `MontantAffecte` sur 2 décimales (a minima), idéalement virgule
  décimale pour cohérence avec `formatMoney()` du front.
- **Inclus** : tests couvrant ces messages si des assertions `Assert.Contains` vérifient le texte exact
  (`Task100ReglementEctypeInconnuTests.cs:101/116` notamment) — à ajuster si le format du montant change
  dans la chaîne attendue.
- **Exclu** : tout autre champ/écran — recherche effectuée, ce défaut est isolé à ce bloc.
- **Exclu** : la structure de l'écran ④ elle-même (déjà conforme, cf. constat ci-dessus) — pas de
  réagencement de blocs nécessaire, contrairement à TASK-165.

## Observation secondaire (non bloquante, à confirmer PO si souhaité)
32 avertissements, dont l'écrasante majorité est du même motif (« Règlement hors périmètre (Autre (90))
— non déclarable »), affichés en liste plate sans tri ni regroupement — un montant significatif
(4 973,38 MAD, `RC26060080`, motif différent « non comptabilisé ») est noyé au milieu de dizaines
d'entrées à quelques centimes. Piste possible (non retenue sans confirmation PO, hors périmètre strict
de cette task) : regrouper par motif avec sous-compteur, et/ou trier par montant décroissant, pour faire
remonter les cas matériellement significatifs. **Non traité ici** — signalé pour arbitrage PO séparé si
jugé utile, cette task se limite au défaut de formatage confirmé.

## Étapes
1. `DeclarationWorkflowService.cs` : appliquer un format à 2 décimales (`:N2` a minima) aux 6 templates
   de message (l.990-996).
2. Ajuster les assertions de test qui vérifient le texte exact du message si le format change
   (`Task100ReglementEctypeInconnuTests.cs` et tout autre test dépendant).
3. Rejouer un cas réel (`TVA1-2026-06` ou équivalent) pour confirmer l'affichage `0,14 MAD` (ou `0.14
   MAD` selon la convention retenue) au lieu de `0,140000 MAD`.

## Livrables
- `DeclarationWorkflowService.cs` modifié (6 templates).
- Tests ajustés si nécessaire.
- `VERIFY/TASK-166_verify.md` avec capture d'écran avant/après.

## Critères de validation
- Tous les montants dans les messages d'avertissement/anomalie affichent 2 décimales, cohérentes avec le
  reste de l'écran.
- Aucune régression sur le texte des messages au-delà du format du montant (motif, tiers, numéro de
  règlement inchangés).
- Build + tests `Declaration.Orchestration.Tests` rejoués verts (notamment `Task100...Tests`).

## Risques / dépendances
- **Risque faible** : changement de formatage de chaîne uniquement, aucun changement de calcul. Seul
  point d'attention : tests qui feraient un `Assert.Equal`/`Assert.Contains` sur le texte exact du
  montant (ex. recherche de la sous-chaîne `"MontantAffecte}"` littérale) — à vérifier lors du VERIFY.
