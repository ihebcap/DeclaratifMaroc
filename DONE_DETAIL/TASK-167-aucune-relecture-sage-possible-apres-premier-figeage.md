# TASK-167 — Aucun moyen de relancer une vraie lecture Sage sur une ligne en échec après le premier figeage d'une déclaration

## Contexte
Suite directe de TASK-164 : le PO a demandé pourquoi `FC2502094` restait « Facture introuvable » alors
qu'elle existe bien sur Sage. Investigation architecte (24/07/2026) : la facture se lisait parfaitement
— il suffisait de relancer une vraie lecture Sage, ce que **rien dans l'écran de déclaration ne permet
de faire** une fois la déclaration déjà figée une première fois. C'est ce trou structurel, généralisable
à toute ligne en échec futur, que couvre cette task.

## Constat (vérifié en lisant le code, pas supposé)

### 1. « Passer au calcul » ne relit plus jamais Sage après le premier figeage
`DeclarationWorkflowService.ChargerCandidatesSiNecessaireAsync` (le code réel derrière le bouton
« Passer au calcul ») :
```csharp
if (await _repository.GetLignesCountAsync(declarationId, domaine, null) > 0)
{
    if (declaration.Statut == StatutDeclaration.Cloturee) return Array.Empty<Alerte>();
    return await RevaliderLignesFigeesAsync(declarationId, domaine);
}
```
La **première fois** (déclaration vierge), ce chemin appelle bien `ConstruireLignesFigeesAsync` →
`OrchestrateurDeclaration.Traiter()` → vraie lecture Sage, sur toutes les factures dues sur la période
peu importe leur date de facture (c'est la date de règlement qui détermine la période, pas la date de
facture — confirmé, l'intuition initiale du PO sur ce point est juste). Mais dès que des lignes existent
déjà pour ce (déclaration, domaine) — ce qui est le cas dès le premier figeage —, tous les appels
suivants passent par `RevaliderLignesFigeesAsync`, dont le commentaire du code dit explicitement :
« rien n'est recalculé/modifié, uniquement une alerte possible ». **Aucune nouvelle lecture Sage n'est
jamais retentée pour une ligne déjà en échec.**

### 2. Le bouton « Diagnostiquer » (TASK-144/147/164) ne relit pas Sage non plus
`RecalculerLigneDepuisCacheAsync` (`DeclarationWorkflowService.cs:1464-1467`, commentaire du code
lui-même) : « Ne redéclenche AUCUNE nouvelle lecture OM Sage — reconstruit la (les) ligne(s) candidate(s)
directement depuis les buckets déjà en cache ». Si le cache est absent ou en erreur (cas des 7 lignes de
TASK-164), ce bouton échoue avec « rien à recalculer sans nouvelle lecture Sage » — **il ne peut
structurellement pas réparer une ligne dont le cache n'a jamais été alimenté avec succès.**

### 3. Le seul chemin qui relit vraiment Sage est un écran différent, filtré par une date différente
`RafraichirValorisationAsync`/`LireFacturesDepuisPeriodeAsync` (bouton « Rafraîchir » de l'écran
Factures, TASK-050) déclenche une vraie lecture Sage — mais filtrée par **date de facture** (`DO_Date`),
saisie manuellement par l'utilisateur. Rien dans l'écran de déclaration ne renvoie vers cet écran, ni
n'indique quelle plage de dates couvrir pour une ligne précise en échec. Un comptable voyant
« FC2502094 : Facture introuvable » n'a aucun moyen de deviner qu'il doit aller sur l'écran Factures et
taper « nov.-déc. 2025 » (la date de la facture, pas celle du règlement affiché dans la déclaration).

### Preuve réelle (TVA1-2026-06)
197 lignes bloquées depuis le figeage du 16-17/07/2026 par un aléa de lecture OM ponctuel (probablement
contention, cf. TASK-156). 190 se sont réparées seules car leur cache a été rafraîchi entre-temps par un
autre appel sans rapport (facture-first sur une autre plage). Les 7 restantes (TASK-164) sont restées
bloquées et ont été escaladées au PO comme « cause non élucidée, arbitrage nécessaire » — alors qu'un
simple rafraîchissement facture-first sur `2025-11-01→2025-12-31` (leur vraie date de facture) les a
toutes réparées instantanément (24/07/2026, vérifié en base et au navigateur : 7 → 0 anomalie). Le
défaut n'était pas Sage, c'était l'absence de tout chemin, depuis la déclaration, pour relancer cette
relecture.

## Objectif
1. **Donner un moyen, depuis l'écran ③ Vérifier & Intégrer, de déclencher une vraie relecture Sage
   individuelle** pour une ligne en anomalie (motif non vide), sans avoir à deviner une plage de dates
   sur un autre écran. Concrètement : le repli individuel qui existe déjà côté orchestrateur
   (`OrchestrateurDeclaration.Traiter`, lecture Sage pièce par pièce en cas d'échec du lot) doit devenir
   invocable pour UNE ligne précise depuis ce bouton, au lieu d'être seulement un mécanisme interne au
   figeage initial.
2. **Décision de conception à trancher par le PO, pas par l'architecte** : cette relecture doit-elle
   rester une action manuelle explicite par ligne (même patron que « Diagnostiquer » aujourd'hui), ou le
   diagnostic doit-il proposer un bouton distinct « Relire depuis Sage » quand le cache est absent/en
   erreur (`cachePerime=false`, cf. `DiagnosticModal.tsx`) ? **Recommandation architecte** : action
   manuelle explicite par ligne, jamais un retry automatique silencieux à chaque ouverture d'écran —
   une relecture Sage individuelle ouvre une session OM à chaque appel ; un retry automatique en masse
   réintroduirait exactement le risque de contention déjà corrigé par TASK-156.
3. **Respecter impérativement le verrou anti-chevauchement par `soId`** (TASK-156,
   `ExecuterAvecVerrouOMAsync`/`_valorisationLocks`) pour toute nouvelle relecture individuelle ajoutée
   par cette task — ne pas réintroduire un 5ᵉ appelant de l'orchestrateur hors du verrou partagé.
4. Une fois livré, vérifier en conditions réelles (pas une fixture) qu'une ligne en échec authentique
   (ou reproduite volontairement) redevient valorisée après l'action, sans jamais changer `DT_Id` ni
   sortir la ligne de la déclaration.

## Garde-fous
- Lecture seule pour tout diagnostic SQL.
- Toute nouvelle relecture Sage individuelle doit passer par le verrou `soId` partagé (TASK-156) — pas
  de 5ᵉ chemin non protégé vers `OrchestrateurDeclaration`/le worker Sage.
- Jamais de montant fabriqué : si la relecture échoue encore, le motif réel reste affiché, bloquant.
- Jamais de changement de `DT_Id`/périmètre de déclaration par cette action (même garde-fou que
  `RecalculerLigneDepuisCacheAsync`, TASK-164).
- Pas de retry automatique silencieux en masse (risque de contention OM, cf. recommandation §2) sans
  arbitrage explicite du PO en sens contraire.

## Files
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`ChargerCandidatesSiNecessaireAsync`, `RecalculerLigneDepuisCacheAsync`, `RafraichirValorisationAsync`, `_valorisationLocks`/`ExecuterAvecVerrouOMAsync`).
- [Declaration.Orchestration/OrchestrateurDeclaration.cs](../Declaration.Orchestration/OrchestrateurDeclaration.cs) (repli individuel existant, à rendre invocable pour une ligne précise).
- [Declaration.API/Controllers/DeclarationsController.cs](../Declaration.API/Controllers/DeclarationsController.cs) (nouvel endpoint ou extension de `recalculer-depuis-cache`).
- [declaration-tva-web/src/DiagnosticModal.tsx](../declaration-tva-web/src/DiagnosticModal.tsx) / [VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) (action front).

## Validation
- [ ] Build back + front OK.
- [ ] Une ligne en échec authentique (motif `FACTURE_INTROUVABLE` ou équivalent, cache absent/en erreur)
      redevient valorisée après l'action, vérifié en base réelle (pas une fixture) et au navigateur.
- [ ] Verrou `soId` (TASK-156) confirmé respecté par le nouveau chemin (test croisé : rejet 409 si un
      autre appelant du même `soId` est déjà en cours).
- [ ] Aucun changement de `DT_Id`/périmètre observé après l'action.
- [ ] Aucune régression sur `RecalculerLigneDepuisCacheAsync`/`RafraichirValorisationAsync` existants
      (tests `Declaration.Orchestration.Tests` rejoués verts).
- [ ] Décision PO tracée sur le mode d'action (manuel explicite vs proposition automatique, §2).

## Dépendances / risques
- Dépend de l'arbitrage PO sur le mode d'action (§2) avant tout développement front définitif.
- Risque principal : mal borner cette relecture individuelle pourrait réintroduire la contention OM déjà
  corrigée par TASK-156 si elle n'est pas strictement soumise au même verrou par `soId`.
- Aucune autre déclaration connue n'est concernée aujourd'hui (TVA1-2026-06 déjà résorbée à 0 anomalie,
  cf. addendum TASK-164) — cette task est préventive, pas un correctif d'urgence sur une déclaration
  bloquée.
