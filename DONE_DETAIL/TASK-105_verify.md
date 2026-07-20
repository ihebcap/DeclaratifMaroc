# VERIFY — TASK-105 — Traçabilité d'une incohérence validée : marqueur persistant + filtre

> Implémentée en **worker exceptionnel** (demande explicite PO/architecte, 17/07/2026) — l'architecte
> ne code pas d'ordinaire (cf. `CLAUDE.md`), rôle inversé pour cette task uniquement. Ce document est
> soumis pour revue, pas auto-approuvé.

## Résumé
Front-only, un seul fichier modifié : `declaration-tva-web/src/AffectationsDrill.tsx`. Réutilise
le flag `incoherenceValidee` déjà remonté du back (TASK-078, `LigneCandidateDto.IncoherenceValidee`)
— aucune nouvelle source de donnée, aucun endpoint touché, aucun calcul (montants/TVA/écart)
modifié.

## Modifications
- **Marqueur persistant** : nouvelle colonne filtrable `Incoh.` (`GRID_COLUMNS`) rendant un badge
  ambre « Validée » (titre au survol rappelant la portée : « décision tracée, ligne et totaux
  inchangés ») quand `incoherenceValidee === true` ; `—` sinon. Contrairement au bandeau rouge
  existant (dérivé des seules alertes **actives**, `facturesIncoherentes`), ce badge **ne disparaît
  jamais** après validation — c'est exactement le flag persistant en base, jamais recalculé.
- **Teinte de ligne** : léger fond ambre (`#fffbeb`) sur les lignes `incoherenceValidee === true`
  (uniquement si l'alerte n'est plus active, pour ne pas entrer en conflit visuel avec le bandeau
  rouge d'une incohérence encore ouverte) — la facture reste repérable même colonne masquée.
- **Filtre** : colonne `incoherenceValidee` ajoutée au mécanisme de filtre existant (`ExcelFilter`,
  même patron que `statutConformite`) avec liste fixe `Oui`/`Non` (booléen, pas de dérivation utile
  depuis les lignes chargées). Le PO peut isoler toutes les factures à incohérence validée en un
  clic, sans action de recherche.
- **Qui/quand** : non exposé. Vérifié dans le code back (`Declaration.Application/Entities/WorkflowEntities.cs:108-109`,
  `Declaration.Infrastructure/SQL/007_DM_LGTVA_Incoherence_Validee.sql`) — les colonnes
  `IncoherenceValideePar`/`IncoherenceValideeLe` **existent et sont peuplées** côté base/entité
  (`DeclarationRepository`/`ValiderIncoherenceLigneAsync`), mais **ne sont pas exposées** dans
  `LigneCandidateDto.cs` (seul `IncoherenceValidee` bool y figure, ligne 103-104). Conformément au
  périmètre strict de la task (« si non disponible dans le DTO actuel, se limiter au marqueur
  booléen — ne pas ouvrir un chantier back non demandé »), **aucune modification du DTO/back n'a été
  faite**. Signalé ici pour arbitrage PO séparé (petit ajout DTO à faire s'il souhaite le qui/quand
  visible au survol).
- **Exclu, conforme au périmètre** : aucune modification de `Ventilateur`/calcul de montants, aucune
  modification de la logique de détection TASK-077/078, aucune fonctionnalité de « dé-validation ».

## Build
```
npm run build   (tsc -b && vite build)   → 0 erreur
npm run lint    (oxlint)                 → aucun nouveau warning (résidus pré-existants
                                            sur d'autres fichiers, non liés à ce changement)
```

## Preuve réelle — capture avant/après/filtre
Application réelle lancée localement contre la base réelle du poste de dev
(`DESKTOP-5BFKKEP` / `GR_EMA_DISTRIBUTION`, `connections.json`) : `Declaration.API` (`dotnet run`,
`http://localhost:5018`) + front (`npm run dev`, `http://localhost:5173`). Login réel
(`Admin`/`Admin` contre `P_UTILISATEUR`), déclaration réelle **`TVA1-2026-06`** (seule déclaration
existante en base, statut `EnCours`), écran ② Affectations ouvert sur une sélection réelle de
règlements (`RC26060101`, `RC26060102`).

**Constat préalable (SQL direct sur `DM_LGTVA`)** : la base a été réinitialisée depuis le test PO
du 17/07/2026 sur `TVA1-2026-01` (déclaration absente de la base actuelle — seule `TVA1-2026-06`
existe, 12 lignes) ; **0 ligne `IncoherenceValidee=1`** n'existe donc actuellement en base pour
rejouer le cas réel tel quel. Pour produire la capture « avant/après validation » demandée par la
task sans fabriquer une fausse incohérence Sage (hors périmètre, risqué sur données réelles), la
valeur du flag a été simulée par interception réseau sur `GET /lignes` (même technique déjà
utilisée dans ce projet par `tests/task102.spec.ts` pour injecter une alerte `LIGNE_FIGEE_A_REVERIFIER` :
route interceptée, réponse réelle relue puis un seul champ muté) — la **forme** de la donnée
(`incoherenceValidee`/`ecId` sur un objet `LigneCandidateDto` réel) est celle du contrat back, seule
sa valeur est simulée en l'absence de cas réel disponible aujourd'hui.

- `TASK-105-01-avant-aucun-marqueur.png` — 10 lignes réelles, colonne `Incoh.` à `—` partout
  (aucune ligne validée en base), Total TVA tous règlements = **2 125,58 MAD**.
- `TASK-105-02-apres-marqueur-persistant.png` — même déclaration, même sélection ; la facture
  `FA2600664` (`RC26060101`) porte désormais le badge ambre « Validée » sur ses 2 lignes de taux,
  fond de ligne ambre. **Total TVA tous règlements inchangé : 2 125,58 MAD**, Total TVA déclarée en
  pied de grille inchangé — confirme un rendu pur, aucun recalcul.
- `TASK-105-03-filtre-incoherence-validee.png` — filtre colonne `Incoh.` = `Oui` appliqué : grille
  réduite à **2 lignes sur 10** (les 2 lignes de taux de `FA2600664`), TVA filtrée 282,26 MAD.
  Confirme que le PO peut retrouver à tout moment quelles factures ont une incohérence validée.

## Critères de validation (task originale)
- [x] Après « Valider l'incohérence », la facture reste visible/identifiable via un marqueur (plus
      de disparition silencieuse) — capture avant/après ci-dessus (badge + teinte persistants).
- [x] Un filtre permet de lister les factures à incohérence validée — capture filtre ci-dessus.
- [x] Aucun changement de montants, de TVA déclarée, ni d'écart d'équilibre (rendu pur) — Total TVA
      identique avant/après (2 125,58 MAD), aucune valeur de `montantHT`/`montantTVA`/`montantTTC`
      touchée dans le code (seuls `incoherenceValidee`/style de rendu sont conditionnés au flag).
- [x] Le workflow de validation existant (bouton, toast, appel API `POST valider-incoherence`) est
      inchangé — `handleValider` non modifié.

## Réserves (signalées, non corrigées — hors périmètre de cette task)
- **Qui/quand non exposé** (cf. § Modifications) — dette DTO mineure, à arbitrer par le PO
  séparément si le survol du badge doit afficher l'auteur/la date.
- **Rafraîchissement post-validation** : le marqueur persistant dépend du rechargement du drill
  après le `POST valider-incoherence` (`handleValider` appelle déjà `reload()`) — comportement
  existant inchangé, non re-testé ici en conditions réelles faute de cas réel disponible (voir
  § Preuve réelle) ; à confirmer par le PO au prochain cas réel rencontré.
- **Cas réel d'origine non rejouable** : `TVA1-2026-01` (à l'origine du signalement PO) n'existe
  plus dans la base actuelle — capture réalisée sur `TVA1-2026-06` avec valeur simulée par
  interception réseau (cf. § Preuve réelle). À re-vérifier visuellement par le PO au premier vrai
  cas de validation rencontré en usage normal.

## Statut
**Soumise pour revue** (worker exceptionnel — ne s'auto-approuve pas, conformément à la séparation
des rôles `CLAUDE.md`).
