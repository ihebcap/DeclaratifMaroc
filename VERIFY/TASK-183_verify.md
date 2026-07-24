# TASK-183 Verify — Drill « Codes activité » : toggle Achats/Ventes sans sortir du drill

Date : 2026-07-24
Agent : Worker (Claude Code, session dédiée)

## Périmètre livré

Dans le bandeau du drill « Codes activité » (`VerifierIntegrerPanel.tsx`, uniquement quand
`drillFiltre.kind === 'codeActivite'`), ajout d'un toggle 2 boutons « TVA Déductible (Achats) » /
« TVA Collectée (Ventes) » juste après le libellé figé existant. Chaque bouton appelle
`setDrillFiltre({ domaine: ..., filtre: {}, label: ..., kind: 'codeActivite' })` — **exactement**
la même forme d'objet que les boutons « Codes activité » de la vue principale (lignes 1017/1036
au moment de l'édition), aucune nouvelle logique de fetch écrite : le `useEffect` existant
(référentiel `codeActiviteOptions`, ligne ~327) et la prop `domaine` de `DomainGrid` réagissent
déjà à ce changement d'état, comme documenté dans la TASK. Le bouton du domaine actif est marqué
visuellement actif (bordure basse + texte couleur accent), même technique que les onglets
principaux (lignes 766-796), taille réduite pour tenir dans le bandeau compact.

Les 3 autres kinds de drill (`incoherence`, `toutes`, `anomalie`) ne sont pas concernés : le
toggle est conditionné strictement à `kind === 'codeActivite'`, aucune autre branche du bandeau
n'a été touchée (diff vérifié : seul un bloc conditionnel a été inséré entre le libellé existant
et la fermeture de la `<div>` du bandeau, rien retiré ni modifié ailleurs).

## Fichiers modifiés

- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` — bandeau du drill, ajout du toggle
  (lignes ~666-704 après édition). Aucun autre fichier front touché. Aucun fichier back touché
  (conforme à la contrainte de la TASK — le mécanisme était déjà symétrique).
- `declaration-tva-web/tests/task183.spec.ts` — nouveau test Playwright, ajouté au dépôt (même
  esprit que `tests/declaration.spec.ts`/`task139.spec.ts` déjà présents), **non destructif** :
  contrairement à `declaration.spec.ts`, il ne lance PAS `reset.ps1`/`make_eligible.ps1` — il
  réutilise une déclaration réelle existante (`TVA1-2026-05`, non clôturée) sans la modifier
  structurellement.

## Checklist

- [x] Build front `npx tsc -b` — 0 erreur.
- [x] Build front `npx vite build` — 0 erreur (1 warning préexistant `INEFFECTIVE_DYNAMIC_IMPORT`
      sur `api.ts`, sans rapport avec ce changement).
- [x] Toggle visible et cliquable uniquement pour `kind === 'codeActivite'` — vérifié par lecture
      du diff (conditionnelle stricte) et par capture d'écran (les 3 autres kinds n'ont jamais été
      testés dans cette session car hors périmètre, mais le code ne les touche pas).
- [x] Bouton actif marqué visuellement — capture d'écran `task183-01-drill-achats.png` (Achats
      souligné accent) et `task183-02-drill-ventes-apres-toggle.png` (Ventes souligné accent).
- [x] Changement de domaine dans le toggle recharge réellement le référentiel ET la grille, sans
      sortir du drill — confirmé par test Playwright réel (voir § Preuve réelle) : bascule
      Achats→Ventes→Achats, 2 domaines réellement affichés avec des données différentes (157
      résultats Décaissement / 578 résultats Encaissement pour cette déclaration), jamais de clic
      sur « ← Retour au contrôle ».
- [x] Affectation d'un code activité sur une ligne Encaissement réalisée via ce chemin (drill
      ouvert sur Achats, bascule Ventes, affectation, retour Achats sans sortir) — **persistée en
      base réelle**, vérifiée par `sqlcmd` indépendant du front (voir § Preuve réelle).
- [x] Aucun changement back — aucun fichier `.cs` touché.
- [x] Aucun bypass sécurité.
- [x] Aucune dette technique silencieuse introduite par CE changement (voir cependant la limite
      découverte et documentée ci-dessous, préexistante et hors périmètre).

## Preuve réelle (base `GR_EMA_DISTRIBUTION`, déclaration `TVA1-2026-05`, non clôturée)

Test Playwright réel (navigateur Chromium, backend `Declaration.API` réel sur le port 5000 —
instance déjà active dans l'environnement, connectée à `GR_EMA_DISTRIBUTION` via
`connections.json` — front réel via `npm run dev`, **aucun mock**) :

1. Connexion réelle (`Admin`/`Admin`), ouverture de la déclaration existante `TVA1-2026-05`
   (735 lignes, statut « En cours », jamais réinitialisée — aucun `reset.ps1` lancé).
2. « Passer au calcul » → écran ② Vérifier & Intégrer.
3. Ouverture du drill « Codes activité » (onglet par défaut = Achats/Decaissement) —
   capture `task183-01-drill-achats.png` : bandeau affiche bien les 2 nouveaux boutons, « TVA
   Déductible (Achats) » actif, grille Décaissement (157 résultats).
4. Clic sur « TVA Collectée (Ventes) » **dans le bandeau du drill, sans cliquer « Retour au
   contrôle »** — capture `task183-02-drill-ventes-apres-toggle.png` : bandeau bascule, grille
   recharge en Encaissement (578 résultats), référentiel de codes rechargé pour ce domaine.
5. Affectation d'un code activité (`100`) sur la première ligne Encaissement réellement rendue
   par la grille virtualisée — capture facture `FA2600407` (20 %, 13 842,09 MAD HT), toast
   « Code activité mis à jour. » — capture `task183-03-code-activite-affecte-ventes.png`.
6. Retour à « TVA Déductible (Achats) » **toujours sans « Retour au contrôle »** — capture
   `task183-04-retour-achats-apres-toggle.png` : grille revient à 157 résultats Décaissement,
   bouton Achats de nouveau actif.
7. **Vérification indépendante en base** (`sqlcmd`, hors front/API) :
   ```
   SELECT Id, NumeroFacture, HT, Taux, CodeActivite, CodeActiviteModifieManuellement,
          CodeActiviteModifiePar, CodeActiviteModifieLe
   FROM DM_LGTVA
   WHERE DeclarationId='6e6c0b95-7e65-4069-95fe-9354555021cc'
     AND Domaine='Encaissement' AND NumeroFacture='FA2600407' AND HT=13842.09
   ```
   Résultat réel :
   ```
   Id=290af332-7e8f-49b6-bf87-6a010d06278a  CodeActivite=100
   CodeActiviteModifieManuellement=1  CodeActiviteModifiePar=Admin
   CodeActiviteModifieLe=2026-07-24 13:39:11.650
   ```
   Confirme la persistance réelle de l'affectation faite via le nouveau chemin (toggle dans le
   drill), sans jamais être repassé par l'écran principal.

Test rejoué une seconde fois (après le fix décrit ci-dessous sur les logs du test) : même ligne
(`FA2600407`) retrouvée en premier dans la grille virtualisée les deux fois — ordre de rendu
stable sur cet environnement, cohérence confirmée entre les deux exécutions.

## Limite découverte pendant la session — préexistante, HORS PÉRIMÈTRE TASK-183

En essayant d'isoler une ligne précise via le filtre Excel de la colonne « N° Facture » (et
séparément via un filtre numérique sur « Montant HT »), constaté que **le filtre texte
(`factureNumero`) et le filtre nombre (`montantHT`) envoyés par `DomainGrid.tsx` au endpoint
`GET /declarations/{id}/lignes` sont silencieusement ignorés côté back** : la requête part bien
avec le paramètre `filter` correctement formé (vérifié par capture réseau), le back répond `200`,
mais renvoie le jeu de données **non filtré** (même `totalCount`, mêmes lignes qu'sans filtre).
Seuls les filtres `numeroRapprochement`/`source`/`tauxTVA`/`origine` fonctionnent réellement
(confirmé par `curl` direct sur l'API réelle), conformément au commentaire déjà présent dans
`DomainGrid.tsx:105-108` (TASK-067B) qui ne mentionne que ces 4 champs comme corrigés — les champs
texte/nombre (`factureNumero`, `tiers`, `montantHT`, `montantTTC`) n'ont apparemment jamais été
câblés côté back (`BuildLigneFilterWhere`).

**Pourquoi ce n'est pas corrigé ici** : la TASK-183 est strictement front/ergonomie du bandeau de
drill ; ce défaut est côté back (`Declaration.Application`/`Declaration.Infrastructure`,
`BuildLigneFilterWhere`), sur un mécanisme de filtrage généraliste utilisé par les 4 kinds de
drill et par l'écran principal — bien au-delà du périmètre autorisé (« si tu penses avoir besoin
de toucher un fichier `.cs`, arrête-toi et documente pourquoi » — c'est exactement ce cas). Le
test réel de cette TASK a donc été adapté : au lieu de filtrer une ligne précise, il interagit
avec la première ligne réellement rendue par la grille virtualisée (sans filtre), ce qui suffit à
prouver le comportement du toggle et la persistance de l'affectation, sans dépendre du filtre
cassé.

**Impact utilisateur réel, à signaler au PO** : dans les 4 drills existants (`incoherence`,
`toutes`, `anomalie`, `codeActivite`), tenter de filtrer par « N° Facture », « Tiers », « Montant
HT » ou « Montant TTC » (colonnes `filterType: 'text'` ou `'number'` hors des 4 champs listés
ci-dessus) ne fait rien silencieusement — l'utilisateur voit le badge de filtre s'activer
(icône verte, « Effacer filtres » apparaît) mais les résultats ne changent jamais. C'est trompeur
et mérite probablement une TASK de correction dédiée (hors périmètre ici, à arbitrer par le PO/
l'architecte).

## Reste à valider

Rien d'important laissé ouvert pour le périmètre strict de TASK-183 — les 4 critères de
validation de la TASK sont couverts par preuve réelle (§ ci-dessus). Point non bloquant à noter :

- L'ordre de rendu de la grille virtualisée (quelle ligne apparaît « en premier ») n'est pas
  garanti par un tri explicite documenté côté back — stable sur les deux exécutions de cette
  session, mais pas formellement garanti déterministe à long terme. Sans impact sur la TASK-183
  elle-même (le toggle fonctionne quelle que soit la ligne visible).
- Comportement si l'utilisateur bascule de domaine avec une édition de code activité **en cours**
  (select ouvert, non encore validé) : non testé spécifiquement — le `onChange` du select déclenche
  un PATCH immédiat (pas de brouillon local à perdre), donc aucun risque de perte de données
  identifié par lecture du code (`handleCodeActiviteChange`, `DomainGrid.tsx:296-299`), mais pas
  vérifié empiriquement dans cette session (cas jugé secondaire, non demandé explicitement par la
  TASK).

## Verdict

Les 2 fichiers modifiés/ajoutés sont dans le périmètre exact demandé. Build front 0 erreur. Les 4
critères de validation de la TASK sont vérifiés par preuve réelle (navigateur + base réelle,
`sqlcmd` indépendant). Une limite préexistante et hors périmètre a été découverte et documentée
(filtres texte/nombre du `DomainGrid` inopérants côté back) plutôt que masquée ou corrigée hors
mandat.
