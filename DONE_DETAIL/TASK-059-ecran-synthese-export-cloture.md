# TASK-059 — Écran ⑥ Synthèse & Export (Excel + XML + clôture)

> Étape ⑥ (dernière) du tunnel (TASK-053). Front sur exports **déjà livrés** (TASK-010 Excel, TASK-011 XML). Densité **0 espace perdu**.

## Contexte
Écran final (`reflexion dectva.md` §8) : récap déclaration (nb lignes, TVA totale, état « contrôle terminé »), puis actions **Export Excel de contrôle**, **Générer XML Simpl-TVA**, **Clôturer**. Les exports existent (`GenerationPanel.tsx`, TASK-010/011) et la clôture gardée existe (TASK-012). Cette task assemble ces briques dans l'étape ⑥ du tunnel, en s'appuyant sur le contrôle ⑤ (aucun export tant qu'il reste des 🔴).

## Périmètre STRICT
- **Inclus** : récap synthèse dense (nb lignes, TVA totale, état contrôle) ; boutons Export Excel / Générer XML / Clôturer câblés sur l'existant ; garde clôture (bloquée si anomalies 🔴 ou contrôle non terminé).
- **Exclu** : la génération elle-même (Excel/XML déjà livrés, ne pas retoucher) ; la télédéclaration SIMPL (roadmap R2, hors périmètre) ; toute logique de calcul.

## Objectif
```
Entrée : déclaration intégrée et contrôlée (⑤ OK)
Traitement : présenter la synthèse, déclencher Excel/XML existants, clôturer (gardé)
Sortie : artefacts de dépôt (Excel + XML) + déclaration clôturée (verrou)
```

## Étapes
1. Bandeau synthèse dense : nb lignes, TVA totale, état « ✔ Contrôle terminé » (issu de ⑤).
2. Boutons Export Excel (TASK-010) et Générer XML Simpl-TVA (TASK-011) câblés via `GenerationPanel` existant.
3. Clôturer : appel clôture gardée (TASK-012) — bloquée tant que ⑤ signale des 🔴 ou contrôle non terminé.
4. Retours utilisateur via toast (succès/erreur), aucun état factice.

## Livrables
- Écran ⑥ (adaptation `GenerationPanel`/`SummaryPanel` dans l'étape).
- `VERIFY/TASK-059_verify.md` : preuve (Excel généré, XML généré conforme, clôture bloquée si 🔴 puis réussie, verrou posé), build + lint verts.

## Critères de validation
- Excel + XML produits par les modules existants (aucune régression export).
- Clôture impossible si anomalie 🔴 / contrôle non terminé ; réussie sinon (verrou).
- Aucune donnée factice ; densité 0 espace perdu.

## Risques / dépendances
- Dépend de TASK-058 (⑤ contrôle) et TASK-053.
- Réutilise TASK-010/011 (exports) + TASK-012 (clôture gardée) — **ne pas** retoucher ces modules.
- Télédéclaration SIMPL = roadmap R2, hors périmètre.
- 🚫 **Anti-régression** : les exports (Excel TASK-010, XML TASK-011, `GenerationPanel`) et la clôture gardée (TASK-012) sont **intouchables** — cette task ne fait que les **assembler/câbler** dans l'étape ⑥. Aucune régression des exports existants (VERIFY doit rejouer un export réel).
