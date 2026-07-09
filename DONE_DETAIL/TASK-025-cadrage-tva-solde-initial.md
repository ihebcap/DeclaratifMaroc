# TASK-025 — Cadrage TVA du solde initial (`EC_Type = 4`)

## Contexte
`RT_ECHEANCE.EC_Type = 4` = **solde initial créé dans GRF** (exemple réel : `SI- 4411ELEC`, DO_Type 99,
libellé « FRAIS DE RETARD »). Le **traitement TVA de ces lignes n'est pas défini** : on ignore si elles
portent de la TVA, et si oui où en lire le détail. Signalé par le PO comme « à voir plus tard ».

Sans décision, TASK-022 route ces lignes vers une **alerte explicite** (« TVA solde initial non gérée ») —
jamais un saut silencieux ni un calcul OM hasardeux (règle n°1 : aucune ligne silencieuse). Cette tâche
existe pour **ne pas perdre le sujet** et le trancher proprement.

## Périmètre STRICT
- **Cadrage / décision métier d'abord**, implémentation ensuite (séparée si nécessaire).
- **Exclu** : toute implémentation avant arbitrage PO. Pas de calcul deviné.

## Objectif
Répondre, données réelles à l'appui, à :
1. Les lignes `EC_Type = 4` portent-elles de la TVA déclarable ? (cas « frais de retard » = souvent hors
   champ ou exonéré, à confirmer).
2. Si oui, **où** est le détail ? (`RT_HISTOCOMPTA` comme les FGR ? une autre source ? aucune ?)
3. Quel comportement cible : intégrer, exclure avec motif, ou reporter ?

## Contraintes techniques
- Lecture seule pour l'investigation ; `decimal`.
- Le comportement par défaut (alerte, non intégré) reste en place tant que non tranché — **transparent**.

## Étapes
1. Recenser sur `GR_EMA_DISTRIBUTION` les lignes `EC_Type = 4` : volume, montants, présence/absence de
   détail `RT_HISTOCOMPTA` rattaché (`MV_Id = EC_Id`).
2. Caractériser leur nature (frais, régularisations, reprises d'à-nouveau…) et leur champ TVA.
3. Proposer une règle de traitement + arbitrage PO.
4. Si décision d'intégrer → créer la tâche d'implémentation (réutilise le lecteur FGR si détail en
   `RT_HISTOCOMPTA`).

## Livrables
- `VERIFY/TASK-025_verify.md` : inventaire réel des `EC_Type = 4`, analyse de leur champ TVA, **décision PO**
  et règle retenue.
- Le cas échéant, une tâche d'implémentation dédiée.

## Critères de validation
- Inventaire réel chiffré des `EC_Type = 4` sur la vraie base.
- Décision métier explicite (intégrer / exclure+motif / reporter) validée PO.
- Aucune ligne `EC_Type = 4` traitée en silence entre-temps (alerte maintenue).

## Risques / dépendances
- **Bloquant** : décision PO (métier).
- Dépend de la base prod (dispo TASK-001/017). Indépendant de TASK-023/024.
- Priorité basse tant que le volume/impact n'est pas mesuré (étape 1).
