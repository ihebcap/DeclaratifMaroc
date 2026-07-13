# TASK-041 — Écran « Factures » (interrogation, filtre date obligatoire)

## Contexte
Deuxième entrée du menu **INTERROGATION** (voir TASK-035, aujourd'hui ⚪ placeholder), à côté de
« Rapprochement bancaire » (règlement-pivot, TASK-037). Ici le **pivot est la facture**.

Cadrage PO (09/07/2026) : la grille démarre par les colonnes
`N° facture · Date · Fournisseur (code + intitulé) · Référence · Total HT · Total TVA · Autre taxe ·
Écart · Escompte · TTC · Solde facture · statut de déclaration`. Une facture peut être **partiellement
déclarée** (la déclaration suit le paiement — TVA fournisseur Maroc = déduction au **décaissement**),
donc le statut n'est **pas** un booléen mais un **état calculé** à partir des règlements affectés.

> **Décision PO explicite** : cette task ne traite QUE l'**interrogation** (affichage + filtre date
> obligatoire). L'**option 2** (unité de sélection pour déclarer : facture vs règlement, cases à
> cocher, déclaration partielle actionnable) est **différée** — « on verra après ». Aucune écriture,
> aucun tampon `DT_Id`, aucune action de masse dans cette task.

## Le piège structurant — deux familles de colonnes
Les colonnes demandées n'ont **pas la même source ni le même coût** :

| Famille | Colonnes | Source | Coût |
|---|---|---|---|
| **A — identité/facture** | N° facture, Date, Fournisseur (code+intitulé), Référence, **TTC** (`Montant`), **Solde** | `RT_ECHEANCE` (SQL local) | gratuit |
| **B — valorisation TVA** | Total HT, Total TVA, Autre taxe, Écart, Escompte | **ventilation par facture** : worker OM (`EC_Type=0`) / lecteur FGR (`EC_Type=111`) — cache TASK-024 | coûteux |
| **C — statut déclaration** | Déclaré / Reste à déclarer / statut (Non déclarable · Partiel · Total) | `RT_AFFECTATION.DT_Id` + montants affectés | SQL local |

Conséquences de conception (à respecter) :
- **Le filtre date est obligatoire** précisément pour **borner la famille B**. Sans période, on
  refuserait de valoriser (garde-fou perf : cf. `grf-tva-perf-lecture-sage`).
- Ne **jamais** fabriquer HT/TVA à partir de `Montant`/`DO_TotalHT` par un forfait : piège de l'ancien
  GRF (cf. mémoire `tva-controle-ttc-vs-om`, retrait forfait 20 % TASK-017). Valeurs = worker/FGR ou
  **rien** (cellule explicitement « non valorisé » + motif), jamais une valeur inventée.
- **Transparence** : une facture non valorisable (OM illisible → `FACTURE_ILLISIBLE_OM`, solde initial
  `EC_Type=4`, etc.) reste **visible** avec son motif, jamais masquée (règle n°1 du projet).
- **TTC qui fait foi pour la déclarabilité = le décaissé**, pas le TTC théorique. La famille C se lit
  sur les affectations, pas sur `Montant`.

## Périmètre STRICT
- **Inclus** :
  1. **Back** — nouvel endpoint lecture seule `GET /api/factures?debut&fin[+filtres/pagination]`
     projetant la famille A (SQL `RT_ECHEANCE`) + la famille C (agrégat `RT_AFFECTATION` : `Réglé`,
     `Déclaré` = Σ affecté avec `DT_Id` non nul, `Reste à déclarer` = Réglé − Déclaré, `Solde` =
     TTC − Réglé, statut dérivé). Filtre **période obligatoire** (400 si absente).
  2. **Back** — famille B servie **depuis le cache de ventilation** (TASK-024) quand présent ;
     si absent, la cellule est renvoyée « non valorisé » avec motif (**pas** de déclenchement de
     lecture Sage synchrone massive dans cette task — à cadrer si besoin). DTO explicite par colonne.
  3. **Front** — écran « Factures » sous INTERROGATION : **filtre de période obligatoire** (pas de
     chargement tant que la période n'est pas saisie), grille dense (style projet, 0 espace perdu),
     tri/filtres colonnes/pagination serveur, statut de déclaration à **3 valeurs** lisible
     (Non déclarable / Partiel / Total) + colonnes Réglé/Déclaré/Reste.
- **Exclu (différé — option 2)** : cases à cocher, sélection facture/règlement, action « déclarer »,
  déclaration partielle actionnable, écriture `DT_Id`, export. Aucune modification du verrou TASK-028.
  Aucun recalcul/valorisation synchrone de masse (réutiliser le cache existant uniquement).

## Objectif
```
Entrée : GET /api/factures?debut=YYYY-MM-DD&fin=YYYY-MM-DD  (période OBLIGATOIRE) [+ filtres/tri/page]
Traitement : projection RT_ECHEANCE (A) + agrégat RT_AFFECTATION/DT_Id (C) ; B lue au cache TASK-024
Sortie : lignes facture { N°, Date, Fournisseur(code+intitulé), Réf, HT, TVA, AutreTaxe, Écart,
         Escompte, TTC, Réglé, Déclaré, Reste à déclarer, Solde, Statut(NonDéclarable|Partiel|Total),
         Motif si non valorisé } — pagination cohérente liste/count
```

## Étapes
1. **Back — repo** : requête `RT_ECHEANCE` filtrée période (borne sur la date de document), JOIN/agrégat
   `RT_AFFECTATION` pour Réglé/Déclaré/Reste/Solde et statut. WHERE partagé liste + `COUNT` (leçon
   TASK-040). Filtrer la nature facture pertinente (`EcheanceType.FactureGR`/OM ; exclure ce qui n'est
   pas une facture fournisseur). Lecture seule stricte (`DT_Id` lu, jamais écrit).
2. **Back — jointure cache B** : rattacher HT/TVA/AutreTaxe/Écart/Escompte depuis le cache de
   ventilation (TASK-024) par clé facture ; absence → `null` + motif. Aucun appel Sage synchrone.
3. **Back — DTO + contrôleur** (`FacturesController` ou avenant) : période obligatoire (400 si absente),
   pagination/tri, DTO explicite (une propriété par colonne, motif inclus).
4. **Front** — nouvel écran + câblage menu INTERROGATION (remplace le placeholder ⚪ de TASK-035).
   Garde-fou : rien n'est chargé sans période. Statut 3 valeurs + colonnes Réglé/Déclaré/Reste.
   Cellules famille B « non valorisé » rendues explicitement (jamais 0 silencieux).
5. **Doc** — noter la frontière avec l'option 2 (sélection/déclaration) pour la task suivante.

## Livrables
- Endpoint `GET /api/factures` lecture seule, période obligatoire, familles A+C réelles + B depuis cache.
- Écran « Factures » sous INTERROGATION, filtre date obligatoire, grille dense, statut 3 valeurs.
- `VERIFY/TASK-041_verify.md` : preuve réelle sur `GR_EMA_DISTRIBUTION` — une période donnée ; vérifier
  sur ≥1 facture : TTC=`Montant`, Solde=TTC−Réglé, Déclaré=Σ affecté `DT_Id` non nul, statut cohérent
  (montrer un cas **Partiel**), et au moins une facture **non valorisée** affichée avec son motif
  (transparence). Compteur = nombre réel après filtres (cohérent liste/count). Captures avant/après.

## Critères de validation
- Aucune requête tant que la période n'est pas saisie ; absence de période côté API ⇒ 400.
- Famille A exacte (TTC=`Montant`, Solde=TTC−Réglé). Aucun HT/TVA forfaitaire ; famille B = cache ou
  « non valorisé »+motif, jamais valeur inventée.
- Statut à 3 valeurs correct, dérivé des affectations (`DT_Id`), pas d'un booléen ; cas **Partiel** prouvé.
- Factures non valorisables **visibles** avec motif (aucune ligne silencieuse).
- Lecture seule stricte : aucun write, aucun `DT_Id` écrit, aucune lecture Sage synchrone de masse.
- Compteur cohérent avec la liste filtrée (leçon TASK-040) ; pagination/tri corrects.

## Risques / dépendances
- **Dépend du cache TASK-024** pour la famille B. Politique en cache-miss à confirmer avec le PO :
  (a) afficher « non valorisé »+motif [retenu par défaut ici] ou (b) valoriser à la demande, borné —
  **à cadrer** si (a) est jugé insuffisant. Ne pas ouvrir une lecture Sage massive sans décision.
- Frontière avec l'**option 2** (sélection/déclaration partielle actionnable) : hors périmètre,
  task suivante. Ne pas anticiper l'UI de sélection ici.
- Définition « facture fournisseur » (types `EcheanceType`/domaines retenus) à verrouiller au VERIFY,
  cohérente avec la sélection déclaration existante.
```