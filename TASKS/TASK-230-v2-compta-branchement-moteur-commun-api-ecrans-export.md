# TASK-230 — V2 compta : déclaration définitive depuis le fichier importé (moteur commun, lignes déclarées, XML)

Status: 🆕 à faire (révisée le 16/10/2026 : le flux passe par l'aller-retour Excel)
Priority: HIGH
Risk: CRITICAL (produit la déclaration déposée ; épisode TASK-224)
Module: Declaration.Compta.* + Declaration.Application + Declaration.API + declaration-tva-web
Dépend de : TASK-237 (lignes validées issues du fichier), TASK-228, TASK-229, TASK-234 ; front = TASK-231 + TASK-232

> **Origine :** PO 16/10/2026 — la V2 **aide au maximum** mais ne livre pas la déclaration définitive seule : le fichier Excel complété par l'utilisateur et ré-importé (TASK-236/237) est la **source de la déclaration définitive**. La partie commune (moteur, exports XML/Excel) ne change pas.

## Décisions de conception

1. **Entrée = lignes confirmées par TASK-237** (statut `Acceptée`, décision `Inclure`), pas un recalcul direct depuis la compta. Aucun chemin « déclarer sans fichier ».
2. **Adaptateur de source** : transforme ces lignes en lignes candidates du moteur existant (`ConstructeurDeclaration`, `LigneCandidate`) **sans modifier `Declaration.Core`**. Si une modification du moteur commun s'avère nécessaire : **s'arrêter et signaler** (règle PO).
3. **Idempotence structurelle** (leçon TASK-224) : `LigneDeclareeCompta` avec index unique (RèglementCbMarq, FactureCbMarq, TauxCode, DossierId), **écrit uniquement à la déclaration définitive** (pas à l'export ni à l'import). Garde-fou bloquant si une facture est déclarée pour plus de 100 % de sa TVA.
4. **Lignes `Reporter`** : restent disponibles pour les périodes suivantes (TASK-227 §6). **Lignes `Exclure`** : tracées avec motif, ne sont pas redéclarées automatiquement.
5. **Identité fiscale** : IF/ICE validés (TASK-237), repris du fichier ; mêmes règles Maroc que V1.
6. **Cycle de vie** : Export (TASK-236) → Import + prévisualisation (TASK-237) → **Déclaration définitive** (cette TASK : confirmation, clôture, génération XML, dépôt marqué) ; verrous V1 conservés. Jamais de mélange V1/V2 pour une même période : bascule explicite, tracée (à confirmer PO).
7. **Exports** : décaissement (+ dépenses, frais) → XML Simpl-TVA « relevé de déductions » existant + Excel de contrôle final ; **encaissement → Excel uniquement** (schéma XML DGI introuvable, CADRAGE §7.8).
8. **Traçabilité** : la déclaration définitive référence `ExportId` et l'identifiant d'import ; le fichier importé est archivé (copie intégrale) pour audit.
9. Écran : assistant en 3 étapes (Exporter → Importer/Contrôler → Déclarer), avec XGrid, KpiCard et tags de la charte xGR ; `ApbsGrid` interdit sur les écrans V2.

## Périmètre STRICT

- **Inclus** : adaptateur, persistance des lignes déclarées, confirmation/clôture, génération XML et Excel final (réutilisation), archivage du fichier importé, assistant en 3 étapes, tests d'orchestration sur fixtures + scénarios LOTREX et NEW_EMA.
- **Exclu** : DDP V2 (TASK à part), migration des déclarations V1, écriture dans Sage, avoirs.

## Livrables

- Adaptateur + API + assistant + XML/Excel branchés + scénarios de test manuel (`DOCS/`, epic CRITICAL UX).

## Critères de validation

- Chaîne complète sur NEW_EMA (décaissement) et LOTREX (décaissement + encaissement) : total déclaré = Σ des lignes `Inclure` du fichier importé, rejeu SQL indépendant écart 0,00.
- Double confirmation/réimport/relance ⇒ 0 doublon (requête de preuve + test automatisé).
- Facture réglée en 2 fois ⇒ 2 lignes proratisées, jamais 4 (cas TASK-224).
- Lignes `Reporter` réapparaissent à l'export de la période suivante ; lignes déclarées n'y figurent plus.
- XML conforme au schéma existant, structure identique à la V1 sur un cas commun ; `Declaration.Core` : aucun diff (`git diff --stat` joint).
- Build solution 0 erreur, tests verts, `npm run lint`/`build` 0 erreur, **checklist UI cochée**, preuve par critère datée dans le VERIFY.

## Files

- `Declaration.Compta.*`, `Declaration.Application/Services/DeclarationWorkflowService.cs` (lecture, extension minimale), `Declaration.API/Controllers/`, `declaration-tva-web/src/` (lire `DOCS/UI_STANDARDS.md`).
- `VERIFY/TASK-224_verify.md` (patron idempotence et garde-fou).
