# Traitement des 5 lignes confirmées réellement anormales par TASK-149 (TASK-150)

**Date d'exécution :** 2026-07-20 (nuit, worker autonome).
**Méthode :** diagnostic individuel (réutilise `DiagnostiquerLigneAsync`, TASK-144/147 — lecture
seule stricte, aucune nouvelle lecture OM Sage), un cas à la fois, conformément au garde-fou
« pas de décision par lot ». Sortie complète du diagnostic dans `scratch/TestTask150/`.

## Décision par ligne

| EC_Id | Facture | Déclaration | Motif réel (diagnostic) | Cache Sage à jour ? | Décision |
|---:|---|---|---|:---:|---|
| 20650 | FC2501667 | TVA1-2026-02 | Incohérence Sage : Σ(HT+TVA+Parafiscale)=3 960,00 ≠ TTC=3 762,00 (écart −198,00) | Non périmé (dernière lecture 2026-07-16, aucune lecture plus récente) | **Aucune action technique possible** — voir ci-dessous |
| 21473 | FC2501717 | TVA1-2026-02 | Incohérence Sage : Σ=2 064 301,44 ≠ TTC=20 700,00 (écart −2 043 601,44) | Non périmé | **Aucune action technique possible** — voir ci-dessous |
| 24098 | FA2502718 | TVA1-2026-03 | Incohérence Sage : Σ=−15 318,45 ≠ TTC=20 294,93 (écart 35 613,38) | Non périmé (dernière lecture 2026-07-17, antérieure à la déclaration) | **Aucune action technique possible** — voir ci-dessous |
| 18198 | FC2600004 | TVA1-2026-04 | Facture introuvable — aucun document renvoyé par Sage OM | Aucune lecture en cache (jamais retrouvée) | **Aucune action technique possible** — voir ci-dessous |
| 18199 | FC2600005 | TVA1-2026-04 | Facture introuvable — aucun document renvoyé par Sage OM | Aucune lecture en cache | **Aucune action technique possible** — voir ci-dessous |

## Justification — pourquoi aucune ligne n'a été « fait aboutir » ni « exclue » par ce worker

L'Objectif de TASK-150 propose deux issues possibles : faire aboutir la ligne (si une correction
technique légitime existe) ou l'exclure avec un motif explicite. **Aucune des deux n'est
applicable ici sans une décision métier que ce worker ne peut pas prendre seul** :

1. **3 lignes en « Incohérence Sage »** (20650, 21473, 24098) : l'écart entre le total Sage
   (`Σ HT net+TVA+Parafiscale`) et le `TTC` de `RT_ECHEANCE` n'est **pas une erreur technique
   corrigible côté application** — c'est soit (a) une vraie anomalie de saisie/comptabilisation
   côté Sage (à corriger par le client dans son ERP), soit (b) un cas métier légitime que l'analyse
   automatique ne sait pas distinguer (avoir, facture d'acompte, écriture multi-devises, etc.). Sans
   accès à la pièce Sage réelle pour investigation humaine, **faire aboutir la ligne inventerait un
   montant** (interdit — « ne jamais insérer une valeur d'IF arbitraire/placeholder », principe
   étendu ici aux montants) et **l'exclure déciderait unilatéralement qu'elle ne doit jamais être
   déclarable**, ce qui est une décision fiscale, pas technique. Le cas `21473` (écart de
   **2 043 601,44 MAD**, très supérieur au montant de la pièce elle-même) est particulièrement
   suspect d'une erreur de saisie côté Sage (montant à plusieurs zéros de trop) — signalement
   PRIORITAIRE au PO/service comptable client.
2. **2 lignes « Facture introuvable »** (18198, 18199, même règlement `RF26030084`, tiers ATLANTIC
   FOODS) : Sage OM ne renvoie aucun document pour ces échéances. Deux hypothèses possibles
   (la TASK le dit explicitement : « facture réellement introuvable des deux côtés, à investiguer
   avec le client ») : la pièce n'a jamais été comptabilisée sous ce numéro côté Sage (auquel cas le
   règlement ne doit effectivement pas être déclaré cette période — mais c'est au client de confirmer
   qu'aucune facture ne sera jamais rattachée), ou un problème de synchronisation/numérotation à
   creuser côté ERP. Aucune preuve positive dans un sens ou l'autre n'est disponible en lecture
   seule.

**Aucun cache périmé** (TASK-147) ne s'applique à ces 5 lignes : les dernières lectures connues
sont **antérieures** à leur déclaration respective (voire inexistantes pour 18198/18199) — le
mécanisme de recalcul TASK-147 refuse donc correctement d'agir (vérifié : `CachePerime=False` pour
les 5, sortie complète dans `scratch/TestTask150/`).

## Décision finale

**Toutes les 5 lignes restent à l'état `Proposee`, inchangées** (aucune écriture effectuée — aucun
`UPDATE`/`DELETE` sur `DM_LGTVA`/`DM_VENTILATION_SAGE_CACHE` par cette TASK). Elles sont marquées
ici comme **« nécessite investigation Sage + arbitrage PO »** :

- 3 lignes : le service comptable doit vérifier la pièce dans Sage et confirmer/corriger le montant
  réel avant toute déclaration (`20650`/`FC2501667`, `21473`/`FC2501717`, `24098`/`FA2502718`).
- 2 lignes : le service comptable doit confirmer si la facture `FC2600004`/`FC2600005` (tiers
  ATLANTIC FOODS, règlement `RF26030084`) existe réellement côté Sage sous ce numéro, ou si le
  règlement ne doit pas être déclaré cette période.

Ce report explicite est le comportement attendu par le garde-fou de TASK-150 (« cette TASK peut
légitimement se terminer avec certaines lignes encore ouvertes si elles nécessitent un arbitrage
humain — ce n'est pas un échec »).

## Écart d'équilibre résiduel après ce traitement

Inchangé par rapport au ré-audit TASK-149 (aucune ligne modifiée) : `TVA1-2026-02: −24 462,00`,
`TVA1-2026-03: −20 294,92`, `TVA1-2026-04: −6 651,99`. Ces 3 déclarations restent **non signables**
tant que le PO/service comptable n'a pas statué sur les 5 lignes ci-dessus.
