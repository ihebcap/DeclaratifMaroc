# TASK-058 — Écran ⑤ Contrôle déclaration (synthèse source/taux + anomalies)

> Étape ⑤ du tunnel (TASK-053). Front (lecture seule). Densité **0 espace perdu**. Répond à : « ma déclaration est-elle correcte avant dépôt ? »

## Contexte
Après intégration, vue de contrôle final (`reflexion dectva.md` §6) : répartition par **source** (décaissements/espèces/dépenses/frais bancaires), vue par **taux**, et liste d'**anomalies** séparées bloquantes 🔴 / avertissements 🟠. Le mode contrôle back existe (recalcul vs GRFN, `Declaration.Controle`, TASK-009) et le front `ControlGrid.tsx` fournit le patron. ⚠️ Ne pas refaire le hub « écart 0 » abandonné (mémoire `grf-checkup-hub-maquette`) : ici c'est une **liste d'anomalies actionnable**, pas un héros d'agrégats.

## Périmètre STRICT
- **Inclus** : synthèse répartition par source + contrôle d'équilibre ; vue par taux ; anomalies 🔴 bloquantes (ICE/IF incorrect, facture absente Sage, montant nul) et 🟠 avertissements (règlement sans affectation, données incomplètes) ; drill anomalie → ligne concernée (réutiliser TASK-016).
- **Exclu** : tout recalcul front (source = back TASK-009) ; l'export ⑥ ; réintroduction du hub abandonné (TASK-018).

## Objectif
```
Entrée : déclaration intégrée + résultat du mode contrôle back (TASK-009)
Traitement : agréger par source & par taux, lister/typer les anomalies
Sortie : vue de contrôle dense, anomalies 🔴/🟠 actionnables (drill vers la ligne)
```

## Étapes
1. Répartition par source (décaissements/espèces/dépenses/frais bancaires) + ligne total + indicateur d'équilibre.
2. Vue par taux (20/14/10/…) en tableau dense.
3. Anomalies typées 🔴 bloquantes / 🟠 avertissements, aucune masquée (transparence).
4. Drill anomalie → grille filtrée sur la ligne (réutiliser le drill-down TASK-016).
5. Bloquer la clôture/export tant qu'il reste des 🔴 (garde-fou, cohérent clôture gardée TASK-012).

## Livrables
- Écran ⑤ (adaptation `ControlGrid` + panneau anomalies).
- `VERIFY/TASK-058_verify.md` : preuve réelle (répartition source/taux exacte, 🔴/🟠 listées, drill fonctionnel, export bloqué si 🔴), build + lint verts.

## Critères de validation
- Répartitions = somme des lignes intégrées ; équilibre calculé côté back.
- Toutes les anomalies visibles et typées (aucun saut silencieux).
- Export/clôture bloqués s'il reste une anomalie 🔴.
- Lecture seule ; densité 0 espace perdu ; **pas** de hub agrégat (TASK-018 abandonné).

## Risques / dépendances
- Dépend de TASK-057 (intégration) et TASK-053.
- Réutilise TASK-009 (recalcul/contrôle) + TASK-016 (drill anomalie→grille).
- Ne pas ressusciter le concept hub « écart 0 » (mémoire `grf-checkup-hub-maquette`).
- 🚫 **Anti-régression** : le recalcul/contrôle back (TASK-009) et le drill (TASK-016) sont **intouchables** — cette task n'ajoute qu'une vue anomalies/répartition qui **consomme** le résultat du contrôle. Aucun recalcul front.
