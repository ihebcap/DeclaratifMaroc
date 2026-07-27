Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md`, puis
chaque fichier TASK listé ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Contexte

Trois signalements simultanés du PO (24/07/2026) sur la base de **production** cliente, écran
③ Vérifier & Intégrer — diagnostiqués et documentés par l'architecte (lecture de code, causes
confirmées, pas des hypothèses). Voir le bloc en tête de `D:\_vibe\GRF\TODO.md` pour le résumé.

## Ta mission

Traiter, dans cet ordre STRICT (TASK-175 conditionne le test réel des deux suivantes — inutile de
valider une resynchronisation en masse ou une validation d'incohérence sur un écran qui plante en
500 au chargement), les 3 TASKs suivantes du dossier `D:\_vibe\GRF\TASKS\` :

1. `TASK-175-500-chargement-lignes-verrou-om-non-capture.md` — **CRITIQUE, à faire en premier.**
   500 générique au lieu d'un 409 propre quand les deux domaines (Encaissement/Decaissement) se
   chargent en parallèle et entrent en contention sur le verrou `soId` de TASK-156.
2. `TASK-177-incoherence-validee-ignoree-controle-bloquant.md` — **HIGH.** Une incohérence validée
   par le PO (écran ②) continue de bloquer la clôture (écran ③) car `GetCheckupAsync` ne teste pas
   le flag `IncoherenceValidee`, contrairement à `RevaliderLignesFigeesAsync`.
3. `TASK-176-resynchronisation-en-masse-lignes.md` — **MEDIUM.** Aucun endpoint bulk vers
   `ResynchroniserLigneAsync` — le PO doit resynchroniser ligne par ligne (151 lignes dans son cas).

## Modèle / effort par TASK

Tu as accès à l'outil Agent (sous-agents). **Pour chaque TASK listée ci-dessus, ne l'exécute pas
toi-même en ligne — lance-la via l'outil Agent** avec le modèle indiqué ci-dessous, en lui donnant
le contenu complet du fichier TASK correspondant + le rappel des "Règles de travail" ci-dessous
dans le prompt de l'agent (chaque agent démarre sans mémoire de cette conversation, il doit être
autonome). Attends la fin de TASK-175 avant de lancer TASK-177/TASK-176 (dépendance de test, pas de
dépendance de code — mais respecte quand même l'ordre pour pouvoir vérifier chaque correctif en
conditions réelles sur un écran qui charge).

| Task | Modèle | Pourquoi |
|------|--------|----------|
| TASK-175 | `sonnet` | Un seul `try/catch` manquant, pattern déjà en place sur `Resynchroniser` (même fichier) — mécanique, bien cadré. |
| TASK-177 | `sonnet` | Une condition à répliquer, déjà éprouvée ailleurs dans le même service (`RevaliderLignesFigeesAsync:465`). |
| TASK-176 | `opus` | Nouveau contrat d'endpoint + risque de contention interne (traitement séquentiel obligatoire) à ne pas rater — plus de jugement de conception que les deux autres. |

Si l'outil Agent n'est pas disponible dans ta session (pas de sous-agents), ignore ce tableau et
traite les 3 TASKs toi-même dans le modèle de la session.

## Règles de travail (non négociables)

- Pour chaque TASK : vérifie d'abord que les champs Objectif/Files/Garde-fous/Validation sont bien
  remplis (ils le sont toutes les trois). Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`,
  implémente, compile, corrige les erreurs, puis écris `VERIFY/TASK-XXX_verify.md` en suivant le
  même niveau de détail que `VERIFY/TASK-144_verify.md` (déjà dans le dossier, sers-t'en de
  modèle) : section Périmètre livré, Fichiers modifiés, Checklist, **et une section "Reste à
  valider" honnête si tout n'a pas pu être vérifié dans ton environnement** — ne déclare jamais un
  point validé si tu ne l'as pas réellement vérifié.
- **Tu as un accès réel à la base de données** (comme utilisé pendant la session qui a produit ces
  TASKs) : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d GR_EMA_DISTRIBUTION -Q "..."` pour la base
  GRF, et `-d "NEW_EMA DISTRIBUTION"` pour la base Sage (société `SO_Id=1`). Les identifiants
  complets sont dans `D:\_vibe\GRF\connections.json`. **Utilise cet accès pour vérifier réellement
  chaque correctif** (rejeu des cas cités dans chaque TASK — notamment `FC2501717`/`RF26040040` et
  `FC2501667`/`RF26030075` pour TASK-177), pas seulement en te fiant à la compilation.
- Build back (`dotnet build DeclarationTVA.slnx`) et build front (`cd declaration-tva-web && npx
  tsc -b && npx vite build`) doivent passer avant d'écrire un VERIFY.
- Pour TASK-175 : reproduis réellement la course (deux appels concurrents `GET {id}/lignes` sur le
  même `soId`) avant/après le correctif — un test unitaire ou une vérification manuelle à deux
  onglets, pas seulement une relecture de code. Vérifie aussi le point §2 de la TASK
  (`ReintegrerReglementsLiberesAsync` a-t-il la même lacune ?) — documente ce que tu trouves même
  si tu ne le corriges pas dans cette TASK.
- Pour TASK-176 : le traitement du bulk doit être **séquentiel**, jamais parallèle en interne (le
  garde-fou est explicite dans la TASK) — vérifie-le au build ET en le lisant dans le diff avant de
  committer.
- Pour TASK-177 : n'oublie pas de chercher d'autres emplacements générant une alerte à partir de
  `MotifRejet` sans tester `IncoherenceValidee` (§Garde-fous de la TASK) — documente ce que tu
  trouves dans le VERIFY, corrige-les si tu en trouves.
- Committe chaque TASK terminée **individuellement** (un commit par TASK, message clair décrivant
  le correctif), jamais de `--no-verify`, **ne jamais pousser (`git push`)** — les commits restent
  locaux pour revue.
- Ne touche à aucun fichier hors du périmètre `Files` listé dans la TASK en cours. Pas de refactor,
  pas de nettoyage non demandé.
- Respecte toutes les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors couche
  repository, pas de secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse
  — documente tout compromis dans le VERIFY).
- Si un point d'une TASK est réellement ambigu ou nécessite une décision métier que tu ne peux pas
  prendre seul (ex. TASK-177 §Objectif dernier point — visibilité de la validation à l'écran ③/④) :
  **arrête-toi sur ce point précis, documente-le clairement dans le VERIFY** plutôt que d'inventer
  une réponse, et continue le reste de la TASK.

## À la fin

Laisse les 3 TASKs traitées dans `VERIFY/` (ne les déplace pas toi-même vers `DONE_DETAIL/` — c'est
le rôle de l'architecte de les reviewer et de faire la clôture). Si tu n'as pas eu le temps de
toutes les traiter, laisse les suivantes dans `TASKS/` sans y toucher et indique dans un résumé
final quelles TASKs ont un VERIFY prêt pour revue et lesquelles restent à faire.
