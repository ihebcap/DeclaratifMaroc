# TASK-193 — Frais bancaires (TVA) jamais inclus dans une déclaration — filtre domaine incomplet

Status: ✅ Terminé
Date: 05/08/2026
Module: Declaration.Application
Fichiers modifiés:
- `Declaration.Application/Services/DeclarationWorkflowService.cs`
- `Declaration.Orchestration.Tests/Task193FraisBancairesDomaineTests.cs`

---

## 1. Contexte & Constat

Lors de la création et du figeage des lignes d'une déclaration (`DeclarationWorkflowService.ConstruireLignesFigeesAsync` et `RevaliderLignesFigeesAsync`), les candidats retournés par `SelectionExpliqueeService.SelectionnerExpliqueeAsync` étaient filtrés par domaine :
- Pour le domaine `"Decaissement"`, seules les sources `Decaissement`, `Espece`, `Depense` étaient conservées.
- Pour le domaine `"Encaissement"`, seule la source `Encaissement` était conservée.

Conséquence : la 5ᵉ source `SourceAffectation.FraisBancaire` (opérations bancaires avec TVA, `PT_Domaine = 6`, TASK-031) était systématiquement éliminée quel que soit le domaine, rendant les frais bancaires invisibles et absents de toute déclaration créée.

---

## 2. Solution apportée

1. **Rattachement au domaine selon le Sens de l'opération** :
   Dans `DeclarationWorkflowService.cs` (`ConstruireLignesFigeesAsync` et `RevaliderLignesFigeesAsync`) :
   - `domaine == "Decaissement"` inclut désormais `Source == FraisBancaire && Sens == Achat` (commissions et frais bancaires déductibles).
   - `domaine == "Encaissement"` inclut désormais `Source == FraisBancaire && Sens == Vente` (encaissements/produits bancaires collectés).

2. **Clivage Collecté / Déductible dans les agrégats de contrôle** :
   Dans `DeclarationWorkflowService.cs` (calculs de `RecapsParTaux` et `RecapsParActivite`) :
   - Condition mise à jour : `Collecte = l.Domaine == "Encaissement" || l.Source == nameof(SourceAffectation.Encaissement)` pour garantir l'exactitude de la ventilation par type de TVA.

3. **Garde-fous & non-régression** :
   - Aucune modification de `SelectionnerFraisBancaireAsync` ni du calcul TVA existant.
   - Les 4 autres sources (`Decaissement`, `Espece`, `Depense`, `Encaissement`) conservent exactement leur comportement initial.
   - Ajout d'une suite de tests unitaires dédiés `Task193FraisBancairesDomaineTests.cs` (229/229 tests `Declaration.Orchestration.Tests` verts).

---

## 3. Impact rétroactif (Note PO)

Toutes les déclarations closes avant ce correctif ont potentiellement ignoré des frais bancaires avec TVA si la société en comptabilisait. 
- Les nouvelles déclarations (et déclarations recalculées/réouvertes) incluront automatiquement les frais bancaires éligibles.
- Pour les déclarations déjà clôturées en production, un éventuel rattrapage devra être arbitré avec le PO (réouverture/recalcul explicite).
