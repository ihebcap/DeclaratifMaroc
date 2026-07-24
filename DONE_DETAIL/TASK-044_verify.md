# TASK-044 — VERIFY (clôture exceptionnelle PO, 23/07/2026)

> Ce fichier n'est **pas** un VERIFY standard. Aucun VERIFY n'a jamais été soumis pour TASK-044.
> Il documente, pour la traçabilité, une **clôture par décision PO explicite assumant le risque**,
> après refus d'approbation normale par l'architecte (dossier de preuve incomplet).

## Ce qui a été vérifié réellement (lecture directe du code source par l'architecte)

| Point | Constat | Preuve |
|---|---|---|
| Front servi sur `GET /` | ✅ Corrigé | `Declaration.API/Program.cs:128-129` (`UseDefaultFiles()`+`UseStaticFiles()`) + `:162` (`MapFallbackToFile`) — livré sous TASK-116 |
| `WorkerExePath` relatif | ✅ Conforme | `Declaration.Application/Services/DeclarationWorkflowService.cs:739-743` (`Path.IsPathRooted` + `Path.Combine(AppContext.BaseDirectory, ...)`) |
| Logs relatifs à l'exe | ✅ Conforme | `Declaration.Application/Services/DeclarationWorkflowService.cs:132-136` (`logs/valorisation.log` sous `AppContext.BaseDirectory`) |
| Script de publication | ✅ Existe | `Deploy-All.ps1` (racine) — build front + `dotnet publish` API self-contained win-x64 + workers Sage + `Publish-Setup.ps1`, fusionné avec TASK-115 (décision PO 18/07/2026) |

## Ce qui N'A PAS été vérifié — assumé explicitement par le PO en clôturant quand même

- **`connections.json.exemple`** (gabarit de déploiement sans secret réel) : absent du dépôt, jamais livré.
- **`DOCS/DEPLOIEMENT.md`** : non corrigé, affirme toujours *« TASK-044 n'a pas livré de `publish.ps1`
  unique à ce jour »* — contredit par l'existence de `Deploy-All.ps1`. Incohérence documentaire
  laissée en l'état.
- **Aucune preuve runtime** : pas de service Windows installé/démarré hors arbo de build dans cette
  session, pas de `GET /` ni `GET /api/...` rejoués en même origine, pas de trace confirmée d'un
  worker invoqué en relatif dans `logs/valorisation.log` sur une instance réellement déployée.
- **Aucun compte de service vérifié** (droits Sage OM + SQL), aucun démarrage automatique testé.
- La TASK n'est jamais passée par `IN_PROGRESS/` ; aucun cycle normal TASK→VERIFY→DONE n'a eu lieu.

## Décision

Le PO, informé de ces manques par l'architecte (échange du 23/07/2026), a demandé explicitement de
clôturer quand même en assumant le risque (« j'assume et je clôture »). L'architecte a exécuté la
clôture demandée **sans la présenter comme une vérification réussie** — cf. note en tête de
`DONE_DETAIL/TASK-044-deploiement-mono-service-mono-dossier.md` et entrée `DONE.md`.

**Risque résiduel pour un futur déploiement réel** : le premier déploiement en production devra
vérifier from scratch les points non couverts ci-dessus (gabarit connections.json, doc à jour,
service installé avec compte Sage+SQL fonctionnel).
