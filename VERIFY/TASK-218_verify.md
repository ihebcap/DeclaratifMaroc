# TASK-218 Verify — Commentaire généré automatiquement expliquant chaque ligne (écran Contrôle DDP)

> Implémenté par Claude en rôle **WORKER de secours** (dérogation explicite du PO, session du
> 10/09/2026). Conformément à la règle de séparation implémentation/clôture (`CLAUDE.md` racine),
> ce fichier VERIFY est déposé pour review par un tiers (PO ou session ARCHITECT distincte) — aucune
> clôture (déplacement `DONE_DETAIL/`, mise à jour `DONE.md`/`TODO.md`/`CHANGELOG.md`) n'a été
> effectuée par ce worker.

## Étape 1 — choix backend vs front : **FRONT**

Choix documenté comme demandé par la TASK. Justification : tous les champs nécessaires à la mise en
phrase (`bucket`, `statut`, `origineDelai`, `nombreJoursDelaiApplique`, `echeanceLegale`,
`origineBorneReference`, `borneReference`, `borneActuelle`, `depassement`, `typeReglement`,
`dateReglement`, `dateRapprochement`) sont **déjà présents** dans `LigneSelectionDdpDto`/`api.ts` —
aucune nouvelle donnée backend requise. Un aller-retour serveur n'ajouterait qu'une couche de
sérialisation d'un champ texte, sans réduire le risque de divergence (le front devrait de toute
façon rester la référence pour le vocabulaire "Dernière déclaration"/"Constaté le" introduit par
TASK-217, déjà côté front). Générer côté front garde tout le calcul métier dans
`SelectionDelaiPaiementCalculator` (seule source de vérité, inchangé) et ne fait QUE de la mise en
phrase, conformément au périmètre strict de la TASK.

## Fichiers modifiés

- `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx` :
  - `phraseOrigineDelai`, `phraseBorneReference`, `genererCommentaireLigne` (nouvelles fonctions
    pures, aucune dépendance React, testables indépendamment).
  - Nouvelle colonne `Explication` dans `columnDefs` (avant la colonne `Action` pinned à droite) :
    texte tronqué avec ellipsis + `title` natif (tooltip navigateur) pour lire le texte complet au
    survol sans casser la densité de la grille — pas de modal, pas de clic requis.

Aucun fichier `.cs` touché (choix front, cf. étape 1). Aucune logique de calcul du dépassement/des
bornes modifiée — uniquement une lecture des champs déjà exposés.

## Couverture des cas (croisement bucket × statut × payé/non payé, étape 2 de la TASK)

Les 5 combinaisons réelles produites par `SelectionDelaiPaiementCalculator` sont couvertes par un
gabarit distinct :

| # | Cas réel | Gabarit |
|---|---|---|
| 1 | `Statut = RepriseManuelleRequise` (tout bucket confondu — le garde-fou TASK-128 est vérifié en premier dans `genererCommentaireLigne`) | Reprise manuelle |
| 2 | `Candidate` + `DansPeriodePartAffectee` ou `HorsPeriodePartAffectee`, réglée par **pièce rapprochée** (Chèque/Traite/Virement) | Payée + rapprochée |
| 3 | `Candidate` + `DansPeriodePartAffectee` ou `HorsPeriodePartAffectee`, réglée par **pièce NON rapprochée** | Payée + non rapprochée |
| 4 | `Candidate` + `DansPeriodePartAffectee` ou `HorsPeriodePartAffectee`, réglée par **Espèce/Autre** | Payée (implicite dans #2/#3, la phrase règlement omet juste la clause pointage) |
| 5 | `Candidate` + `DansPeriodePartNonAffectee` ou `HorsPeriodePartNonAffectee` (échéance/solde restant **non payé**) | Non payée |

Les buckets `DansPeriodePartAffectee`/`HorsPeriodePartAffectee` et
`DansPeriodePartNonAffectee`/`HorsPeriodePartNonAffectee` partagent volontairement le même gabarit
(seule la donnée réelle change : dates de règlement pour l'un, `borneActuelle` = fin de période pour
l'autre) — la distinction hors/dans période n'apporte pas de sémantique supplémentaire pour
l'utilisateur final, qui voit déjà la colonne « Échéance légale » et peut la comparer à la période
affichée en toolbar.

De plus, la borne de référence (`origineBorneReference`) varie indépendamment du bucket et produit
sa propre sous-phrase : `DerniereDeclaration` (« Déjà déclarée jusqu'au … »), `EcheanceLegale`
(« 1ʳᵉ déclaration… »), `RepriseManuelle` (« Retard antérieur repris manuellement… »).

## ⚠️ Test visuel navigateur — NON réalisé dans cette session (signalé explicitement)

Tentative réelle effectuée (pas un renoncement de principe) :
1. API démarrée (`dotnet run`) + front démarré (`npm run dev`) dans cette session.
2. `GET /api/societes` retourne un **500** : `SqlException` — `Le serveur est introuvable ou n'est
   pas accessible` (`Fournisseur de canaux nommés, error: 40`). La base SQL Server
   (`GR_EMA_DISTRIBUTION`) n'est **pas accessible depuis cet environnement de session** — aucune
   société ne peut être chargée, donc aucun écran de contrôle DDP ne peut être atteint pour produire
   une vraie capture d'écran.
3. Les deux serveurs de dev ont été arrêtés proprement à la fin de la tentative ; le port de proxy
   Vite (`vite.config.ts` → `http://localhost:5280`) avait été temporairement changé pour pointer
   vers l'instance locale (`5005`) le temps du test, puis **restauré** à sa valeur d'origine
   (`git diff` confirme `vite.config.ts` intact, aucune modification résiduelle).

**Aucune capture d'écran réelle n'a donc pu être produite dans cette session** — même contrainte
d'environnement que celle déjà rencontrée et documentée pour TASK-217 (« backend non démarré dans
cette session »), ici constatée plus précisément : backend démarré avec succès, mais base de données
hors d'atteinte.

### Exemples tracés manuellement (à défaut de capture, un exemple par cas, calculé à la main en
### rejouant le code de `genererCommentaireLigne` ligne par ligne — à revérifier visuellement par le
### reviewer une fois la base accessible) :

1. **Reprise manuelle requise** (`statut=RepriseManuelleRequise`, `origineDelai=Defaut`,
   `echeanceLegale=2023-05-10`, `nombreJoursDelaiApplique=60`) :
   > Échéance légale le 10/05/2023 (Défaut société, 60 j), antérieure à la date de mise en route du
   > module et sans historique de déclaration : le retard déjà couvert doit être saisi manuellement
   > (bouton « Reprise manuelle ») avant toute intégration.

2. **Payée, pièce rapprochée** (`bucket=DansPeriodePartAffectee`, `origineDelai=Convention` 90j,
   `echeanceLegale=2026-04-07`, `typeReglement=Cheque`, `dateReglement=2026-07-20`,
   `dateRapprochement=2026-08-06`, `origineBorneReference=DerniereDeclaration`,
   `borneReference=2026-06-30`, `depassement=20`) :
   > Échéance légale le 07/04/2026 (Convention, 90 j). Réglée le 20/07/2026 (Chèque), rapprochée le
   > 06/08/2026. Déjà déclarée jusqu'au 30/06/2026 → 20 jour(s) de retard nouveaux comptés sur cette
   > période.

3. **Payée, pièce NON rapprochée** (`bucket=HorsPeriodePartAffectee`, `typeReglement=Virement`,
   `dateReglement=2026-07-05`, `dateRapprochement=null`, `origineBorneReference=EcheanceLegale`,
   `depassement=15`) :
   > Échéance légale le 15/03/2026 (Défaut société, 60 j). Réglée le 05/07/2026 (Virement), pas
   > encore rapprochée en banque : le retard continue de courir tant que le pointage n'est pas
   > confirmé. 1ʳᵉ déclaration pour cette échéance → 15 jour(s) de retard comptés depuis l'échéance
   > légale.

4. **Payée, Espèce** (`typeReglement=Espece`, sans clause pointage) :
   > Échéance légale le 12/02/2026 (Convention facture, 30 j). Réglée le 18/02/2026 (Espèce). 1ʳᵉ
   > déclaration pour cette échéance → 6 jour(s) de retard comptés depuis l'échéance légale.

5. **Non payée** (`bucket=DansPeriodePartNonAffectee`, `echeanceLegale=2026-06-08`,
   `origineDelai=Defaut` 60j, `origineBorneReference=DerniereDeclaration`,
   `borneReference=2026-06-30`, `borneActuelle=2026-09-30` (fin de période), `depassement=92`) :
   > Échéance légale le 08/06/2026 (Défaut société, 60 j), toujours impayée. Déjà déclarée jusqu'au
   > 30/06/2026 → 92 jour(s) de retard nouveaux comptés jusqu'au 30/09/2026. Ces jours continueront
   > à courir tant que l'échéance reste non réglée.

Ces 5 textes correspondent aux 3 exemples donnés dans la TASK elle-même (le premier et le troisième
y sont repris quasi mot pour mot), ce qui donne un niveau de confiance raisonnable sur la fidélité du
gabarit — mais **ne remplace pas** une vérification visuelle réelle sur des lignes issues de la base.
Un test avec accès DB (ou par le PO en environnement réel) reste nécessaire avant clôture.

## Checklist UI (`DOCS/UI_STANDARDS.md`)

- [x] Aucune instanciation directe de `AgGridReact` ni de `react-select` — seul `columnDefs` modifié.
- [x] `storageKey` unique et nommé selon la convention — non affecté (grille existante inchangée).
- [x] Montants alignés droite + format fr-FR ; dates JJ/MM/AAAA triables — non affecté par cette
  TASK ; la nouvelle colonne `commentaire` est un texte libre, pas une donnée triable/filtrable par
  nature (elle dérive de 8+ champs déjà présents individuellement en colonnes filtrables).
- [x] Aucune couleur en dur ; variables CSS respectées — la nouvelle cellule n'introduit aucune
  couleur (texte simple, tooltip natif du navigateur).
- [x] `npm run lint` + `npm run build` → 0 erreur — vérifié 2 fois dans cette session (avant et
  après correction de formulation dans `phraseBorneReference`), voir logs ci-dessous.

## Logs build/lint (10/09/2026)

```
> npm run lint
[... uniquement des warnings PRÉ-EXISTANTS sur d'autres fichiers (task1xx-harness.tsx,
    ApbsGrid.tsx, api.ts, ReglementsSelection.tsx, DeclarationList.tsx,
    ConventionsDelaiPaiementPanel.tsx, DeclarationsDelaiPaiementPanel.tsx, VerifierIntegrerPanel.tsx,
    DomainGrid.tsx) — AUCUN warning sur ControleLignesDelaiPaiementPanel.tsx ...]

> npm run build
> tsc -b && vite build
✓ 1857 modules transformed.
dist/index.html                     0.68 kB
dist/assets/index-*.css           265.43 kB
dist/assets/index-*.js          1,890.47 kB
✓ built in ~2s
[avertissements pré-existants sans rapport : chunk size, dynamic import api.ts]
```

`dotnet build DeclarationTVA.slnx` : non nécessaire (aucun fichier `.cs` touché, cf. étape 1).

## Cohérence avec TASK-217

Vocabulaire repris à l'identique (« Dernière déclaration », « Constaté le », déjà en place côté
front depuis TASK-217, commit `b9ea5d3`) — pas de divergence de terminologie constatée, TASK-217
étant déjà livrée (colonnes renommées) au moment de cette implémentation.

## Risques signalés (hors périmètre de cette TASK, non corrigés)

Point additionnel détecté en lisant `ControleLignesDelaiPaiementPanel.tsx` (colonne `Statut` et
bouton `Action`, lignes ~141/161 avant cette TASK) : le code lit `p.data.estRepriseManuelleRequise`
/ `l.estRepriseManuelleRequise`, un champ qui **n'existe pas** dans `LigneSelectionDdpDto` (le champ
réel est `statut === 'RepriseManuelleRequise'`). Non typé strictement (`p: any`), donc TypeScript ne
le détecte pas à la compilation — mais à l'exécution `estRepriseManuelleRequise` vaut toujours
`undefined`, ce qui semble empêcher le badge « Reprise manuelle requise » et le bouton « Reprise
manuelle » de jamais s'afficher. **Non corrigé ici** : hors périmètre strict de TASK-218 (qui ne
touche que la nouvelle colonne `Explication`), et une correction changerait le comportement d'une
colonne existante sans validation PO préalable. Signalé pour arbitrage — à transformer en TASK
dédiée si confirmé en environnement réel.
