# TASK-072 — Incohérence Σ(HT+TVA) vs Σ(montant rapproché) à l'écran ③ Calcul TVA

Status: TODO
Priority: HIGH
Risk: HIGH
Module: Front `declaration-tva-web` (écran ③ Calcul TVA) + vérification back valorisation

## OBJECTIF
Investiguer puis corriger l'écart constaté par le PO : sur une sélection de 68 règlements (① Règlements), total rapproché = 1 193 158,70 MAD, mais l'écran ③ Calcul TVA affiche HT = 2 748 914,90 MAD et TVA = 488 087,05 MAD (Σ HT+TVA ≈ 3 237 001,95 MAD, ×2,7 le montant rapproché).

Par construction métier (régime TVA sur décaissement, `MODULE_DECLARATION_TVA.md` §2 : `ratio = TotalTTC_facture / montant_affecté`, `assiette = base_taxe / ratio`), chaque ligne vérifie `assiette + tva ≈ ttc` et le `ttc` par affectation est calé sur le montant réellement payé. **Σ(HT+TVA) des lignes valorisées doit donc converger vers Σ(montant rapproché/affecté) des règlements sélectionnés**, aux arrondis près — pas être 2,7x supérieur.

Piste principale identifiée par l'architecte (à confirmer avant de corriger, ne pas corriger à l'aveugle) : dans `CalculTvaPanel.tsx:186-199`, la fonction boucle sur **chaque règlement de `selectedRows`** et filtre indépendamment `allLignes` par égalité stricte `l.numeroRapprochement === r.numeroReglement`, puis aplatit (`Object.values(mappedData).flat()`) sans dédoublonnage global. Si plusieurs règlements sélectionnés partagent une valeur de `numeroReglement` identique ou vide/`undefined`, les mêmes lignes back sont alors comptabilisées **une fois par règlement correspondant**, gonflant artificiellement HT/TVA. Autre piste à ne pas exclure : doublons ou non-filtrage côté back (`GetLignesAsync`) sur les lignes déjà rattachées à un `numeroRapprochement`.

## BUSINESS VALUE
Un montant de TVA déclaré faux (ici potentiellement surdéclaré) est un risque fiscal direct auprès de la DGI si l'écart n'est pas détecté avant clôture/dépôt — impact direct sur un environnement de production réel.

## CONTRAINTES
- Ne pas modifier la logique métier de valorisation (`Ventilateur`, prorata TASK-004) sans preuve exacte de la cause — commencer par confirmer/infirmer la piste ci-dessus avec les données réelles du cas PO avant tout correctif.
- Analyser le back (`GetLignesAsync`, unicité par `numeroRapprochement`) ET le front (`fetchAllLignes`/`entries` dans `CalculTvaPanel.tsx`) avant de conclure.
- Fournir une preuve chiffrée réelle reproduisant le cas PO (68 règlements) — pas seulement un correctif spéculatif.
- Lecture seule stricte, aucun impact Sage ni écriture RT_*.

## FILES
- `declaration-tva-web/src/CalculTvaPanel.tsx` (L127-149 `fetchAllLignes`, L186-199 `entries`/agrégation par règlement, L68-101 `agregParFactureTaux`)
- `Declaration.Application/Services/DeclarationWorkflowService.cs` (`GetLignesAsync` — origine de `NumeroRapprochement`/valorisation par affectation)
- `declaration-tva-web/src/ReglementsSelection.tsx` (origine de `numeroReglement` sélectionné en ①)

## VALIDATION
- [ ] Build OK
- [ ] Cause racine identifiée et documentée avec preuve chiffrée réelle sur le cas PO (68 règlements / 245 lignes)
- [ ] Après correctif : Σ(HT+TVA) des lignes valorisées ≈ Σ(montant rapproché) des règlements sélectionnés (tolérance d'arrondi uniquement, pas un facteur ×2,7)
- [ ] Test de non-régression couvrant le cas de doublon identifié (ex. règlements partageant un `numeroReglement` vide/identique)
- [ ] Aucune régression sur ② Affectations / ⑤ Contrôle (mêmes données sources, mêmes agrégats attendus)

## ARCHITECTURE RULES APPLICABLES
- Aucune logique métier dans l'UI (`ARCHITECTURE.md` §5) — le calcul TVA reste back, le front n'agrège que ce qu'il reçoit ; si la cause est front, le correctif doit rester une correction d'agrégation/dédoublonnage, pas un recalcul.
- Pas de dette technique silencieuse — documenter la cause exacte trouvée, même si elle diffère de la piste indiquée.

## NOTES
Origine : signalé par le PO (13/07/2026) en même temps que TASK-071, sur le même écran/même déclaration test. Distinct de TASK-071 (qui bloque la clôture) : celui-ci porte sur l'exactitude du montant affiché en ③, qui alimente `onCalcSummary` → `totalTVA` remonté à ④ puis potentiellement déclaré. À traiter indépendamment mais avec les mêmes données de test si possible pour croiser les deux diagnostics.
