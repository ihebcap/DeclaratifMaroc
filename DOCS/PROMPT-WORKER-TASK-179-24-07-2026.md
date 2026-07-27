Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md`, puis le
fichier TASK ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Contexte

Incident réel en production (24/07/2026) : l'écran ③ Vérifier & Intégrer est entièrement inutilisable
chez un client — `GetLignes` plante en 500 (`SqlException: Nom de colonne non valide :
'SCAT_NumeroTiers'`). Diagnostic architecte confirmé par log serveur + arbitrage PO tranché le jour
même : le niveau « défaut par tiers » de la cascade code activité (TASK-161) n'a jamais fonctionné chez
aucun client réel et doit être **retiré**, pas réparé. Voir le bloc TODO.md (section 🔴 CRITIQUE 500) et
la note d'arbitrage dans `TASKS/TASK-171-cascade-code-activite-mapping-tiers-jamais-fonctionnel.md`.

## Ta mission

Traiter **TASK-179-crash-sql-colonne-scat-numerotiers-manquante.md** (dossier `D:\_vibe\GRF\TASKS\`) —
seule task de ce lot, priorité HIGH, à traiter aujourd'hui.

Vérifie d'abord que les champs Objectif/Files/Contraintes/Validation sont bien remplis (ils le sont).
Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les erreurs, puis écris
`VERIFY/TASK-179_verify.md` en suivant le même niveau de détail que `VERIFY/TASK-144_verify.md` (sers-t'en
de modèle) : section Périmètre livré, Fichiers modifiés, Checklist, **et une section "Reste à valider"
honnête si tout n'a pas pu être vérifié** — ne déclare jamais un point validé si tu ne l'as pas réellement
vérifié.

## Règles de travail (non négociables)

- **Portée exacte** : retirer le niveau 2 de `CodeActiviteResolver.Resoudre` (mapping par tiers) et les
  deux méthodes qui l'alimentent (`ChargerMappingCodeActiviteTiersAsync`,
  `GetMappingCodeActiviteTiersAsync`), plus leurs 2 sites d'appel dans `DeclarationWorkflowService.cs`
  (~311 et ~576). Ne touche à aucun autre fichier hors de la liste `Files` de la TASK. Pas de refactor,
  pas de nettoyage non demandé.
- **Ne jamais toucher** `apbs-gr_winform` ni la table `P_SOCIETECODEACTIVITETIERS` elle-même (rappel PO
  explicite, TASK-171) — tu supprimes uniquement le code GRF qui la LIT, jamais un script sur cette table.
- La cascade doit rester à 3 niveaux fonctionnels après ton changement : surcharge manuelle par ligne →
  `F_COMPTET.CT_APE` → "". Vérifie que `SelectionExpliqueeEvaluator.cs:135` et
  `SelectionnerAffectationsService.cs:115` (les deux autres appelants de `Resoudre`) compilent toujours
  avec la nouvelle signature — ils n'utilisaient déjà que les niveaux 3/4, en principe rien à changer
  côté appel sauf si la signature perd des paramètres qu'ils passaient.
- **Tu as un accès réel à la base de données** : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d
  GR_EMA_DISTRIBUTION -Q "..."` pour la base GRF (identifiants complets dans
  `D:\_vibe\GRF\connections.json`). Utilise-le pour confirmer, avant et après ton changement, qu'aucune
  requête GRF ne référence plus `P_SOCIETECODEACTIVITETIERS`/`SCAT_NumeroTiers`/`SCAT_ErpIntitule` (grep
  de contrôle sur le code + test réel de chargement de lignes sur une déclaration réelle de cette base).
- Build back (`dotnet build DeclarationTVA.slnx`) ET tests (`Declaration.Core.Tests`,
  `Declaration.Orchestration.Tests`) doivent passer avant d'écrire un VERIFY. Adapte
  `CodeActiviteResolverTests.cs` et `Task161CodeActiviteCascadeTests.cs` pour retirer les cas de test du
  niveau 2 retiré, sans supprimer la couverture des niveaux 1/3/4 restants.
- Corrige aussi `DONE_DETAIL/TASK-161-code-activite-defaut-tiers-surcharge-ligne.md` et
  `DONE_DETAIL/TASK-161_verify.md` pour ne plus décrire un niveau 2 qui n'existe plus (dette
  documentaire signalée par TASK-171, à clore définitivement ici).
- Committe le correctif **en un seul commit** pour cette TASK, message clair, jamais `--no-verify`,
  **ne jamais pousser (`git push`)** — le commit reste local pour revue.
- Respecte les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors repository, pas de
  secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse — documente tout
  compromis dans le VERIFY).
- Si un point est réellement ambigu (ex. un appelant de `Resoudre` utilise encore `tiersNumero`/
  `tiersNom` pour un usage que tu ne comprends pas) : **arrête-toi sur ce point précis, documente-le
  clairement dans le VERIFY** plutôt que d'inventer une réponse.

## À la fin

Laisse la TASK traitée dans `VERIFY/` (ne la déplace pas toi-même vers `DONE_DETAIL/` — c'est le rôle de
l'architecte de la reviewer et de faire la clôture). Si tu n'as pas eu le temps de la terminer, indique
dans un résumé final l'état exact d'avancement et ce qu'il reste à faire.
