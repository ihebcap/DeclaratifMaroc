Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md` (section
« 🆕 Nouveau périmètre — Délai de Paiement Maroc », PO 19/07/2026), le CDC source
`D:\_vibe\apbs-gr_winform\analayse\CDC-DELAI-PAIEMENT-MAROC.md` (lecture seule, analyse déjà faite),
puis **chaque fichier TASK ci-dessous en entier** avant de coder quoi que ce soit dessus. Ne suppose
jamais un contexte manquant.

## Contexte

Nouveau périmètre produit : **Délai de Paiement Maroc**, développement **neuf** sur la plateforme
GRF web (`Declaration.API`/React), même principe que le module TVA — **aucune réutilisation de
DLL/code WinForms**, `apbs-gr_winform` n'est lu qu'à titre d'analyse (CDC).

Décisions déjà actées par le PO (session du 19/07/2026, détaillées dans `TODO.md`) :
- Rapprochement bancaire (§3.3) réutilise le mécanisme déjà en place côté TVA (`RT_MOUVEMENT.MV_Point`/
  `MV_PointDate`, local GRF) — **pas** le mécanisme Sage de l'ancien module RAS suggéré par le CDC brut.
- Attestation de régularité fiscale (§3.4) **hors périmètre** de cette vague.
- **3 anomalies découvertes en lisant le code legacy**, absentes du §4 du CDC, à corriger (et non
  reproduire) dans le neuf : chevauchement de conventions incomplet, affectation partielle ignorée sur
  le bucket "échéance hors période non payée", `Depassement` structurellement constant sur ce bucket.
  Détail dans TASK-129/TASK-131 respectivement.
- **Aucun mécanisme anti-double-déclaration** dans le legacy → remplacé par un calcul **incrémental**
  (`Depassement` = écart depuis la dernière borne déjà déclarée, pas depuis l'échéance légale).
- Mesure du délai fournisseur (§3.3) : **pas d'écran dédié**, extension de l'écran Factures existant
  (facture-pivot, TASK-041), pas l'écran Rapprochement (règlement-pivot).
- Menu : nouvelle entrée **« Délai de paiement »** autonome (groupe DÉCLARATION), même niveau que
  « Déclaration TVA ».

## Contrainte NON NÉGOCIABLE — schéma de base de données

**Aucune modification de schéma sur une table possédée par `apbs-gr_winform`** (`P_SOCIETE`,
`F_COMPTET`, ou toute autre table déjà utilisée par l'application WinForms), **même une migration
manuelle coordonnée**. Seules des tables **neuves, préfixées `DM_*` (convention déjà établie côté
TVA), possédées et gérées uniquement par GRF**, peuvent être créées. Confirmé déjà cohérent avec les
TASKs telles que rédigées (TASK-128 crée `DM_PARAM_DELAIPAIEMENT_SOCIETE` ou nom conforme,
explicitement pour ne pas toucher `P_SOCIETE` ; TASK-129 réutilise `RT_CONVENTIONTIERS`, table déjà
possédée par GRF) — si en cours de développement une TASK semble nécessiter une colonne/contrainte
ajoutée à une table winform, **arrête-toi sur ce point précis, documente-le dans le VERIFY et
n'implémente rien qui y touche**, quand bien même ce serait idempotent ou réversible.

## Ta mission

Traiter **les 10 TASKs du périmètre DDP** (`D:\_vibe\GRF\TASKS\DDP-TASK-127...136`), **de bout en
bout et sans t'arrêter entre elles pour attendre une validation** — enchaîne automatiquement d'une
TASK à la suivante dès que ses dépendances sont satisfaites, jusqu'à épuisement de ce qui peut être
fait. Ne reviens vers l'utilisateur/l'architecte que si un point est réellement bloquant (décision
métier ambiguë, contrainte de schéma ci-dessus, ou dépendance non satisfaite) — dans ce cas,
documente-le dans le VERIFY correspondant **et continue avec les autres TASKs non bloquées** plutôt
que de t'arrêter complètement.

### Ordre de dépendance STRICT

```
127 (socle délai/échéance légale)
 ├─→ 128 (paramètre date de mise en route)
 │     └─→ 131 (sélection lignes hors délai + calcul incrémental) ── dépend de 127 ET 128
 ├─→ 129 (convention délai tiers, back) ──→ 130 (convention délai tiers, front)
 ├─→ 135 (mesure délai fournisseur, écran Factures) — indépendant du reste dès que 127 est fait
 └─→ 131 ──→ 132 (cycle de vie déclaration + contrôle IF/ICE) ──→ 133 (génération XML/ZIP)
                                                              └─→ 134 (front liste/fiche/sélection/contrôle) — dépend de 131+132+133
136 (menu) ──→ dépend de 130 ET 134 (dernière TASK, câble la navigation vers les deux)
```

Concrètement : fais **127 en premier, seul**. Une fois 127 vert (build+VERIFY), tu peux lancer en
parallèle 3 branches indépendantes : (a) 128 puis 131→132→133→134, (b) 129→130, (c) 135. Termine par
136 seulement quand 130 et 134 ont chacune un VERIFY.

### Parallélisation

Tu as accès à l'outil Agent (sous-agents). **Ne traite pas les TASKs toi-même en ligne — lance
chacune via l'outil Agent**, avec le modèle indiqué ci-dessous, en donnant à l'agent le contenu
complet du fichier TASK correspondant + le rappel intégral des « Règles de travail » ci-dessous
(chaque agent démarre sans mémoire de cette conversation, il doit être autonome). Respecte le
graphe de dépendance ci-dessus pour décider quand lancer quoi ; les 3 branches (a)/(b)/(c) peuvent
tourner en parallèle une fois 127 terminé. Si l'outil Agent n'est pas disponible dans ta session,
ignore ce mécanisme et traite les 10 TASKs toi-même, séquentiellement, dans l'ordre de dépendance.

| Task | Modèle | Pourquoi |
|------|--------|----------|
| 127 | `opus` | Socle légal (jours ouvrés, `P_JOURSREPOS`) — erreur ici se propage à toute la chaîne. |
| 128 | `sonnet` | Nouvelle table + paramètre simple, pattern déjà éprouvé (tables `DM_*` existantes). |
| 129 | `sonnet` | Back sur table déjà existante (`RT_CONVENTIONTIERS`), corrige un chevauchement déjà diagnostiqué. |
| 130 | `sonnet` | Front mécanique une fois le back 129 livré. |
| 131 | `opus` | Cœur métier : sélection + calcul incrémental anti-double-déclaration — le point le plus sensible de tout le périmètre. |
| 132 | `opus` | Cycle de vie déclaration + contrôle bloquant IF/ICE — jugement de conception (cf. TASK-132 pour les états). |
| 133 | `sonnet` | Génération XML/ZIP, structure legacy reprise à l'identique — mécanique une fois 132 stable. |
| 134 | `opus` | Plus gros morceau front (liste/fiche/sélection/contrôle), filtre de période à concevoir correctement. |
| 135 | `sonnet` | Extension ciblée d'un écran existant (2 colonnes), périmètre étroit. |
| 136 | `sonnet` | Câblage menu, trivial une fois 130/134 livrées. |

## Règles de travail (non négociables)

- Pour chaque TASK : vérifie d'abord que les champs Objectif/Files/Garde-fous/Validation sont bien
  remplis. Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les
  erreurs, puis écris `VERIFY/TASK-XXX_verify.md` en suivant le même niveau de détail que
  `VERIFY/TASK-144_verify.md` (déjà dans le dossier, sers-t'en de modèle) : section Périmètre livré,
  Fichiers modifiés, Checklist, **et une section « Reste à valider » honnête si tout n'a pas pu être
  vérifié dans ton environnement** — ne déclare jamais un point validé si tu ne l'as pas réellement
  vérifié.
- **Tu as un accès réel à la base de données** : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d
  GR_EMA_DISTRIBUTION -Q "..."` pour la base GRF (identifiants complets dans
  `D:\_vibe\GRF\connections.json`). Utilise-le pour vérifier réellement chaque migration (`DM_*`
  créée, idempotente si rejouée) et chaque comportement métier (calcul de délai, incrémental,
  contrôle IF/ICE) sur des données réelles, pas seulement en te fiant à la compilation ou à des tests
  synthétiques.
- Toute nouvelle table va dans `DeclarationTVA.sql` (ou un fragment de migration séparé si le fichier
  devient trop long — documente ton choix), **idempotente** (`IF NOT EXISTS`), jamais un `CREATE
  TABLE` brut qui échouerait au second rejeu. Respecte la contrainte de schéma en tête de ce document.
- Build back (`dotnet build DeclarationTVA.slnx`) et build front (`cd declaration-tva-web && npx
  tsc -b && npx vite build`) doivent passer, 0 erreur, avant d'écrire un VERIFY — à chaque TASK.
- Committe chaque TASK terminée **individuellement** (un commit par TASK, jamais un commit qui
  mélange plusieurs TASKs), message clair décrivant le correctif, jamais `--no-verify`, **ne jamais
  pousser (`git push`)** — les commits restent locaux pour revue architecte.
- Si plusieurs agents tournent en parallèle sur des branches différentes : **ne stage/commit que les
  fichiers de ton propre périmètre** (jamais `git add -A`), pour ne pas embarquer un fichier encore
  en cours de modification par un autre agent.
- Ne touche à aucun fichier hors du périmètre `Files` listé dans la TASK en cours. Pas de refactor,
  pas de nettoyage non demandé, pas d'anticipation d'une TASK suivante dans une TASK antérieure.
- Respecte toutes les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors couche
  repository, pas de secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse —
  documente tout compromis dans le VERIFY).
- **Un commit = une TASK déclarée** : ne noie jamais le correctif d'une TASK dans le commit d'une
  autre (dérive déjà constatée deux fois sur ce projet, cf. `DONE_DETAIL/TASK-158_verify.md`) — même
  si tu enchaînes 10 TASKs sans t'arrêter, chacune reste un commit strictement isolé.
- Si un point d'une TASK est réellement ambigu ou nécessite une décision métier que tu ne peux pas
  prendre seul (au-delà de la contrainte de schéma déjà couverte plus haut) : **arrête-toi sur ce
  point précis dans cette TASK, documente-le clairement dans son VERIFY** plutôt que d'inventer une
  réponse, et **continue avec les TASKs suivantes non bloquées** — ne stoppe jamais l'ensemble du
  run pour un seul point ambigu.

## À la fin

Laisse les 10 TASKs traitées dans `VERIFY/` (ne les déplace pas toi-même vers `DONE_DETAIL/` — c'est
le rôle de l'architecte de les reviewer et de faire la clôture). Si tu n'as pas eu le temps ou la
possibilité de toutes les traiter (dépendance bloquée, point ambigu), laisse les suivantes dans
`TASKS/` sans y toucher. Termine par un résumé final listant, pour chacune des 10 TASKs : ✅ VERIFY
prêt pour revue / ⏸️ bloquée (et pourquoi) / non commencée (et pourquoi).
