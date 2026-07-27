# TASK-171 — Le niveau « défaut par tiers » de la cascade code activité (TASK-161) ne peut jamais matcher en réel

## Contexte
Suite directe de TASK-161 (déjà **APPROUVÉE**, cf. `DONE_DETAIL/TASK-161-code-activite-defaut-tiers-surcharge-ligne.md`).
Le PO signale (24/07/2026) : *« SCAT_NumeroTiers ça n'existe pas, je dois pas toucher à la table winform »* et
*« P_SOCIETECODEACTIVITETIERS c'est pas l'intitulé du tiers, c'est l'intitulé de code activité »*.
Investigation architecte (lecture directe de `D:\_vibe\apbs-gr_winform`, lecture seule, pas de supposition) :
le PO a raison sur les deux points, et le second révèle un défaut plus large que celui déjà connu.

## Constat (vérifié en lisant le code, pas supposé)

### 1. `SCAT_NumeroTiers` n'existe pas en base — confirmé, déjà documenté (TASK-161 §Risques)
Script `P_SOCIETECODEACTIVITETIERS-numero-tiers.sql` volontairement **non fusionné, non auto-exécuté**
(table possédée par `apbs-gr_winform`, jamais modifiée par ce dépôt — confirmé, aucun fichier winform
touché). Personne ne l'a exécuté : la colonne n'existe donc pas chez le PO. Comportement voulu, pas un bug.

### 2. `SCAT_ErpIntitule` n'est PAS l'intitulé du tiers — nouveau constat, plus grave
Lecture de `apbs-gr_winform` :
- `SocieteCodeActiviteTiersController.GetAllDistinctColumnValue()` :
  ```csharp
  if (string.IsNullOrEmpty(societe.ErpColumnNameCodeActiviteMarroc)) return new List<IErpColumnValue>();
  return _erpService.GetAllDistinctColumnValue(ErpFileType.Tiers, societe.ErpColumnNameCodeActiviteMarroc).ToList();
  ```
- `CDC-DELAI-PAIEMENT-MAROC.md` documente explicitement ce paramètre société :
  `ErpColumnNameCodeActiviteMarroc` (colonne SQL `SO_ErpColNameCodeActiviteMarrocFournisseur`) =
  *« Colonne Sage — code activité tiers (Maroc) »*.
- Autrement dit : `SCAT_ErpIntitule` est une valeur choisie (via un `SearchLookUpEdit`, pas une saisie
  libre) parmi les valeurs distinctes d'une **colonne Sage personnalisée par société**, dédiée à porter
  le **code activité TVA** directement sur la fiche tiers — **pas l'intitulé/nom du tiers**. La valeur
  réelle observée en base par le worker TASK-161 (`"146-Achat à l'intérieur"`, cf.
  `DONE_DETAIL/TASK-161_verify.md` ligne ~156) le confirmait déjà, mais avait été notée comme un
  simple "constat honnête" sans en tirer la conséquence ci-dessous.

### 3. GRF compare cette valeur au mauvais champ — défaut confirmé
`DeclarationWorkflowService.cs:813-819` (`MapLignesCandidates`) :
```csharp
var codeActivite = Declaration.Core.CodeActiviteResolver.Resoudre(
    surchargeManuelle: null,
    tiersNumero: c.Affectation.Tiers.Numero,
    tiersNom: c.Affectation.Tiers.Nom,          // <-- vrai nom du tiers (ex. "PHARMACIE MOUKRIM")
    codeActiviteSage: c.Affectation.Tiers.CodeActivite,
    mappingParNumero: mappingCodeActiviteParNumero,
    mappingParNom: mappingCodeActiviteParNom);  // <-- clés = SCAT_ErpIntitule (valeur de colonne activité, PAS un nom)
```
`ChargerMappingCodeActiviteTiersAsync` (ligne ~788) construit `mappingParNom` avec pour clé
`r.ErpIntitule` — donc littéralement la valeur de la colonne Sage "code activité", jamais le nom du
tiers. **`tiersNom` et les clés de `mappingParNom` appartiennent à deux domaines de valeurs disjoints** :
il n'existe aucune raison structurelle pour qu'ils coïncident un jour, sauf configuration Sage
accidentelle où `ErpColumnNameCodeActiviteMarroc` pointerait par erreur vers la colonne intitulé du
tiers. **Le niveau 2 de la cascade (`CodeActiviteResolver.cs` commentaire : « Défaut par tiers... sinon
par intitulé ERP libre en repli ») ne produit donc jamais de résultat en conditions réelles** — retombe
silencieusement, sans erreur ni log, au niveau 3 (`F_COMPTET.CT_APE`) ou au niveau 4 (`""`).

Les tests existants (`Task161CodeActiviteCascadeTests.cs`, `CodeActiviteResolverTests.cs`) ne détectent
pas ce défaut car ils injectent des données synthétiques où `tiersNom` est délibérément construit pour
correspondre aux clés du mapping mocké (ex. `tiersNom: "PHARMACIE MOUKRIM"` face à une clé identique) —
hypothèse invalidée par la lecture du code winform réel.

## Objectif
1. **Décision de conception à trancher par le PO** — deux voies possibles, non triviales :
   - **(A)** Faire lire par GRF, pour chaque tiers, la même colonne Sage que celle configurée par
     `ErpColumnNameCodeActiviteMarroc` (nécessite de lire ce paramètre société — actuellement stocké côté
     `apbs-gr_winform`/`P_SOCIETE`, à vérifier si déjà accessible côté GRF) et de comparer CETTE valeur
     à `SCAT_ErpIntitule`, au lieu du nom du tiers. Rend le niveau 2 enfin fonctionnel tel que conçu.
   - **(B)** Constater que ce niveau fait doublon avec le niveau 3 (`F_COMPTET.CT_APE`, déjà câblé et
     fonctionnel) si les deux visent le même objectif (code activité déjà connu côté Sage par tiers), et
     simplifier la cascade en le retirant plutôt que de le réparer.
   **Recommandation architecte** : creuser d'abord si `CT_APE` (niveau 3, déjà fonctionnel) et la colonne
   `ErpColumnNameCodeActiviteMarroc` (niveau 2, visé ici) sont deux mécanismes réellement distincts chez
   les clients existants, ou si le second est une tentative antérieure du même besoin — éviter de réparer
   un mécanisme redondant avec un autre déjà en place.
2. Corriger la documentation déjà approuvée : `DONE_DETAIL/TASK-161-code-activite-defaut-tiers-surcharge-ligne.md`
   et `DONE_DETAIL/TASK-161_verify.md` affirment un fonctionnement de la cascade niveau 2 qui n'est pas
   avéré en réel — à corriger pour refléter ce constat (pas une exigence de cette task de coder, mais de
   documenter honnêtement, cf. rôle garde-fou).
3. Si (A) est retenu : vérifier en conditions réelles (pas une fixture) qu'un tiers réellement paramétré
   côté Sage avec la colonne `ErpColumnNameCodeActiviteMarroc` produit bien le code activité attendu.

## Garde-fous
- Ne jamais toucher `apbs-gr_winform`/`P_SOCIETECODEACTIVITETIERS` (rappel PO, déjà respecté par TASK-161).
- Ne jamais faire dépendre GRF de `SCAT_NumeroTiers` (colonne jamais exécutée, cf. §1) — toute solution
  doit fonctionner avec les seules colonnes réellement présentes en base aujourd'hui.
- Aucune valeur de code activité fabriquée si la résolution échoue (cascade niveau 4 = "", inchangé —
  décision PO TASK-161 point 3, le code activité n'entre pas dans le XML de dépôt DGI).

## Files
- [Declaration.Core/CodeActiviteResolver.cs](../Declaration.Core/CodeActiviteResolver.cs)
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`ChargerMappingCodeActiviteTiersAsync`, `MapLignesCandidates`)
- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs) (`GetMappingCodeActiviteTiersAsync`)
- `DONE_DETAIL/TASK-161-code-activite-defaut-tiers-surcharge-ligne.md` / `DONE_DETAIL/TASK-161_verify.md` (correction documentaire)
- Référence externe (lecture seule) : `D:\_vibe\apbs-gr_winform\src\Tresorerie.UIConfiguration\Controllers\SocieteCodeActiviteTiersController.cs`, `D:\_vibe\apbs-gr_winform\analayse\CDC-DELAI-PAIEMENT-MAROC.md`.

## Validation
- [ ] Décision PO tracée sur (A) réparer vs (B) retirer le niveau 2 de la cascade.
- [ ] Si (A) : test réel avec un tiers Sage réellement paramétré via `ErpColumnNameCodeActiviteMarroc`,
      code activité résolu correctement, vérifié en base et non en fixture.
- [ ] Si (B) : `CodeActiviteResolver` simplifié, tests mis à jour pour refléter la cascade réellement
      utile (numéro tiers retiré aussi si (A) n'est jamais retenu, puisque structurellement inatteignable
      sans la colonne SCAT_NumeroTiers).
- [ ] `DONE_DETAIL/TASK-161*.md` corrigés pour ne plus affirmer un fonctionnement non avéré du niveau 2.
- [ ] Aucune régression sur le niveau 3 (`CT_APE`) déjà fonctionnel, tests `CodeActiviteResolverTests`/
      `Task161CodeActiviteCascadeTests` rejoués verts.

## Dépendances / risques
- Dépend de l'arbitrage PO (§1) avant tout développement.
- Risque principal si non traité : la cascade TASK-161 continue de laisser croire (documentation, code
  commenté) qu'un mapping par tiers existe et fonctionne, alors qu'il ne s'est jamais déclenché chez
  aucun client réel depuis sa livraison — dette silencieuse sur une fonctionnalité déjà "DONE".

> ⚠️ **CORRECTIF ARCHITECTE (24/07/2026, 12h30) — l'affirmation ci-dessous était fausse, confirmée par
> un incident réel en production.** Log serveur fourni par le PO
> (`DeclaratifMaroc.out.log`) : l'absence de `SCAT_NumeroTiers` en base ne dégrade PAS silencieusement —
> elle lève une `SqlException` non catchée (`Nom de colonne non valide`) qui **crashe entièrement**
> `GetLignes`/le figeage (500 générique), rendant l'écran ③ Vérifier & Intégrer inutilisable pour ce
> client. Correctif ouvert séparément : [TASK-179](TASK-179-crash-sql-colonne-scat-numerotiers-manquante.md).
>
> ✅ **ARBITRAGE PO TRANCHÉ (24/07/2026, 12h35) : option (B) retenue** — le niveau 2 « défaut par
> tiers » est **retiré** de la cascade, pas réparé. Justification (analyse architecte, cf. §1
> ci-dessus, confirmée par le PO) : ce niveau n'a jamais fonctionné chez aucun client réel depuis
> sa livraison, dépend d'une table possédée par `apbs-gr_winform` que le PO refuse de faire modifier,
> et fait doublon avec le niveau 3 (`F_COMPTET.CT_APE`), déjà fonctionnel et sans dépendance externe.
> TASK-179 reformulée en conséquence : suppression du code plutôt que sécurisation défensive.
>
> ~~Aucune perte de donnée ni blocage : le code activité n'étant jamais bloquant (cascade niveau 4 = ""),
> ce défaut n'a provoqué aucun incident visible — c'est une fonctionnalité inerte, pas un bug qui casse
> quelque chose.~~ *(affirmation invalidée, voir ci-dessus)*
