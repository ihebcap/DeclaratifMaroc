# TASK-130 Verify — Convention de délai de paiement par tiers (front)

> Implémentation worker. Front React 19/Vite/TS sur `declaration-tva-web`, consommant le back
> TASK-129 via un contrôleur HTTP TASK-130 (déjà présent, non commité, au démarrage de cette
> session — cf. § Vérification du socle back ci-dessous). Build back (`dotnet build
> DeclarationTVA.slnx`) : **0 erreur**. Build front (`npx tsc -b` + `npx vite build`) : **0
> erreur**. `npx oxlint` : uniquement des avertissements préexistants + 1 avertissement de la même
> famille que les harnais déjà en place (`task138`/`task139`), aucune nouvelle erreur. Test e2e
> Playwright dédié (`tests/task130.spec.ts`) : **4/4 verts**, captures réelles dans `VERIFY/`.

## Étape 0 — vérification du socle back (TASK-129/TASK-130 déjà en place, non commité)

Avant de coder le front, j'ai lu intégralement les 6 fichiers déjà présents (non commités par une
session précédente) et rejoué `git diff` sur les 3 fichiers modifiés :
- `Declaration.API/Controllers/ConventionsDelaiPaiementController.cs`
- `Declaration.API/Dtos/ConventionDelaiPaiementDto.cs`
- `Declaration.Application/Entities/ConventionDelaiPaiementQueryModels.cs`
- `Declaration.Application/Interfaces/IConventionDelaiPaiementTiersRepository.cs` (diff : 3
  méthodes additives)
- `Declaration.Application/Services/ConventionDelaiPaiementService.cs` (diff : 3 méthodes
  additives, pur passe-plat)
- `Declaration.Infrastructure/Repositories/ConventionDelaiPaiementRepository.cs` (diff : 3
  méthodes additives)

**Verdict : socle correct et complet, aucune retouche nécessaire.** Points vérifiés :
- Le contrôleur expose exactement le CRUD attendu (`GetAll`/`Get`/`GetFichier`/`SearchTiers`/
  `GetFacturesNonPayees`/`Create`/`Terminer`/`Delete`), route les exceptions du service
  (`ArgumentException` → 400, `InvalidOperationException` → 409) sans dupliquer le métier — tout
  le métier (plafond 180j, chevauchement bidirectionnel, unicité, bornes clôture) reste dans
  `ConventionDelaiPaiementValidator`/`ConventionDelaiPaiementService` (TASK-129, non modifiés).
- Garde société (`[Authorize]` + `UT_Admin=1` ou société dans le claim CSV `Societes`), cohérente
  avec le reste des contrôleurs de paramétrage GRF (TASK-074/128).
- Les 3 méthodes de lecture additives (`GetAllForListAsync`/`GetFacturesNonPayeesAsync`/
  `SearchTiersAsync`) respectent les deux mappings domaine déjà documentés par TASK-129
  (`CP_Domaine` direct, `RT_ECHEANCE.DO_Domaine` inversé via `ToDoDomaineErp`) — aucune confusion.
- SQL confiné à la couche repository (ARCHITECTURE §5), requêtes paramétrées.
- Build solution complète rejoué (`dotnet build DeclarationTVA.slnx`) avant de commencer le front :
  **0 erreur** (24 avertissements préexistants, aucun nouveau).

**Vérification base réelle (`GR_EMA_DISTRIBUTION`, instance locale `localhost\SQL2022` — le nom
`DESKTOP-5BFKKEP` cité dans les consignes ne résout pas sur ce poste, le nom réel de la machine est
`Iheb-PC`, connexion confirmée par `SELECT @@SERVERNAME`)** :
- `RT_CONVENTIONTIERS` : 0 ligne (comme constaté par TASK-129), schéma des 15 colonnes confirmé
  identique à celui déjà documenté par TASK-129 (`CP_Id, SO_Id, CT_No, CT_Code, CP_Numero,
  CP_DateDebut, CP_DateFin, CP_Date, CP_FileName, CP_File, CP_DelaisPaiement, RowVersion,
  CP_Domaine, CP_Type, CP_FactureNo`).
- `RT_ECHEANCE` : schéma confirmé colonne par colonne (69 colonnes) — `EC_Montant`/`EC_Solde`
  utilisées par `GetFacturesNonPayeesAsync` existent bel et bien (types `decimal`), pas une
  suppositon.
- `SearchTiersAsync` (requête exacte rejouée) : `SO_Id=1, DO_Domaine=1` (achat) → **107 tiers
  fournisseurs distincts** ; filtre `CT_Code LIKE '%441%'` → 3 résultats cohérents (`4411ELEC`,
  `4411INWI`, `4411ORANGE`).
- `GetFacturesNonPayeesAsync` (requête exacte rejouée) : tiers `CT_No=228` (`4411ELEC`, achat) →
  **3 factures non payées réelles** (`FF260076`/63,47 ; `FF260075`/939,28 ; `SI- 4411ELEC`/107,96,
  toutes `EC_Etat=0`) — la requête produit un résultat exploitable, pas un jeu vide qui masquerait
  un défaut de jointure.
- `GetAllForListAsync` : table vide → liste vide, cas trivial (aucune convention réelle en base à
  ce jour, cohérent avec TASK-129).

## Périmètre livré (front, cette session)

### 1. Écran liste — `declaration-tva-web/src/ConventionsDelaiPaiementPanel.tsx`

- Grille dense (pattern flexbox partagé en-tête/lignes, TASK-138), colonnes : Tiers (code),
  Type (badge Convention/Facture), N° convention, Dates ou N° facture (bascule selon le type),
  Délai (jours), Statut de validité (badge Valide/Expirée — Convention : `DateFin ≥ aujourd'hui`,
  calculé côté back dans le DTO ; Facture : toujours valide si rattachée), Pièce jointe (bouton de
  téléchargement si présente, `—` sinon, **jamais bloquant**), Actions.
- Filtres (`ExcelFilter`, texte sur Tiers, liste sur Type/Statut) + tri (clic sur Tiers/N°
  convention/Délai) + sélecteur de colonnes (`ColumnSelector`/`useColumnPrefs`, clé
  `grf.cols.conventionsDelaiPaiement`) — tous **côté client** : `GetAllForListAsync` (TASK-130,
  back) ne pagine pas (volume attendu par tiers/société, pas comparable aux grilles de
  règlements/factures) ; documenté comme décision assumée, pas un oubli de pagination serveur.
- Bascule Achat (fournisseurs) / Vente (clients) en tête d'écran — pilote le paramètre `domaine`
  de tous les appels.
- Téléchargement pièce jointe **authentifié via blob** (`api.get(url, {responseType:'blob'})` +
  `URL.createObjectURL` + `<a>` temporaire), PAS un `<a href>` brut — un lien direct ne porterait
  pas l'en-tête `Authorization` (JWT posé par l'intercepteur axios, `api.ts`), l'endpoint étant
  `[Authorize]`. Pattern repris à l'identique de `DeclarationFinalePanel.tsx` (déjà en place pour
  les exports XML/Excel, TASK-155).
- Action « Supprimer » ajoutée en complément de « Terminer » (confirmation navigateur explicite,
  message rappelant l'absence de garde côté serveur) — **décision assumée** : l'Objectif de la
  TASK mentionne « CRUD Convention/Facture » pour l'écran liste, alors que la section Étapes ne
  détaille explicitement que Terminer. Ajoutée car (a) le contrôleur l'exposait déjà (TASK-129/130
  back), (b) sans elle, toute convention de test créée pendant la validation manuelle resterait
  orpheline sans recours UI. Retirable sans impact si le PO juge que ce n'était pas demandé.

### 2. Formulaire de création — `CreerConventionModal` (même fichier)

- Bascule Convention/Facture (Étape 2) : Convention → Date début/Date fin (obligatoires, `Date
  fin ≥ Date début` contrôlé en JS avant tout envoi réseau) ; Facture → recherche + sélection du
  tiers puis sélection d'une facture non payée du tiers (`getFacturesNonPayees`, liste réelle).
- **Recherche tiers** débouncée (300ms), endpoint additif `SearchTiersAsync` — sans lui le
  formulaire ne pourrait désigner aucun tiers (limite déjà documentée par le back : seuls les
  tiers ayant au moins une échéance dans le domaine sont trouvables — acceptée, pas retouchée).
- **Plafond 180 jours affiché en validation immédiate** (Étape 2 de la TASK) : constante
  `PLAFOND_JOURS = 180` dupliquée côté front en lecture, **volontairement** — c'est un simple
  retour UX sans aller-retour serveur pour ce contrôle ; la vérité métier reste
  `Declaration.Core.ConventionDelaiPaiementValidator.PlafondJours` (TASK-129), le serveur
  rejetterait de toute façon en cas de divergence future entre les deux valeurs (défense en
  profondeur, pas une source de vérité dupliquée qui casserait silencieusement).
- **Upload PDF optionnel** : `<input type="file" accept="application/pdf">` → `FileReader` →
  base64 → `fileBase64` (le contrôleur back attend du JSON base64, pas multipart — cohérent avec
  le reste de l'API, décision déjà actée côté back cette session-là).
- **Message d'erreur explicite en cas de chevauchement (Étape 3)** : le message reçu du serveur
  (`err.response.data.Message`, qui cite déjà la convention en conflit — numéro + période, cf.
  `ConventionDelaiPaiementService.CreerAsync`) est affiché **tel quel** dans un bandeau rouge en
  haut du formulaire — jamais remplacé par un message générique. Vérifié explicitement par le test
  e2e C (assertion négative sur le message générique en plus de l'assertion positive sur le
  message serveur).

### 3. Décision assumée — valeur par défaut de `DateFin` à la création

Comme demandé par la note de cadrage de la TASK (aucune décision PO explicite trouvée dans
`TODO.md` sur ce point précis) : **aucune valeur pré-remplie** pour `DateDebut`/`DateFin` —
l'utilisateur saisit les deux dates, seul contrôle immédiat : `DateFin ≥ DateDebut` + délai ≤
180 jours. Évite de reproduire une valeur par défaut arbitraire qui masquerait l'incohérence
legacy §4.7 par accident. `Date` (date de saisie, `CP_Date`) est en revanche pré-remplie à
aujourd'hui — champ différent, sans lien avec la réserve legacy, décision d'ergonomie neutre.

### 4. Action « Terminer » (clôture anticipée) — `TerminerConventionModal` (Étape 4)

- Modale de saisie de la nouvelle date de fin, **bornée `[DateDebut, DateFin actuelle]` côté UI**
  via les attributs natifs `min`/`max` de l'`<input type="date">` **en plus** du contrôle back
  (`ValiderTerminer`, TASK-129) — double protection, jamais la borne UI seule.
- Validation JS de secours identique (`nouvelleDateFin < DateDebut` / `> DateFin actuelle`) pour
  les cas où la contrainte HTML5 native ne s'applique pas (ex. saisie non passée par le sélecteur
  natif) — **découverte pendant l'écriture du test e2e** (§ ci-dessous) : sur Chromium, quand la
  valeur dépasse `max`, le navigateur bloque nativement la soumission du `<form>` **avant** même
  que le gestionnaire React `onSubmit` ne s'exécute — la borne native suffit déjà à elle seule dans
  ce cas précis, le code JS de secours reste un filet de sécurité pour d'autres navigateurs/cas
  (paste, autofill) sans être le mécanisme qui a été exercé par le test e2e D.

## Fichiers créés

- `declaration-tva-web/src/ConventionsDelaiPaiementPanel.tsx` — écran liste + formulaire de
  création + modale « Terminer » (3 composants dans un seul fichier, cohérent avec le pattern
  `FactureInterrogation.tsx`/`CreateDeclarationModal.tsx` déjà en place).
- `declaration-tva-web/src/task130-harness.tsx` + `declaration-tva-web/task130.html` — harnais de
  test e2e (hors build de prod, servi uniquement par `vite dev`, pattern identique à
  `task138`/`task139`) : contourne l'absence de branchement au menu (TASK-136, hors périmètre) pour
  pouvoir tester le VRAI composant dans Chromium sans dépendre du reste de l'application.
- `declaration-tva-web/tests/task130.spec.ts` — 4 tests Playwright (création Convention, création
  Facture, rejet chevauchement, clôture anticipée + borne UI), captures dans `VERIFY/`.

## Fichiers modifiés

- Aucun fichier front existant modifié à part `declaration-tva-web/src/api.ts` — **déjà présent
  non commité** au démarrage de cette session (fonctions client `getConventionsDelaiPaiement`/
  `searchTiersConvention`/`getFacturesNonPayees`/`creerConventionDelaiPaiement`/
  `terminerConventionDelaiPaiement`/`supprimerConventionDelaiPaiement`/
  `urlFichierConventionDelaiPaiement` + types `ConventionDelaiPaiementDto`/`TiersRechercheDto`/
  `FactureNonPayeeDto`/`CreerConventionPayload`). Relu intégralement, cohérent avec le contrat
  HTTP du contrôleur (§ Étape 0) — je ne l'ai pas retouché, seulement consommé depuis
  `ConventionsDelaiPaiementPanel.tsx`.
- Aucun fichier back modifié (le socle TASK-129/130 déjà en place était correct, § Étape 0).
- **Non touchés délibérément** (hors périmètre strict, présents dans l'arbre de travail au
  démarrage de cette session, appartenant à d'autres agents travaillant en parallèle sur
  TASK-131) : `Declaration.API/Program.cs`, `Declaration.Application/Interfaces/
  IRepriseDelaiPaiementRepository.cs`, `Declaration.Application/Services/
  DelaiPaiementBootstrapService.cs`/`DelaiPaiementService.cs`,
  `Declaration.Infrastructure/Repositories/DelaiPaiementBootstrapRepository.cs`, les nouveaux
  fichiers `*SelectionDelaiPaiement*`/`ContexteDelaiPaiement.cs`, ainsi que
  `declaration-tva-web/.env.development`/`vite.config.ts` (changement de port de dev sans rapport
  visible avec cette TASK, déjà présent au démarrage de la session, jamais modifié par moi). Aucun
  de ces fichiers n'est stagé dans mon commit.

## Test e2e Playwright (`tests/task130.spec.ts`)

4 scénarios, mock réseau (`page.route`, dispatcher unique par test pour éviter toute ambiguïté
d'ordre entre plusieurs `page.route` qui se chevauchent sur le même préfixe) — VRAI composant rendu
dans Chromium, aucun besoin du backend .NET ni de la base réelle pour ces tests :

- **A — création Convention** : recherche + sélection tiers, remplissage complet, soumission,
  vérification du corps POST envoyé (`toMatchObject`) + rafraîchissement de la liste (mock GET
  stateful, piloté par l'état « créé » plutôt que par un compteur d'appels — **piège découvert
  pendant l'écriture** : React 19 `StrictMode` double-invoque les effets de montage en dev, un
  compteur de 1er/2ᵉ appel GET aurait été fragile).
- **B — création Facture** : bascule de type, sélection tiers puis facture non payée réelle
  (liste), vérification du corps POST (`factureNo` = `ecId` de la facture choisie, pas `doNumero`).
- **C — rejet chevauchement** : mock 409 avec message serveur explicite citant une convention en
  conflit → assertion positive sur le message exact **et** assertion négative sur l'absence du
  message générique (`Erreur lors de la création de la convention.`) — preuve que le message
  serveur n'est jamais écrasé.
- **D — clôture anticipée** : D1 hors borne haute → **contrainte HTML5 native** (`checkValidity()`
  vérifié explicitement, pas supposé) bloque la soumission avant React, aucun appel réseau ;
  D2 dans les bornes → succès, modale fermée, 1 appel PUT confirmé.

4/4 verts. Captures réelles (`page.screenshot`) : `VERIFY/task130-A-creation-convention.png`,
`task130-B-creation-facture.png`, `task130-C-rejet-chevauchement.png`,
`task130-D-cloture-anticipee.png` — inspectées visuellement, rendu conforme (densité, badges,
message d'erreur rouge explicite en capture C).

## Checklist

- [x] Build back `dotnet build DeclarationTVA.slnx` — 0 erreur (avant et après revue du socle,
      aucun changement back de mon fait).
- [x] Build front `npx tsc -b` — 0 erreur.
- [x] Build front `npx vite build` — 0 erreur (1 avertissement `INEFFECTIVE_DYNAMIC_IMPORT`
      préexistant, sans rapport).
- [x] `npx oxlint` — aucune nouvelle erreur (avertissements `only-export-components` sur le
      harnais, même famille que `task138`/`task139` déjà en place, pas une régression).
- [x] Test e2e Playwright dédié (`task130.spec.ts`) — 4/4 verts, captures fournies.
- [x] Écran liste : colonnes conformes à l'Étape 1, filtres/tri, sélecteur de colonnes.
- [x] Formulaire de création : bascule Convention/Facture, plafond 180j en validation immédiate,
      upload PDF optionnel.
- [x] Message d'erreur explicite (pas générique) en cas de chevauchement — vérifié par test e2e.
- [x] Action « Terminer » bornée `[DateDebut, DateFin actuelle]` côté UI + contrôle back.
- [x] Vérification base réelle des 3 endpoints de lecture additifs (`GR_EMA_DISTRIBUTION`,
      instance locale) — § Étape 0.
- [x] Aucune modification de schéma / aucune table créée.
- [x] SQL confiné à la couche repository (back, non modifié — vérifié, pas retouché).
- [x] Pas de secret codé en dur, pas de bypass sécurité (téléchargement pièce jointe authentifié
      via blob, pas un lien direct sans JWT).
- [x] Aucun fichier hors périmètre TASK-130 modifié/stagé (vérifié `git status`/`git diff` fichier
      par fichier avant `git add`, cf. § Fichiers modifiés).
- [ ] Branchement au menu — **explicitement hors périmètre** (TASK-136), l'écran n'est atteignable
      que via le harnais de test `task130.html` (hors build de prod) tant que TASK-136 n'est pas
      livrée.

## Reste à valider (NON couvert par ce VERIFY)

1. **Parcours réel de bout en bout (API .NET + base réelle) non rejoué** : le test e2e mocke
   intégralement le réseau (pattern déjà accepté pour `task138`/`task139`) — je n'ai pas démarré
   `Declaration.API` + navigué manuellement dans un vrai navigateur contre la base réelle avec une
   vraie création de convention (qui aurait laissé une ligne réelle dans `RT_CONVENTIONTIERS`,
   à nettoyer). Les 3 endpoints de lecture additifs ont été vérifiés en revanche par SQL direct
   (§ Étape 0), pas par un appel HTTP réel bout en bout.
2. **Suite Playwright complète (tous fichiers `tests/*.spec.ts`) non re-confirmée verte** dans
   cette session : un `npx playwright test` (sans filtre) a été lancé en tâche de fond mais n'a
   pas produit de résultat exploitable dans le temps de cette session (probablement des tests
   préexistants nécessitant l'API .NET réelle démarrée sur le port attendu, indépendant de mon
   changement — plusieurs specs font un login réel `Admin`/`Admin`). Seul `task130.spec.ts`
   (4/4 verts) a été confirmé explicitement. Aucune régression n'est attendue (aucun fichier
   partagé par d'autres specs n'a été modifié), mais ce n'est **pas prouvé** — à revérifier par
   l'architecte si un doute existe.
3. **Action « Supprimer »** ajoutée à l'écran liste au-delà du périmètre explicitement détaillé
   par les Étapes de la TASK (§ Fichiers créés, point 1) — décision assumée documentée, retirable
   sans impact si jugée hors périmètre par le PO.
4. **Pas de test de l'upload PDF réel** (FileReader → base64 → back) dans le test e2e — le champ
   fichier est rendu et fonctionnel (vérifié par lecture de code + build), mais aucun scénario e2e
   ne simule une sélection de fichier réelle (Playwright peut le faire via `setInputFiles`, non
   fait ici, périmètre du test limité aux 4 scénarios explicitement requis par la TASK).
5. **Limite héritée du back, non retouchée** : la recherche tiers ne trouve que les tiers ayant au
   moins une échéance dans le domaine (documenté par TASK-129/130 back) — un tiers "propre" sans
   aucune facture ne pourrait pas recevoir de convention par ce formulaire. Comportement hérité,
   pas un défaut introduit par le front.

## Verdict

Écran conventions livré (liste + création Convention/Facture + clôture anticipée), consommant sans
modification le contrat HTTP déjà posé par la session précédente (vérifié correct et complet avant
construction, § Étape 0, avec vérification indépendante des 3 endpoints de lecture additifs contre
des données réelles). Message de chevauchement serveur affiché tel quel (jamais générique), borne
UI de clôture anticipée doublement protégée (native + JS). Build back/front 0 erreur, test e2e
dédié 4/4 vert avec captures réelles. Décision assumée sur l'absence de valeur par défaut de
`DateFin` (documentée point 3). Deux points non couverts à signaler explicitement à l'architecte :
absence de parcours réel bout en bout (API+DB réelle) et suite Playwright complète non
re-confirmée dans cette session (§ Reste à valider, points 1-2).
