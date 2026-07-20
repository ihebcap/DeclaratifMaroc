# TASK-060 — Remontée claire des erreurs de valorisation à l'utilisateur (breakdown par code)

> **Origine :** rafraîchissement facture-first juin 2026 (`soId=1`) : log serveur
> `[VALO-050] === Fin : 202 traitée(s), 240 en erreur ===`. Le PO constate ce chiffre agrégé et ne
> peut pas savoir, sans intervention technique, **quelles** erreurs il recouvre (qualité de données
> tiers ? factures FGR en échec ? OM illisible ? réglement non affecté ?). Investigation technique
> confirmée : le backend calcule et transmet déjà le détail complet, mais **rien ne l'affiche**.

## Constat (preuve code, aucune supposition)
1. **Le backend construit déjà le détail complet** — `Declaration.Application/Services/DeclarationWorkflowService.cs:1048,1051` :
   ```csharp
   public record RapportValorisation(int FacturesTraitees, IReadOnlyList<MotifValorisation> Erreurs);
   public record MotifValorisation(string Code, string Message, string RefLigne);
   ```
   `Erreurs` est une liste plate (une entrée par facture en erreur), sans agrégation par `Code`.
2. **L'endpoint transmet tout au front** — `Declaration.API/Controllers/FacturesController.cs:140-163`,
   `POST /factures/rafraichir-valorisation` renvoie `{ FacturesTraitees, NbErreurs, Erreurs[] }` avec
   le tableau complet (Code/Message/RefLigne), pas seulement un compteur.
3. **Le front reçoit le détail mais ne l'affiche pas** —
   `declaration-tva-web/src/FactureInterrogation.tsx:218-238`, `handleRefreshValorisation` :
   - `console.warn('Motifs de non-valorisation :', res.data?.erreurs)` → détail **uniquement dans la
     console navigateur**.
   - `showToast(\`Valorisation — ${n} traitée(s), ${nbErr} en erreur (voir console / logs serveur)\`, 'error')`
     → seul signal utilisateur, un **compteur brut** avec renvoi vers la console ou les logs serveur.
   - `showToast` (`App.tsx:74-77`) n'accepte qu'une chaîne simple, toast 3 secondes — **aucune capacité
     de liste/tableau**.
4. **Aucune agrégation par `Code` nulle part** (ni back ni front, ni dans `logs/valorisation.log` qui
   ne journalise que la ligne agrégée `[VALO-050] === Fin : ... ===`).
5. **Conséquence produit** : un utilisateur non technique n'a **aucun moyen** de distinguer
   « 30 `TIERS_SANS_ICE` (qualité de données, hors périmètre applicatif) » de « 10 `ERREUR_FGR`
   (échec de lecture taxe, à investiguer) » sans ouvrir les DevTools ou demander un `grep` sur le
   fichier de log serveur.

## Objectif
Rendre le détail des erreurs de valorisation **lisible par le PO/utilisateur métier dans l'écran**,
sans étape technique intermédiaire (console, log serveur, support).

## Périmètre proposé (à valider/arbitrer en revue de TASK, pas décidé ici)
### A. Agrégation par code (affichage)
Grouper `Erreurs[]` par `Code` côté front (ou ajouter un DTO d'agrégation côté back si le volume par
page devient un sujet) : `{ Code, Libellé, Count, Exemples: RefLigne[] }`. Réutiliser la taxonomie
existante de `Declaration.Core/ConstructeurDeclaration.cs` (`REGLEMENT_NON_AFFECTE`,
`CODE_TAXE_INCONNU`, `ERREUR_FGR`, `FACTURE_INTROUVABLE`, `FACTURE_ILLISIBLE_OM`, `TIERS_SANS_ICE`,
`ICE_INVALIDE`, `TIERS_SANS_IF`, `IF_INVALIDE`) pour donner un libellé métier lisible à chaque code
(pas juste la constante technique).

### B. Composant d'affichage
Remplacer/compléter le toast actuel (`FactureInterrogation.tsx:185`) par une vue qui liste la
répartition par code (modale ou panneau extensible depuis le toast — arbitrage UX à faire), avec accès
au détail par ligne (`RefLigne`) pour investigation. Garder le toast agrégé comme signal immédiat, mais
ne plus renvoyer l'utilisateur vers « la console / les logs serveur ».

### C. Distinction qualité-de-données vs anomalie applicative
Séparer visuellement (ou par filtre) les codes de **qualité de données tiers**
(`TIERS_SANS_ICE`/`ICE_INVALIDE`/`TIERS_SANS_IF`/`IF_INVALIDE` — non bloquants pour la déclaration,
à corriger côté fiche tiers Sage) des codes d'**anomalie applicative** (`ERREUR_FGR`,
`FACTURE_ILLISIBLE_OM`, `FACTURE_INTROUVABLE` — à investiguer côté technique).

## Garde-fous
1. **Lecture seule** — aucune écriture, ce chantier est strictement de la restitution d'un état déjà
   calculé (`modele.Alertes`) ; ne pas toucher à `ConstructeurDeclaration.cs` ni à la logique de
   valorisation/cache (TASK-050/052).
2. **Ne pas fabriquer de nouveau code d'erreur** sans revue — réutiliser la taxonomie existante.
3. **Pas de régression sur le toast agrégé actuel** tant que le nouveau composant n'est pas livré (pas
   de rupture d'UX intermédiaire).
4. **Volume** : avec ~240 entrées sur un mois, la vue doit grouper par défaut (pas de liste brute de
   240 lignes affichée d'un coup).

## Livrables de preuve (VERIFY)
1. Capture ou description de l'écran avant/après sur le jeu réel juin 2026 (`soId=1`,
   2026-06-01→2026-06-30) : répartition par code visible sans ouvrir les DevTools.
2. Confirmation que la somme des `Count` par code = `NbErreurs` (240 sur le run de référence).
3. Confirmation qu'aucune ligne de `RapportValorisation`/`ConstructeurDeclaration` n'a été modifiée
   (diff limité au front + éventuel DTO d'agrégation, pas de logique métier).
4. Tests front (composant d'agrégation/affichage) + non-régression suite existante.

## Dépendances / risques
- **Dépend de** TASK-050/052 (source du `RapportValorisation.Erreurs`, déjà livrées).
- **Risque UX** : périmètre B (choix modale vs panneau) à trancher en amont du développement — ne pas
  laisser l'implémenteur arbitrer seul un choix produit.
- **Aucun risque de sécurité/données** : restitution pure d'un calcul déjà produit côté serveur.
