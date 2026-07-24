# TASK-174 — Récapitulatif TVA collectée/déductible par code activité (écran ③ Déclaration)

## Contexte
Demande PO (24/07/2026), formulée simplement : « on a déjà l'export XML, on a l'affectation des codes
activité, on affiche tout simplement le regroupement collecté et déductible par code activité, c'est
tout. » Confirmé par le PO : indépendant de TASK-172/173 (affectation en masse) — pas de dépendance dure,
mais TASK-172/173 amélioreront la fiabilité des codes affichés ici une fois livrées.

« Collecté » = TVA du domaine **Encaissement** (client). « Déductible » = TVA du domaine **Décaissement**
(fournisseur, y compris Espèce/Dépense/Frais bancaire qui restent des *sources* internes à ce domaine —
cf. `SourceAffectation` dans `Declaration.Core/Model.cs:16`).

## Constat (vérifié en lisant le code, pas supposé)
- Le modèle `RecapParActivite` existe **déjà** (`Declaration.Core/Model.cs:84-90` : `CodeActivite`,
  `TotalHT`, `TotalTva`, `TotalTtc`) et est **déjà calculé** par `ConstructeurDeclaration.cs:188`
  (`GroupBy(l => l.CodeActivite)`) — mais **uniquement groupé par code activité, sans distinguer le
  domaine**, et **uniquement consommé par l'export Excel** (`Declaration.Export.Excel/Exporter.cs:124` et
  `:296`, dans les deux feuilles Récap). **Jamais exposé côté API ni affiché dans le front.**
- L'endpoint `GET /api/declarations/{id}/checkup` (`DeclarationsController.cs:357-408`) calcule déjà,
  sur le **même ensemble de lignes** `lignesRecap` (Integree ∪ Proposee, les deux domaines Decaissement +
  Encaissement déjà concaténés ligne 363-365) :
  - `recapSource` (groupé par `Source`, ligne 383-394) ;
  - `recapTaux` (groupé par `{Taux, Domaine}`, ligne 396-408).
  Un `recapActivite` groupé par `{CodeActivite, Domaine}` suivrait **exactement le même patron**, sans
  nouveau calcul TVA — pure agrégation d'un champ (`CodeActivite`, déjà présent sur la ligne depuis
  TASK-161, exposé par `LigneCandidateDto.cs:112`).
- Front : `DeclarationFinalePanel.tsx` consomme déjà ce endpoint et affiche `recapSource` via le
  composant générique `RecapSourceTable.tsx` (prop `columnLabel` déjà prévue pour un axe autre que
  « Source », cf. TASK-112 qui l'a déjà réutilisé pour l'axe « lignes incohérentes »). **Le composant est
  directement réutilisable tel quel** pour cet écran — aucun nouveau composant de table nécessaire.

## Objectif
1. **Back** (`DeclarationsController.cs`, `GetCheckup`) : ajouter `recapActivite`, groupé par
   `{CodeActivite, Domaine}` sur `lignesRecap` (même ensemble que `recapSource`/`recapTaux`) :
   ```csharp
   var recapActivite = lignesRecap
       .GroupBy(l => new { CodeActivite = string.IsNullOrWhiteSpace(l.CodeActivite) ? "—" : l.CodeActivite, l.Domaine })
       .Select(g => new { codeActivite = g.Key.CodeActivite, domaine = g.Key.Domaine,
                           ht = g.Sum(x => x.HT), tva = g.Sum(x => x.TVA), ttc = g.Sum(x => x.TTC) })
       .OrderBy(x => x.codeActivite).ToList();
   ```
   (code activité vide → `"—"`, même traitement que `recapSource` ligne 384 — jamais masquer une ligne
   silencieusement).
2. **Front** (`DeclarationFinalePanel.tsx`) : nouvelle section « Récap par code activité », **deux**
   instances de `RecapSourceTable` réutilisé tel quel (`columnLabel="Code activité"`) :
   - une pour `recapActivite.filter(r => r.domaine === 'Encaissement')` → titre « Collecté (Encaissement) » ;
   - une pour `recapActivite.filter(r => r.domaine === 'Decaissement')` → titre « Déductible (Décaissement) » ;
   en passant `{ source: r.codeActivite, ht: r.ht, tva: r.tva, ttc: r.ttc }` (le composant attend
   `RecapLigneSource.source`, ne renomme pas la prop). Pas de drill (`onRowClick` omis) — simple lecture,
   conforme à la demande « c'est tout ».
3. Mettre à jour l'interface TypeScript `CheckupData` (`DeclarationFinalePanel.tsx:44-57`) avec
   `recapActivite: { codeActivite: string; domaine: string; ht: number; tva: number; ttc: number }[]`.
4. **Nettoyage nav (décision PO 24/07/2026)** : supprimer l'entrée de menu de gauche « Relevé de
   déductions » (`App.tsx:54`, `status: 'soon'`) ainsi que le bloc `Placeholder` associé
   (`App.tsx:311-316`) et le `import { Receipt }` s'il devient inutilisé. Cette entrée était un
   placeholder vide — la fonctionnalité qu'elle devait porter (export + récap) vit déjà entièrement dans
   « Déclaration TVA » → étape ③, elle n'a jamais mené à un écran réel. Suppression pure, aucune
   fonctionnalité perdue.

## Garde-fous
- Lecture seule stricte, **aucun recalcul de TVA** — pure agrégation d'un champ déjà valorisé.
- Ne pas filtrer/masquer les lignes sans code activité : les regrouper sous `"—"` et les afficher (permet
  justement de voir d'un coup d'œil ce qu'il reste à affecter — utile en synergie avec TASK-172/173).
- Ne pas introduire de nouvelle valeur de `Domaine` en dur ailleurs que `"Encaissement"`/`"Decaissement"` —
  ce sont les deux seules valeurs produites par `GetLignesAsync(id, "Decaissement"/"Encaissement", ...)`
  (lignes 363-364) ; si une 3ᵉ valeur apparaissait un jour, elle tomberait dans aucune des deux tables sans
  message — **à vérifier en base réelle avant de considérer le clivage Encaissement/Decaissement exhaustif**
  (déclaré comme risque non bloquant ci-dessous, pas silencieusement supposé complet).

## Files
- [Declaration.API/Controllers/DeclarationsController.cs](../Declaration.API/Controllers/DeclarationsController.cs) (`GetCheckup`, ajout `recapActivite`).
- [declaration-tva-web/src/DeclarationFinalePanel.tsx](../declaration-tva-web/src/DeclarationFinalePanel.tsx) (interface `CheckupData`, 2 nouvelles instances de `RecapSourceTable`).
- [declaration-tva-web/src/RecapSourceTable.tsx](../declaration-tva-web/src/RecapSourceTable.tsx) — réutilisé sans modification.
- [declaration-tva-web/src/App.tsx](../declaration-tva-web/src/App.tsx) (retrait entrée menu + `Placeholder` « Relevé de déductions », §Objectif 4).

## Validation
- [ ] Build back + front OK.
- [ ] `dotnet test` : aucune régression sur les tests existants du checkup (`Task*Tests.cs` pertinents).
- [ ] Vérification réelle sur une déclaration mixte (lignes Décaissement **et** Encaissement, plusieurs
      codes activité dont au moins une ligne sans code) : les deux tables affichent les bons totaux,
      la ligne sans code apparaît sous `"—"`, aucune ligne perdue (Σ des deux tables = `recapSource` total).
- [ ] Confirmer en base qu'aucune 3ᵉ valeur de `Domaine` n'existe sur les lignes d'une déclaration réelle
      (sinon ces lignes seraient invisibles dans les deux tables — à signaler, pas à masquer).
- [ ] Menu de gauche : entrée « Relevé de déductions » absente, aucune section vide/orpheline laissée
      dans `SECTIONS`, build front 0 erreur (import `Receipt` retiré si devenu inutilisé).

## Dépendances / risques
- **Indépendant de TASK-172/173** (confirmé PO) : cet écran affiche les codes activité tels
  qu'affectés aujourd'hui, quelle que soit leur fiabilité. Il bénéficiera mécaniquement de TASK-172/173
  (moins de codes incohérents/mélangés) sans qu'aucune des deux tâches ne bloque l'autre.
- Risque nul sur le calcul TVA (aucun chemin de calcul touché, uniquement une nouvelle projection en
  lecture sur des données déjà valorisées et déjà exportées en Excel).
