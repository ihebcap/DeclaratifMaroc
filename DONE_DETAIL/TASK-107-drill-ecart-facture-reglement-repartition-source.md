# TASK-107 — Localiser l'écart jusqu'à la facture / au règlement (drill depuis « Répartition par source »)

> ✅ **APPROUVÉE (17/07/2026)** — après correctif TASK-108, arbitrage PO : **option 3** retenue
> (renvoi vers le drill filtré existant TASK-016/`DomainGrid`, front-only). Voir `DONE.md`,
> `CHANGELOG.md` et `DONE_DETAIL/TASK-107_verify.md`. Fiche conservée ci-dessous telle que rédigée
> avant arbitrage, à titre d'historique.

> ⛔ **BLOQUÉE par TASK-108 — à réévaluer après.** Le micro-cadrage architecte (17/07/2026) a
> établi que l'« écart » affiché à l'étape ② n'est **pas** un déséquilibre : c'est la TVA totale
> mal étiquetée (artefact d'ensembles, cf. TASK-108). Tant que ce n'est pas corrigé, « localiser
> l'écart » est **mal posé** — l'écart n'a aucune pièce coupable, il *est* la TVA totale.
> Après TASK-108, seul un **vrai** résidu `TTC ≠ HT+TVA` subsistera ; cette fiche se réduira alors
> probablement à un drill vers les **lignes réellement incohérentes** (option 2 ci-dessous), à
> confirmer avec le PO. Ne rien coder ici avant TASK-108.

> **Origine** : test PO 17/07/2026 sur l'écran ② « Vérifier & Intégrer » — scepticisme PO sur
> TASK-103 : « je suis pas convaincu, il n'a pas affiché la facture ou bien le règlement ». TASK-103
> a rendu visible le tableau « Répartition par source » en pré-intégration (✅ approuvée), mais ce
> tableau **agrège par source** (Espece / Decaissement / Encaissement…) : il situe l'écart *par
> source*, jamais jusqu'à la **facture** ou au **règlement** qui le compose.

## Contexte

Le contrôle « Cohérence des totaux déclarés » (écran ②) affiche, en cas d'écart :
- le **badge** d'écart global (ex. 1 480,50 MAD) ;
- le tableau **« Répartition par source »** (`RecapSourceTable`, alimenté par `recapSource` —
  TASK-087/103) : une ligne par source, colonnes HT / TVA / TTC.

Ce niveau permet de dire *quelle source* porte l'écart, mais pas *quelle pièce*. Pour un comptable
(cf. modèle 4-interrogations, mémoire `grf-modele-comptable-4-interrogations` ; drill à la demande
TASK-092), l'attente exprimée est de **descendre** de la source vers la ou les factures/règlements
responsables — sans quitter l'écran ni figer la déclaration.

Le socle de drill existe déjà et est réutilisable :
- `AffectationsDrill.tsx` (drill plein écran à la demande depuis ① — TASK-092), grille par
  facture × taux avec n° de règlement (`RC…`) et n° de facture (`FC…`).
- Mécanisme drill-down anomalie → grille filtrée de TASK-016 (`DomainGrid` filtré).
- `refLigne` déjà remonté par l'API `/checkup` (TASK-088) sur les alertes.

## Point à cadrer avec le PO (avant rédaction du périmètre définitif)

« Localiser l'écart » peut vouloir dire plusieurs choses — **à trancher** :
1. **Drill depuis une ligne de source** → liste des factures/règlements de cette source
   contribuant aux totaux (ventilation descendante).
2. **Écart = différence** entre deux ensembles (totaux déclarés vs rapproché/attendu) : identifier
   *les pièces qui expliquent la différence* (plus riche, plus complexe — suppose de définir le
   terme de comparaison exact de l'écart).
3. Simple **renvoi** vers le drill affectations existant (`AffectationsDrill`) pré-filtré sur la
   source cliquée (option la plus légère, réutilise TASK-092 sans nouveau back).

Recommandation architecte : commencer par **l'option 3** (valeur immédiate, front-only, réutilise
l'existant), et n'ouvrir l'option 1/2 que si le PO juge le renvoi insuffisant.

## Périmètre (à finaliser après arbitrage PO)

- **Inclus (hypothèse option 3)** :
  - Rendre les lignes de `RecapSourceTable` actionnables (clic sur une source) → ouverture du drill
    affectations (`AffectationsDrill`) pré-filtré sur la source, à la demande, sans figer.
  - Traçabilité honnête : si une pièce ne peut être rattachée, le montrer (pas de ligne muette).
- **Exclu** :
  - Le **calcul de l'écart** lui-même (inchangé — déjà correct, cf. TASK-103).
  - Toute modification de la valorisation ou de la sélection.
  - L'ouverture de l'écart en **édition** (le drill reste en lecture ; l'engagement reste l'acte ④).

## Objectif

```
Entrée  : écran ② en écart, tableau « Répartition par source » affiché (TASK-103)
Traitement : depuis une source, drill à la demande vers les factures/règlements qui la composent
Sortie  : le comptable atteint la (les) pièce(s) FC…/RC… derrière la source, sans figer ni quitter
```

## Livrables (indicatifs, à confirmer)

- Front : `RecapSourceTable` / `VerifierIntegrerPanel.tsx` — action de drill par source réutilisant
  `AffectationsDrill` (TASK-092) ou le drill filtré TASK-016.
- Le cas échéant (option 1/2), un enrichissement back du `/checkup` pour exposer la ventilation
  descendante par pièce (à spécifier seulement si l'option 3 est jugée insuffisante).
- `VERIFY/TASK-107_verify.md` : preuve sur un cas réel multi-pièces montrant qu'on atteint la
  facture/le règlement derrière une source en écart.

## Critères de validation

- Depuis le tableau « Répartition par source » en écart, on atteint la/les pièce(s) FC…/RC…
  responsable(s), à la demande, sans figer la déclaration.
- Aucune régression sur l'affichage TASK-103 (le tableau reste correct quand on ne drille pas).
- Le calcul de l'écart et la valorisation restent strictement inchangés.

## Risques / dépendances

- **Dépend de TASK-106** pour être pleinement démontrable sur l'espèce : tant que les règlements
  espèce ne se valorisent pas, une source `Espece` en écart n'a pas de pièce à exhiber.
- Réutilise `AffectationsDrill` (TASK-092) et le drill filtré (TASK-016) — pas de nouveau socle si
  l'option 3 est retenue.
- Nécessite un **arbitrage PO** sur le sens exact de « localiser l'écart » (options 1/2/3
  ci-dessus) avant de figer le périmètre — ne pas coder avant.
