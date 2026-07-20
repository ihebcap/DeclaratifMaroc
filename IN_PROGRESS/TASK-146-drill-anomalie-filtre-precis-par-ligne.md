# TASK-146 — Drill "Voir lignes" filtre par règlement au lieu de la ligne précise en anomalie

Status: 🆕 à faire
Priority: HIGH
Risk: MEDIUM (change un filtre existant, périmètre large : toutes les alertes de l'étape ② Vérifier & Intégrer)
Module: Declaration.API / Declaration.Infrastructure / declaration-tva-web

> **Origine :** constat PO en session (20/07/2026) : sur l'écran ② Vérifier & Intégrer, cliquer
> « Voir lignes » sur l'alerte « Ligne déjà figée (facture `FC2501667`, `EC_Id=20650`) : incohérence
> Sage... » ouvre un drill affichant **18 lignes** (toutes les factures du règlement du tiers ATLANTIC
> FOODS), pas seulement `FC2501667`. « Le drill ne filtre pas correctement. »

## Constat (preuve de code)

- [Declaration.API/Controllers/DeclarationsController.cs:328-343](../Declaration.API/Controllers/DeclarationsController.cs:328) :
  ```csharp
  // Drill câblé sur numeroRapprochement (seule clé supportée par le filtre JSON du repo, TASK-034)
  var alertes = result.Alertes.Select(a => {
      var ligne = lignes.FirstOrDefault(l => l.NumeroFacture == a.RefLigne);
      var hasDrill = ligne != null && !string.IsNullOrWhiteSpace(ligne.NumeroRapprochement);
      return new { ..., filtre = hasDrill ? (object)new { numeroRapprochement = ligne!.NumeroRapprochement } : null };
  }).ToList();
  ```
  Le filtre transmis au front est **toujours** `{ numeroRapprochement }` — jamais par facture ni par
  `EC_Id`, quel que soit le code d'alerte (`FACTURE_NON_VENTILEE`, `LIGNE_FIGEE_A_REVERIFIER`, etc.).
- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs:397-471](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs:397)
  (`BuildLigneFilterWhere`) : clés JSON supportées = `numeroRapprochement`, `source`, `tauxTVA`,
  `origine`, `incoherente`, `etat`. **Aucune clé `ecId` ni `numeroFacture` n'existe.**
- Conséquence concrète : un règlement qui couvre plusieurs factures (bordereau/paiement groupé)
  fait remonter TOUTES ses factures dans le drill dès qu'UNE seule est en anomalie — le comptable
  doit chercher la bonne ligne à l'œil parmi les autres, non signalées.

## Objectif

Ajouter une clé de filtre précise par ligne (`ecId`, clé technique déjà disponible sur `LigneCandidate`/`AlerteRevalidation` selon les chemins) au filtre JSON du repo, et l'utiliser en priorité dans
la construction du `filtre` d'alerte du endpoint `checkup` :
1. `BuildLigneFilterWhere` : ajouter la clé `ecId` (`AND EC_Id IN @EcIds`, `EC_Id` étant déjà une
   colonne indexée sur `LigneCandidate`/`DM_LGTVA` d'après les usages existants dans
   `DeclarationRepository.cs`).
2. `DeclarationsController.GetCheckup` (l.331-344) : construire `filtre = { ecId = [ligne.EC_Id] }`
   quand `ligne.EC_Id > 0`, en repli sur `numeroRapprochement` uniquement si `EC_Id` est absent
   (compat lignes historiques sans EC_Id, cf. commentaire ligne 50 `AffectationsDrill.tsx` : « ecId=0
   = clé inconnue, ligne figée avant TASK-077 »).
3. Le drill affiché doit alors ne montrer QUE la ou les ligne(s) réellement visée(s) par l'alerte,
   pas tout le règlement.

## Garde-fous

- Ne pas retirer le filtre `numeroRapprochement` existant (toujours utilisé par `handleDrillIncoherence`
  et potentiellement d'autres call sites) — ajouter, pas remplacer.
- Ne pas modifier `AffectationsDrill.tsx` (composant distinct, TASK-078, hors périmètre) — cette
  TASK concerne uniquement le drill de `VerifierIntegrerPanel.tsx` (`DomainGrid` en mode `drillFiltre`).
- Requêtes paramétrées uniquement (déjà le cas dans `BuildLigneFilterWhere`, garder la convention).

## Files

- [Declaration.API/Controllers/DeclarationsController.cs](../Declaration.API/Controllers/DeclarationsController.cs) (l.328-344).
- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs) (`BuildLigneFilterWhere` l.397-471).
- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` (aucun changement de logique attendu — le
  front transmet déjà `a.filtre` tel quel ; vérifier simplement que la nouvelle clé `ecId` est bien
  sérialisée/acceptée sans changement de contrat front).

## Validation

- [ ] Build back OK.
- [ ] Rejeu réel : ouvrir l'alerte `FC2501667`/`EC_Id=20650` sur une déclaration réelle — le drill
      n'affiche plus que la (les) ligne(s) de `EC_Id=20650`, pas les 18 lignes du règlement.
- [ ] Non-régression : le drill « Lignes incohérentes » (`handleDrillIncoherence`, filtre `incoherente`)
      continue de fonctionner sans changement.
- [ ] Non-régression : les alertes sans `EC_Id` connu (lignes figées avant TASK-077) retombent
      proprement sur le filtre `numeroRapprochement` existant (pas de régression silencieuse).

## Dépendances / risques

- Aucune dépendance sur TASK-145/147.
