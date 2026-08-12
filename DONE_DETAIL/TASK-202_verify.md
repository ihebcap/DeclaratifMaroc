# Vérification / Clôture — TASK-202 : Refonte navigation TVA (scope original non réalisé)

**Date de clôture :** 09/08/2026  
**Statut final :** ❌ SCOPE ORIGINAL REJETÉ — clôturée sur base de correctifs ciblés (décision PO)  
**Auteur du rapport :** Antigravity — session de correctifs directs, 09/08/2026 19 h

---

## 1. Scope original non réalisé — et pourquoi

### Ce que TASK-202 demandait (06/08/2026)

La TASK demandait une scission de l'écran récap existant en **4 étapes distinctes du stepper** :

| # | Étape | Composant |
|---|-------|-----------|
| ① | Sélection | `ReglementsSelection.tsx` — inchangé |
| ② | Factures à déclarer | Nouvel écran persistant, `FacturesADeclarerPanel.tsx` dans le stepper |
| ③ | Vérifier | `VerifierIntegrerPanel.tsx` en mode consultatif pur, sans bouton Confirmer |
| ④ | Confirmer | `VerifierIntegrerPanel.tsx` en mode synthèse + bouton Confirmer |

Elle avait été soumise deux fois :

- **Rejet 06/08/2026** — rejet architecte après 1er round de cadrage.
- **Rejet 07/08/2026** — implémentation complète réalisée, mais **23/30 tests Playwright cassés au retest réel**. Trop de surface de régression pour une TASK classifiée MEDIUM-densité.

### Décision PO (09/08/2026, session de correctifs directs)

Le **09/08/2026**, lors d'une session de correctifs hors workflow normal (dérogation PO explicite), le problème de fond signalé lors du cadrage initial (« écrans cachés », « boutons qui remplacent tout l'écran », doublons visuels) a été traité **sans scission en 4 écrans**.

Au cours de cette session, la proposition de scinder les écrans en étapes supplémentaires a été soumise **deux fois** au PO. Les deux fois, le PO l'a explicitement refusée :

> **PO (09/08/2026) :** « je veux pas de gros chantier »

Le PO a choisi à la place une **pure optimisation de densité de l'écran existant** : intégration des onglets directement dans la toolbar de la grille, édition en ligne du code activité, suppression du composant de drill redondant, correction du bug de cases à cocher dupliquées.

**Conséquence :** TASK-202 est clôturée sur cette base réduite. Le scope original (stepper 4 étapes, `VerifierIntegrerPanel` scindé en modes `verifier`/`confirmer`, navigation « Voir lignes » → step ②) **n'est pas implémenté et ne le sera pas dans cette itération**.

> ⚠️ Le `DeclarationStepper.tsx` expose bien un step `'factures'` (libellé « Factures à déclarer »)
> et importe `FacturesADeclarerPanel`, résidu du passage du 07/08 non entièrement annulé. Ce step
> est fonctionnel en tant que composant isolé. Cependant l'exigence centrale de TASK-202 — scission
> ③/④ de `VerifierIntegrerPanel`, navigation inter-étapes avec filtres pré-appliqués — reste
> **non réalisée**.

---

## 2. Correctifs réellement livrés (09/08/2026) — vérification code actuel

Chaque correctif a été vérifié dans le code source avant rédaction. Aucun n'est pris pour acquis sur la seule base du contexte de session.

### ✅ Correctif 1 — Onglets Achats/Ventes intégrés dans `toolbarPrefix` de `DomainGrid`

**Fichier :** [`declaration-tva-web/src/FacturesADeclarerPanel.tsx`](../declaration-tva-web/src/FacturesADeclarerPanel.tsx)  
**Preuve :** ligne 116 — `toolbarPrefix={onglets}` passé à `<DomainGrid>`. Les boutons Achats/Ventes (domaines `'Decaissement'` / `'Encaissement'`) sont rendus directement dans la barre d'outils de la grille, sans bandeau séparé au-dessus.

**Gain :** densité améliorée, suppression du bandeau d'onglets indépendant qui occupait une ligne entière de l'interface.

---

### ✅ Correctif 2 — Colonne « Code activité » éditable en ligne dans la grille

**Fichier :** [`declaration-tva-web/src/FacturesADeclarerPanel.tsx`](../declaration-tva-web/src/FacturesADeclarerPanel.tsx)  
**Preuve :** ligne 15 — `{ key: 'codeActivite', label: 'Code activité', filterType: 'text', width: '220px', editable: true }` dans `FACTURES_COLUMNS`.

**Gain :** le comptable peut affecter le code activité directement dans la grille « Factures à déclarer », sans passer par le drill « Codes activité » de `VerifierIntegrerPanel`. Ce drill subsiste dans `VerifierIntegrerPanel` (voir §3 ci-dessous) mais n'est plus l'unique chemin.

---

### ✅ Correctif 3 — Suppression de `AffectationsDrill.tsx` et de ses tests Playwright dédiés

**Composant :** `AffectationsDrill.tsx` — **n'existe plus dans le projet** (grep exhaustif, zéro occurrence dans `declaration-tva-web/src/`).  
**Tests :** aucun fichier de test Playwright portant le nom `AffectationsDrill` ou une référence à ce composant ne subsiste dans `declaration-tva-web/tests/`.

Ce composant implémentait un drill plein-écran « Détail des lignes » accessible depuis l'écran ① Sélection. Son comportement était dupliqué avec l'écran « Factures à déclarer » — deux chemins vers la même grille de lignes, sans raison métier.

> **Note :** `VerifierIntegrerPanel.tsx` ligne 80 conserve un commentaire historique
> `// Statuts non valorisés (cohérent avec AffectationsDrill.tsx TASK-055)` — c'est
> une référence documentaire, pas une dépendance active.

---

### ✅ Correctif 4 — Bug cases à cocher dupliquées dans `DomainGrid.tsx`

**Fichier :** [`declaration-tva-web/src/DomainGrid.tsx`](../declaration-tva-web/src/DomainGrid.tsx)  
**Preuve :** lignes 347–349 — commentaire explicitant le correctif appliqué :

```
// La case à cocher est rendue par `rowSelection.checkboxes` (API v36)
// sur la première colonne — une colonne dédiée `checkboxSelection` (ancienne API) en plus
// produisait DEUX cases à cocher côte à côte (l'ancienne et celle injectée par rowSelection).
```

L'ancienne API `checkboxSelection: true` dans les `colDef` a été retirée. Seule la nouvelle API `rowSelection={{ mode: 'multiRow', checkboxes: true, headerCheckbox: true }}` (ligne 486) subsiste.

**Gain :** ce bug (coexistence des deux APIs AG Grid) produisait deux colonnes de cases à cocher — perçu comme un « écran cassé ». C'était une partie significative du retour signalé lors du cadrage de TASK-202.

---

## 3. Correctif listé dans le contexte de session — non confirmé après vérification

### ⚠️ Le mécanisme `drillFiltre` de `VerifierIntegrerPanel` N'a PAS été supprimé

Le contexte de session décrivait la suppression du « mécanisme de drill plein-écran redondant ». Précision nécessaire :

- **`AffectationsDrill.tsx`** : supprimé ✅ (voir §2 Correctif 3).
- **Le mécanisme `drillFiltre` interne à `VerifierIntegrerPanel.tsx`** : **toujours présent** (18 occurrences, lignes 330–758). Cela inclut les boutons « Codes activité » (ligne 1080) et « Toutes les lignes / Resynchroniser » (ligne 1093) qui déclenchent ce drill en remplaçant le contenu de l'écran récap par une instance de `DomainGrid`.

Ce comportement (bouton qui remplace l'écran) était précisément celui pointé par le PO lors du cadrage de TASK-202. Il subsiste dans `VerifierIntegrerPanel` pour les codes activité et le drill de toutes les lignes. Le correctif 09/08 n'a traité que le doublon côté `AffectationsDrill.tsx` accessible depuis l'écran ①, pas le drill côté `VerifierIntegrerPanel`.

---

## 4. Points non traités — hors scope de la clôture

Les éléments suivants figuraient dans la TASK originale comme « Points à trancher » ou dans le recensement « Fichiers impactés — statut binaire Exclue/Reportée ». Ils **restent non traités** et ne font pas partie de la clôture du 09/08/2026.

### 4.1 Points à trancher (TASK originale §Points à trancher)

| Point | Statut |
|-------|--------|
| Fusionner colonnes « Codes activité » + « Toutes les lignes » en une seule vue permanente | **Non traité** — les deux drills restent dans `VerifierIntegrerPanel` |
| Sort du bouton « Relire depuis Sage » dans `DiagnosticModal.tsx` | **Non traité** — bouton conservé |
| Navigation « Voir lignes » (anomalie dans ③) → step ② avec filtre pré-appliqué | **Non réalisé** — mécanisme `drillFiltre` en place, pas de navigation inter-étapes |

### 4.2 Statut binaire Exclue/Reportée (TASK originale §Fichiers impactés)

La simplification du statut de ligne vers un modèle purement binaire (`Proposée`/`Intégrée`) a été partiellement réalisée côté front (07/08 : suppression boutons « Exclure »/« Reporter » de `DomainGrid`, suppression de `WorkstationPanel.tsx` et `mockServer.ts`), mais :

- **Backend `Declaration.Application/Entities/WorkflowEntities.cs`** (enum `EtatLigne.Exclue`/`Reportee`/`Ecartee`) : **non modifié** — 5 suites de tests C# l'utilisent encore pour des scénarios de compatibilité historique (`Task080ExclusiviteInterDeclarationTests.cs`, `Task082LigneExclueDesLeFigeageTests.cs`, `Task102NumeroReglementAnomaliesFactureTests.cs`, `Task155GenerationExportTests.cs`, `Task160ExportControleTests.cs`).
- **`Declaration.Application/Services/DeclarationWorkflowService.cs`** et **`IDeclarationRepository.cs`** : **non modifiés** — logique de compatibilité historique intacte.

### 4.3 Point de vigilance multi-facture (non résolu)

Un règlement peut affecter plusieurs factures. Avec le statut binaire, la seule granularité de décision est le règlement entier (écran ①). Ce cas d'usage (traiter différemment deux factures d'un même règlement) n'est plus supporté. Remonté au PO lors du cadrage (TASK originale §Décisions point 4), **sans retour obtenu — non clôturé**.

---

## 5. Résumé de la décision de clôture

| Dimension | Décision |
|-----------|----------|
| Scission en 4 écrans stepper | ❌ Abandonnée — décision PO explicite (« je veux pas de gros chantier ») |
| Navigation inter-étapes avec filtres pré-appliqués | ❌ Non réalisée |
| `VerifierIntegrerPanel` scindé en modes verifier/confirmer | ❌ Non réalisé |
| Onglets Achats/Ventes dans `toolbarPrefix` | ✅ Livré et vérifié |
| Code activité éditable en ligne dans la grille | ✅ Livré et vérifié |
| Suppression `AffectationsDrill.tsx` + tests dédiés | ✅ Livré et vérifié |
| Bug cases à cocher dupliquées `DomainGrid` | ✅ Livré et vérifié |
| `drillFiltre` de `VerifierIntegrerPanel` supprimé | ❌ Non réalisé (hors scope session 09/08) |
| Statut binaire backend complet | ❌ Non réalisé (legacy intacte, 5 suites de tests C#) |

La TASK est clôturée sur la base des **4 correctifs livrés**, qui répondent partiellement à l'intention de fond (doublons visuels, cases à cocher dupliquées, drill redondant depuis l'écran ①). L'intention structurelle initiale (séparation nette des responsabilités en 4 écrans) est reportée à une itération ultérieure, si le PO la requalifie en priorité.

