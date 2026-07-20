Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md`, puis
chaque fichier TASK listé ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Ta mission cette nuit

Traiter, dans cet ordre STRICT (certaines sont bloquantes sur les précédentes), les 8 TASKs
suivantes du dossier `D:\_vibe\GRF\TASKS\` :

1. `TASK-145-fix-sens-achat-vente-code-en-dur-facture-first.md` — **CRITIQUE, à faire en premier.**
2. `TASK-146-drill-anomalie-filtre-precis-par-ligne.md`
3. `TASK-147-recalcul-ligne-proposee-cache-perime.md` (dépend de TASK-144, déjà livrée — lis aussi
   `D:\_vibe\GRF\VERIFY\TASK-144_verify.md` pour le contexte)
4. `TASK-148-debounce-filtre-dates-ecran-factures.md`
5. `TASK-149-reaudit-lignes-non-valorisees-post-fix-145.md` — **NE PAS COMMENCER avant que TASK-145
   soit terminée, buildée et son VERIFY écrit.**
6. `TASK-150-traitement-lignes-non-valorisees-restantes.md` — **NE PAS COMMENCER avant que TASK-149
   soit terminée.**
7. `TASK-151-if-manquant-tiers-zf-food.md`
8. `TASK-152-desambiguisation-tiers-collision-do-numero.md` — **lis bien le garde-fou : cette TASK
   contient un arbitrage produit (systématique vs à la demande) que tu ne dois PAS trancher seul.
   Si tu l'atteins, documente les options dans le VERIFY et marque-la comme "en attente d'arbitrage
   PO" — ne code rien tant que le choix n'est pas fait pour toi.**

## Modèle / effort par TASK

Tu as accès à l'outil Agent (sous-agents). **Pour chaque TASK listée ci-dessus, ne l'exécute pas toi-même en ligne — lance-la via l'outil Agent** avec le modèle indiqué ci-dessous, en lui donnant le contenu complet du fichier TASK correspondant + le rappel des "Règles de travail" ci-dessous dans le prompt de l'agent (chaque agent démarre sans mémoire de cette conversation, il doit être autonome). Attends la fin d'un agent avant de lancer le suivant quand une dépendance existe (145→149→150).

| Task | Modèle | Pourquoi |
|------|--------|----------|
| TASK-145 | `opus` | Bug le plus subtil (cache, sémantique Sage OM, point 4 ambigu à trancher honnêtement) — sert de fondation aux autres. |
| TASK-146 | `sonnet` | Filtre bien cadré, mécanique. |
| TASK-147 | `sonnet` | Extension d'un pattern existant (`ResynchroniserLigneAsync`), bien documenté. |
| TASK-148 | `sonnet` | Fix front-end simple. |
| TASK-149 | `sonnet` | Audit/comptage, méthode déjà décrite (TASK-143). |
| TASK-150 | `opus` | Nécessite du jugement ligne par ligne, risque de décision hâtive à éviter. |
| TASK-151 | `sonnet` | Investigation bornée. |
| TASK-152 | `sonnet` | Ne doit de toute façon pas coder seule — juste documenter des options. |

Si l'outil Agent n'est pas disponible dans ta session (pas de sous-agents), ignore ce tableau et
traite toutes les TASKs toi-même dans le modèle de la session — dans ce cas, considère basculer
manuellement en Opus (`/model`) au moins pour TASK-145 et TASK-150 avant de les commencer, puis
revenir à Sonnet pour le reste.

## Règles de travail (non négociables)

- Pour chaque TASK : vérifie d'abord que les champs Objectif/Files/Garde-fous/Validation sont bien
  remplis (ils le sont toutes). Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente,
  compile, corrige les erreurs, puis écris `VERIFY/TASK-XXX_verify.md` en suivant le même niveau de
  détail que `VERIFY/TASK-144_verify.md` (déjà dans le dossier, sers-t'en de modèle) : section
  Périmètre livré, Fichiers modifiés, Checklist, **et une section "Reste à valider" honnête si tout
  n'a pas pu être vérifié dans ton environnement** — ne déclare jamais un point validé si tu ne l'as
  pas réellement vérifié.
- **Tu as un accès réel à la base de données** (comme utilisé pendant la session qui a produit ces
  TASKs) : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d GR_EMA_DISTRIBUTION -Q "..."` pour la base
  GRF, et `-d "NEW_EMA DISTRIBUTION"` pour la base Sage (société `SO_Id=1`). Les identifiants
  complets sont dans `D:\_vibe\GRF\connections.json`. **Utilise cet accès pour vérifier réellement
  chaque correctif contre les données réelles** (rejeu des cas cités dans chaque TASK), pas
  seulement en te fiant à la compilation. C'est ce que l'architecte a fait pendant le diagnostic —
  ne te contente pas de moins.
- Build back (`dotnet build`) et build front (`cd declaration-tva-web && npx tsc -b && npx vite
  build` ou équivalent) doivent passer avant d'écrire un VERIFY.
- **Ne jamais commencer une TASK marquée "bloquée" tant que sa dépendance n'a pas un VERIFY écrit.**
- **Ne clôture jamais TASK-144 toi-même** — elle a déjà un VERIFY, et le seul point restant
  (validation PO des libellés métier de `DiagnosticMotifMetier.cs`) ne peut être tranché que par le
  Product Owner humain. Si TASK-147 te fait modifier ce fichier, ajoute simplement les nouveaux
  libellés au même statut "en attente de relecture PO", ne les valide pas toi-même.
- Committe chaque TASK terminée **individuellement** (un commit par TASK, message clair décrivant le
  correctif), jamais de `--no-verify`, **ne jamais pousser (`git push`)** — les commits restent
  locaux pour revue.
- Ne touche à aucun fichier hors du périmètre `Files` listé dans la TASK en cours. Pas de refactor,
  pas de nettoyage non demandé.
- Respecte toutes les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors couche
  repository, pas de secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse
  — documente tout compromis dans le VERIFY).
- Si un point d'une TASK est réellement ambigu ou nécessite une décision métier que tu ne peux pas
  prendre seul (comme prévu explicitement dans TASK-150 et TASK-152) : **arrête-toi sur ce point
  précis, documente-le clairement dans le VERIFY, et passe à la TASK suivante** plutôt que
  d'inventer une réponse.

## À la fin de la nuit

Laisse toutes les TASKs traitées dans `VERIFY/` (ne les déplace pas toi-même vers `DONE_DETAIL/` —
c'est le rôle de l'architecte de les reviewer et de faire la clôture demain). Si tu n'as pas eu le
temps de toutes les traiter, laisse les suivantes dans `TASKS/` sans y toucher et indique dans un
résumé final quelles TASKs ont un VERIFY prêt pour revue et lesquelles restent à faire.
