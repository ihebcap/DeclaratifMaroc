# TASK-054 — Écran ① Règlements (sélection dans la déclaration)

> Étape ① du tunnel (TASK-053). Front (+ réutilisation endpoint back existant). Densité **0 espace perdu**. Règlement-first : c'est le point d'entrée métier.

## Contexte
Premier écran de travail du tunnel : lister les **règlements décaissés** de la période de la déclaration et permettre leur **sélection multiple** avant analyse TVA (`reflexion dectva.md` §2). L'endpoint rapprochement règlement-pivot existe déjà (`GET /api/rapprochement`, TASK-036, filtré `MV_Domaine IN (0,1)` via TASK-039) et l'écran d'interrogation `RapprochementInterrogation.tsx` en fournit le patron de grille dense. Ici on le **scope à la déclaration** et on rend la sélection **actionnable** (vers ②).

## Périmètre STRICT
- **Inclus** : grille dense pivot règlement (N°/date/mode/montant/affecté/statut), sélection multiple + total vivant, filtres (mode, période rapprochement, fournisseur, état), statuts `🟢 Éligible / 🟠 À contrôler / 🔴 Bloqué` **jamais masqués** (aucune ligne silencieuse).
- **Exclu** : le drill affectation (② = TASK-055) ; la valorisation TVA (③) ; toute écriture autre que le figeage de sélection déjà couvert back (TASK-017).

## Objectif
```
Entrée : declarationId + période → règlements décaissés (MV_Domaine IN (0,1))
Traitement : afficher/filtrer/sélectionner, calculer le total sélectionné
Sortie : ensemble de règlements sélectionnés transmis à l'étape ②, statuts explicites
```

## Étapes
1. Câbler la grille sur l'endpoint rapprochement existant, scopé à la période de la déclaration (réutiliser la projection TASK-036 ; ne pas créer d'endpoint si le filtre période suffit).
2. Colonne « Affecté » parlante (« 2 factures », « partiel 60 % », « — », « fourn. sans ICE ») dérivée des champs existants (reste à affecter, conformité IF/ICE de TASK-027).
3. Statut 3 couleurs jamais filtré d'office : 🔴 reste visible dans la liste (promesse transparence, mémoire `grf-valorisation-tracabilite-blocage-om`).
4. Sélection multiple + total sélectionné en bandeau bas (fourni par le shell TASK-053).
5. Filtres serveur cohérents avec le compteur (mémoire TASK-040 : ne pas filtrer côté client la page seule).

## Livrables
- Écran ① (nouveau composant ou adaptation `Selection.tsx`/`RapprochementInterrogation.tsx`).
- `VERIFY/TASK-054_verify.md` : preuve réelle `GR_EMA_DISTRIBUTION` (règlements décaissés, 🔴 non masqués, total sélection exact), build + lint verts.

## Critères de validation
- Filtre `MV_Domaine IN (0,1)` respecté (pas de bordereau BORD, mémoire `grf-mv-domaine-mapping` + TASK-039) — **et pas** `MV_DECAISSE=1** (sinon trou ~465 factures, mémoire `grf-trou-selection-mv-decaisse`).
- Statuts 🟢🟠🔴 tous présents, aucun masquage silencieux.
- Total sélectionné exact = somme des lignes cochées ; compteur = filtres serveur.
- Lecture seule stricte hors figeage de sélection existant.

## Risques / dépendances
- Dépend de TASK-053 (shell) pour le bandeau/total et la transition vers ②.
- Réutilise TASK-036/039/040 (endpoint + filtres serveur) — ne pas les réimplémenter.
- Garde-fou : règlement-first ne doit pas réintroduire l'exclusion `MV_DECAISSE` (cf. critère ci-dessus).
- 🚫 **Anti-régression** : base obligatoire = **adapter** `RapprochementInterrogation.tsx` (grille dense déjà écrite), **interdiction de repartir d'une grille neuve**. L'endpoint `/api/rapprochement` et ses filtres serveur (036/039/040) sont **intouchables** — n'ajouter que la sélection/scope/total côté front. L'interrogation globale existante (TASK-037) doit rester fonctionnelle à l'identique.
