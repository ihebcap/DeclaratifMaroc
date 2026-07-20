# TASK-125 — Grille de règlements vide après retour de « Passer au calcul » / « Détail des lignes » (écran ① Sélection)

## Contexte

Signalement PO (19/07/2026, capture écran ① Sélection, `TVA1-2026-01`) : après avoir cliqué
« Passer au calcul » **ou** « Détail des lignes » puis être revenu à l'étape « 1. Sélection », la
grille de règlements est **entièrement vide** — « Règlements : 0 », « Sélectionnés : 0 »,
« Total sélectionné : 0,00 MAD » — alors que des règlements étaient auparavant listés et
sélectionnés.

**Distinct de TASK-096 (DONE, ne pas confondre)** : TASK-096 traitait un bug où seul le total
en pied de grille retombait à 0 (`Sélectionnés : N` restant correct, lui). Ici le PO rapporte les
**deux** compteurs à 0 simultanément, et pour **deux** déclencheurs (« Passer au calcul » en plus
de « Détail des lignes »). Le correctif TASK-096 a été revérifié en code (`ReglementsSelection.tsx`
actuel, `selectedTotal` dérivé du prop `selectedRows` du parent) : il tient toujours et
**n'explique pas** ce nouveau signalement.

## Analyse code (architecte, 19/07/2026)

- Le tunnel affiché sur la capture est toujours `DeclarationStepper.tsx` (onglets « 1. Sélection »
  / « 2. Vérifier & Intégrer » / « 3. Déclaration », `STEPS` lignes 22-26) — **pas** remplacé par un
  tunnel « 8 écrans » (TASK-090/091 ont au contraire **fusionné** des écrans, ramené à 3 étapes).
- Bascule d'onglet in-app (`goTo('reglements')`, lignes 130-133) : ne change que `activeStep`,
  `DeclarationStepper` reste monté, `selectedKeys`/`selectedRows` (state parent, lignes 50-51)
  survivent — ce chemin seul ne peut pas expliquer « Sélectionnés : 0 ».
- Or `selectedKeys.size` est lu **directement** sur le state du parent, jamais réinitialisé par un
  changement d'`activeStep` : si ce compteur tombe à 0, c'est donc `DeclarationStepper` lui-même qui
  a perdu son state — signe d'un **remontage complet** du composant, pas d'un simple recalcul
  local raté comme en TASK-096.

### Hypothèses, classées par vraisemblance (non tranchées — reproduction incomplète)

1. **H1 (la plus probable) — remontage complet de `DeclarationStepper`** : si le retour se fait via
   navigation navigateur (précédent), F5, ou réouverture de la déclaration depuis la liste plutôt
   que par clic sur l'onglet, `DeclarationStepper` redémarre à neuf (`selectedKeys=new Set()`,
   `selectedRows=[]]`) → « Sélectionnés : 0 » immédiat. `ReglementsSelection` remonte aussi et relance
   `fetchAll()` (ligne 299) ; tant que ce fetch n'a pas abouti (ou échoue, cf. H3), `allData=[]` →
   « Règlements : 0 ». La restauration de sélection persistée (lignes 307-344) est **gatée par
   `allData.length === 0`** (ligne 308) : si le fetch échoue, cette restauration ne se déclenche
   **jamais** — l'écran reste bloqué à 0/0 durablement, pas seulement le temps d'un chargement.
2. **H2 — remontage in-app de `ReglementsSelection` seul + échec du re-fetch** : chaque bascule
   d'onglet redémarre déjà `ReglementsSelection` à zéro (`allData=[]`, `knownRowsRef` neuf) et
   relance toute la pagination (`fetchAll`, lignes 240-297), **sans garde d'annulation** (contrairement
   à l'effet voisin `distincts`, lignes 225-238, qui utilise un flag `cancelled`). En cas d'erreur
   réseau/serveur, le `catch` (lignes 290-296) vide `allData` et affiche un toast. N'explique pas à
   lui seul « Sélectionnés : 0 » (compteur venant du parent) — doit se combiner à H1.
3. **H3 (backend, à confirmer par logs) — jointure cross-catalogue dans `/rapprochement`** :
   `GetReglementsRapprochementAsync` (`Declaration.Infrastructure/Repositories/DeclarationRepository.cs:810-867`)
   s'exécute sur `CreateGrfConnection()` mais joint `dbo.DM_SELECTION_REGLEMENT`/`dbo.DM_ENTTVA`
   (lignes 851-856), tables écrites via `CreatePersistenceConnection()` par
   `SaveSelectionReglementsAsync` (lignes 91-115, appelée par « Passer au calcul »/« Détail des
   lignes »). `Declaration.API/appsettings.json:10-12` sépare `GrfConnection` (`GRFN_Dummy`) et
   `PersistenceConnection` (`DeclarationTVA`) — deux catalogues distincts en config actuelle. Si
   l'environnement du PO a la même séparation, ce sous-select peut échouer/renvoyer vide après
   écriture ; le front avalerait l'erreur en `allData=[]` (mêmes lignes que H2). N'explique pas
   pourquoi le **premier** chargement réussirait avec la même requête — à vérifier en priorité.
4. **H4 (facteur aggravant, pas la cause isolée)** : `showToast` (`App.tsx:103`) n'est pas
   stabilisé (`useCallback`) — dépendance de `fetchAll` (ligne 297) et de l'effet qui l'appelle
   (ligne 299) : tout rendu d'`App` recrée `fetchAll` et redéclenche un fetch sans annuler le
   précédent, course possible avec une réponse tardive/périmée. N'est pas le déclencheur décrit par
   le PO à lui seul.

**Point bloquant non résolu** : impossible de trancher H1 vs H2/H3 sans reproduction instrumentée
(onglet réseau navigateur ouvert au moment exact du retour sur ①) — vérifier si `GET
/api/declarations/{id}` est rappelé (signe de remontage complet → H1) et si `GET /api/rapprochement`
renvoie une erreur HTTP (H3) ou un `TotalCount: 0` légitime. **Conformément à la règle projet
« ne jamais improviser un contexte manquant » : ne pas corriger à l'aveugle sur une seule
hypothèse — la reproduction instrumentée est un préalable obligatoire à l'implémentation.**

## Périmètre STRICT

- **Inclus** :
  1. Reproduire le scénario exact (sélectionner des règlements → « Passer au calcul » **et**,
     séparément, « Détail des lignes » → revenir sur ①) avec l'onglet réseau du navigateur ouvert,
     pour confirmer laquelle des hypothèses H1/H2/H3 est la cause réelle (documenter les requêtes
     observées : rejouées ou non, code HTTP, payload).
  2. Corriger la cause confirmée uniquement (pas de correctif spéculatif sur une hypothèse non
     vérifiée).
- **Exclu** :
  - Toute réouverture du périmètre déjà livré/approuvé de TASK-096 (calcul du total local,
    déjà correct).
  - Toute modification de la logique de figeage/persistance de sélection (TASK-097, déjà livrée)
    au-delà de ce qui est strictement nécessaire pour fiabiliser le rechargement après retour.
  - Changement de connexion SQL (`GrfConnection`/`PersistenceConnection`) hors correctif de la
    cause confirmée — pas de fusion de catalogues sans décision PO explicite.

## Objectif

```
Entrée  : N règlements sélectionnés sur ①, sélection persistée (TASK-097)
Défaut  : après « Passer au calcul » ou « Détail des lignes » puis retour sur ①, grille et
          compteurs (Règlements/Sélectionnés/Total) affichent 0 durablement (pas un simple flash
          de chargement)
Sortie  : au retour sur ①, la grille réaffiche les règlements du périmètre courant et la
          sélection persistée est restaurée à l'identique (compteurs cohérents avec l'état avant
          le départ vers l'étape suivante)
```

## Livrables

- Rapport de reproduction instrumentée (requêtes réseau observées) tranchant H1/H2/H3/H4.
- Correctif ciblé sur la cause confirmée.
- `VERIFY/TASK-125_verify.md` : reproduction du scénario exact du signalement PO (les deux
  déclencheurs — « Passer au calcul » et « Détail des lignes » — testés séparément), capture
  avant/après, preuve réelle (pas mock) sur une base contenant des règlements réels.

## Critères de validation

- Sélectionner des règlements sur ①, cliquer « Passer au calcul », revenir sur ① : grille et
  compteurs identiques à l'état avant le départ.
- Même test avec « Détail des lignes ».
- Aucune régression sur le correctif TASK-096 (total pied de grille) ni sur la sélection persistée
  (TASK-097).
- Build front (`tsc`/`vite`) vert ; si correctif back, build API + tests concernés verts.

## Risques / dépendances

- Risque de diagnostic erroné si le correctif est fait sans reproduction instrumentée préalable
  (4 hypothèses distinctes, mécanismes non exclusifs) — **bloquant tant que la cause n'est pas
  confirmée par capture réseau réelle**.
- Si H3 confirmée (jointure cross-catalogue) : dépendance à vérifier avec la configuration réelle
  de l'environnement PO (`appsettings.json` peut différer entre postes — cf. `DESKTOP-5BFKKEP`
  vs poste de dev).
- Aucun lien avec TASK-123 (rétrogradation net8.0, en cours, bloquant tout autre chantier API) —
  si TASK-123 n'est pas encore livrée, la reproduction back de cette task peut être empêchée par le
  même `PlatformNotSupportedException` ; signaler si observé.
