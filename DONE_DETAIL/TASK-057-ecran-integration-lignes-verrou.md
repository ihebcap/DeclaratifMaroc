# TASK-057 — Écran ④ Intégration des lignes (confirmation + verrou DT_Id)

> Étape ④ du tunnel (TASK-053). Front sur back **déjà livré** (verrou TASK-028). Densité **0 espace perdu**. Équivalent amélioré du bouton « Intégrer » de GRFN.

## Contexte
Écran de confirmation avant de figer la déclaration (`reflexion dectva.md` §5) : récap (nb lignes, montant TVA), check-list de contrôles, puis action **Confirmer intégration** qui pose le tampon `RT_AFFECTATION.DT_Id` et **exclut** ces règlements des prochaines recherches. Le back est en place : verrou/tampon + triggers immuabilité (TASK-028), figeage API (TASK-012/017).

## Périmètre STRICT
- **Inclus** : écran récap pré-intégration (lignes sélectionnées, montant TVA total) ; check-list de contrôles ✔ (factures trouvées, TVA calculée, affectations valides, IF/ICE conformes) ; bouton **Confirmer intégration** appelant l'endpoint de figeage existant ; état post-intégration (lecture seule, exclusion des recherches futures).
- **Exclu** : la logique back du verrou (TASK-028, ne pas retoucher) ; le contrôle final ⑤ (TASK-058) ; l'export ⑥.

## Objectif
```
Entrée : déclaration valorisée et contrôlée (③)
Traitement : présenter le récap + contrôles, confirmer → figeage back (DT_Id)
Sortie : déclaration intégrée (lignes créées, rattachées, exclues des recherches), lecture seule
```

## Étapes
1. Récap dense : nb lignes + montant TVA (repris de ③), sans recalcul front.
2. Check-list de contrôles alimentée par le back (chaque item vert/rouge ; un rouge bloque le bouton).
3. `Confirmer intégration` → endpoint de figeage/verrou existant (TASK-012/017/028) ; gérer succès/erreur via toast (`showToast`).
4. Après succès : basculer le tunnel en lecture seule (piloté par TASK-053), afficher « intégrée ».
5. Réouverture éventuelle = `DT_Id→NULL` (back existant) — exposer seulement si le workflow PO le prévoit, sinon hors périmètre.

## Livrables
- Écran ④ (composant confirmation) branché sur l'API de figeage.
- `VERIFY/TASK-057_verify.md` : preuve d'une intégration réelle (tampon `DT_Id` posé, exclusion vérifiée), bouton bloqué si contrôle rouge, lecture seule après, build + lint verts.

## Critères de validation
- Aucune intégration possible si un contrôle bloquant est rouge.
- Après intégration : lignes exclues des prochaines sélections (vérifié) ; tunnel en lecture seule.
- Aucun recalcul ni écriture front hors appel de l'endpoint existant.
- Densité 0 espace perdu.

## Risques / dépendances
- Dépend de TASK-056 (③) et TASK-053 (verrouillage lecture seule).
- Réutilise TASK-028 (verrou) + TASK-012/017 (figeage) — **ne pas** modifier le back verrou.
- Cohérence stricte : le montant intégré = celui affiché en ③ (source unique).
- 🚫 **Anti-régression** : le back verrou (`DT_Id` + triggers immuabilité TASK-028) et le figeage (012/017) sont **intouchables** — cette task n'ajoute qu'un écran de confirmation qui **appelle** l'endpoint existant. Aucune écriture front hors cet appel.
