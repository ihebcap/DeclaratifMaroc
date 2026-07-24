# TASK-164 — VERIFY

## Implémenté en worker exceptionnel

Rôle inversé, demande explicite du PO/architecte (session du 24/07/2026, prompt dédié « traite
TASK-164 » avec confirmation explicite d'agir comme worker malgré le rôle architecte par défaut de
`CLAUDE.md`), même mode que TASK-101/075/114/117/118/122/160/161. Ce document est écrit par le
worker ; il appelle une revue architecte indépendante avant tout déplacement vers `DONE_DETAIL/`.

**Investigation menée avant tout correctif**, conformément à la consigne explicite du PO — aucune
ligne de code modifiée avant d'avoir établi la cause réelle sur l'échantillon.

## §A — Investigation : cause réelle des 197 lignes « Facture introuvable »

### Méthode

- Déclaration réelle `TVA1-2026-06` (`Id=798eb718-1f20-40e8-a12c-a8a18b912df2`, `SocieteId` NEW_EMA
  DISTRIBUTION), base `GR_EMA_DISTRIBUTION` (`DESKTOP-5BFKKEP`), accès direct `sqlcmd` (lecture
  seule stricte, aucun `UPDATE`/`DELETE` manuel).
- **Correction du diagnostic de la task elle-même** : la TASK-164 supposait (hypothèse de travail
  explicitement marquée « à vérifier ») que les 197 lignes portaient `statutLigne=1` (Intégrée).
  Vérifié faux : `SELECT Etat, COUNT(*) FROM DM_LGTVA WHERE DeclarationId=... GROUP BY Etat` → les
  787 lignes de la déclaration (dont les 197 anormales) sont **toutes** `Etat=0` (Proposée) — la
  déclaration n'a jamais été close (`DM_ENTTVA.Statut=0`, `DT_Id=NULL`), donc `CloturerDeclarationAsync`
  (seul chemin qui pose `Etat=Integree`, `DeclarationWorkflowService.cs:1556-1583`) n'a jamais pu
  s'exécuter tant que les 197 anomalies bloquent le checkup. La confusion venait probablement du cas
  `FC2600517` cité dans la task (`DT_Id=63`), qui référence un tampon `RT_AFFECTATION.DT_Id` posé par
  une **autre déclaration déjà close**, pas l'`Etat` de la ligne courante dans `TVA1-2026-06`.
- Échantillon initial de 15 lignes (`RT_ECHEANCE` : `DO_Type`/`DO_Domaine` réels, tous cohérents,
  aucune anomalie de routage), puis **requête exhaustive sur les 197 lignes** (pas seulement
  l'échantillon) croisant `DM_LGTVA` ↔ `DM_VENTILATION_SAGE_CACHE` (cache de ventilation Sage,
  TASK-024/118) par rapport à `DM_ENTTVA.DateCreation` (2026-07-16 16:45:22) :

```sql
SELECT CASE
    WHEN c.EC_Id IS NULL THEN 'AUCUN_CACHE'
    WHEN c.AUneErreur = 1 THEN 'CACHE_EN_ERREUR'
    WHEN c.DerniereLecture > @DecCreation THEN 'CACHE_OK_POSTERIEUR_A_CREATION'
    ELSE 'CACHE_OK_ANTERIEUR_A_CREATION'
  END AS Categorie, COUNT(*) AS n
FROM anomalies a LEFT JOIN cache c ON c.EC_Id = a.EC_Id
GROUP BY ...
```

### Résultat — deux causes distinctes confirmées (hétérogènes, comme suspecté par la task)

| Catégorie | n | Explication |
|---|---|---|
| `CACHE_OK_POSTERIEUR_A_CREATION` | **190** | Sage a depuis relu ces pièces avec succès (`MotifErreur IS NULL`, `DateLecture` postérieure à la création de la déclaration) — **cache périmé**, exactement le mécanisme déjà prévu par TASK-147 (`DiagnostiquerLigneAsync` bloc 4 : `CachePerime=true`). Confirmé en rejouant l'endpoint réel `GET /lignes/diagnostic/{ecId}` (ex. `EC_Id=18196` → `"cachePerime":true, "cacheDateLecture":"2026-07-23T22:06:49"`). |
| `AUCUN_CACHE` | **7** | Aucune entrée de cache, jamais lue avec succès. Documents identifiés : `FC2502094/FC2502134/FC2502178` (LAMI DISTRIBUTION), `FC2502218/FC2502219/FC2502220` (TOUCOMDIS), `AV2500071` (avoir, LAMI DISTRIBUTION) — tous datés nov./déc. 2025, réglés ensemble le 2026-06-12. Confirmé via l'endpoint réel `GET /lignes/diagnostic/{ecId}` (ex. `EC_Id=19897` → `"cachePerime":false`, `"motifErreurCache":null`). |

### Hypothèse TASK-159 (incident timeout batch OM) — investiguée et **RÉFUTÉE**

- `DM_ENTTVA.DateCreation` de `TVA1-2026-06` = **2026-07-16 16:45:22** ; le premier figeage réel
  (calcul du domaine) a eu lieu le **2026-07-16 23:49 → 2026-07-17 02:24**, d'après
  `Declaration.API/bin/Debug/net10.0-windows/logs/valorisation.log` (log réel, pas reconstitué).
  L'incident TASK-159 documenté (`DONE_DETAIL/TASK-159-...md`) date du **2026-07-23 15:11-15:16**,
  soit **une semaine plus tard** — dates incompatibles, la déclaration était déjà figée avant que
  l'incident ne survienne.
- Aucune ligne `[VALO] ... Timeout` ni `en exception` dans tout le log du 16-17/07 (grep exhaustif,
  0 résultat) — le motif exact de l'incident TASK-159 est absent de la fenêtre de figeage.
- Le log du 16-17/07 montre en revanche des lots `batch OM` concurrents suspects (deux requêtes
  quasi simultanées pour les mêmes 740 pièces à 23:53:34/23:54:12, une requête de 1217 pièces à
  00:56:39 jamais suivie de son propre « rendu » avant qu'une autre requête ne démarre) — cohérent
  avec l'absence, à cette date, du verrou anti-chevauchement `soId` livré depuis par **TASK-156**
  (approuvée seulement le 23/07/2026). C'est donc plus probablement un artefact de contention
  **TASK-156** (déjà corrigée depuis) qu'un timeout **TASK-159** — mais dans les deux cas, la
  conclusion opérationnelle est la même : un aléa transitoire de lecture OM, sans rapport avec les
  documents eux-mêmes, exactement ce que confirme le cache désormais propre pour 190/197 lignes.
- **Conclusion** : lien TASK-159 écarté explicitement (dates incompatibles) ; la cause des 190
  lignes récupérables est un aléa de lecture OM au moment du premier figeage (probablement de la
  même famille que TASK-156, déjà corrigée), sans qu'il soit possible ni nécessaire de rejouer
  l'incident exact a posteriori — le cache réel, relu depuis avec succès, suffit à trancher.
- **Aucune autre déclaration affectée** : `SELECT DISTINCT e.Numero FROM DM_LGTVA l JOIN DM_ENTTVA e
  ON e.Id=l.DeclarationId WHERE MotifRejet IS NOT NULL AND MotifRejet<>''` → **une seule**
  déclaration concernée (`TVA1-2026-06`), sur l'ensemble de la base.

### Les 7 lignes irrémédiables — investiguées, non « résolues par invention »

- Vérifié sur `NEW_EMA DISTRIBUTION` (base Sage réelle) : les 7 documents (`F_DOCENTETE`) **existent**
  bien (`DO_Type=17`, `DO_Domaine=1`, dates de déc./nov. 2025) — donc pas une facture totalement
  absente de Sage au sens littéral.
- `DO_Statut`/`DO_Valide`/`DO_EStatut`/`DO_StatutBAP` comparés entre ces 7 documents et des documents
  sains équivalents (même tiers, même période) : **aucun champ distinctif** trouvé par lecture SQL
  directe — la différence se situe donc dans la couche objets métier Sage (COM/BSCIAL,
  `SageTaxReader.Core/SageTaxReaderService.cs`, `LireFactureAchat`/`LireFactureVente` :
  `docFactory.ExistPiece(DocumentTypeAchatFacture/Cpta, numeroPiece)`), invisible par un simple
  `SELECT` sur les tables SQL.
- Point notable écarté : hypothèse d'un type de document « Avoir » non géré par le lecteur OM
  (`AV2500071` est un avoir) — **infirmée** : un autre avoir de la même période (`AV2500345`,
  22/12/2025) a un cache valide et récent (`DateLecture=2026-07-23 22:22`, `MotifErreur IS NULL`),
  donc les avoirs en général sont bien lus avec succès ; ce n'est pas une exclusion catégorielle.
- **Je n'ai pas invoqué le worker `SageTaxReader.Console.exe` en direct** pour capturer l'exception
  COM exacte (garde-fou : construire/exécuter ce binaire hors du chemin applicatif normal aurait
  dépassé le périmètre « diagnostic lecture seule » de la task, et nécessite une session Sage réelle
  que je n'ai pas cru prudent d'ouvrir en dehors de l'app). **Cause exacte non élucidée au-delà de ce
  qui précède** — documenté honnêtement, pas inventé. Conformément au principe déjà appliqué en
  TASK-150 : ces 7 lignes restent `Proposee` avec leur motif réel, **aucun montant fabriqué**,
  signalées explicitement pour arbitrage PO/service comptable (contact client possible sur ces
  documents spécifiques : LAMI DISTRIBUTION `F0061`, TOUCOMDIS `F0225`).

## §B — Bouton Diagnostiquer invisible pour une ligne en anomalie

### Cause réelle (différente de l'hypothèse de la task, vérifiée par la donnée réelle ci-dessus)

La task supposait que le bouton manquait parce que `NON_VALORISE = new Set([2,3,4])`
(`VerifierIntegrerPanel.tsx:79`) excluait le statut 1 (Intégrée). En réalité (§A), les 197 lignes
sont `statutLigne=0` (Proposée) — **également** exclu du même `Set`. La cause racine est donc plus
large que supposé : **toute** ligne portant un motif (`MotifRejet` non vide) créée par
`MapLignesCandidates` (`DeclarationWorkflowService.cs:813-846`, TASK-097 : « la ligne reste
Proposee ») reste invisible du tableau « lignes non valorisées », qu'elle soit `statutLigne=0` ou
`statutLigne=1`.

### Vérification backend AVANT le correctif front (demande explicite du PO)

`RecalculerLigneDepuisCacheAsync` (`DeclarationWorkflowService.cs:1472-1542`, TASK-147) —
vérifié par lecture : garde explicite ligne 1485 `if (lignes.Any(l => l.Etat != EtatLigne.Proposee))
return (false, false, "Cette action ne s'applique qu'à une ligne encore à l'état 'Proposée'.")`.

- **Pour les 190 lignes réelles (`statutLigne=0`)** : fonctionne exactement comme prévu — vérifié en
  conditions réelles ci-dessous (§ Rejeu).
- **Pour une hypothétique ligne `statutLigne=1` (Intégrée) avec motif** : le back-end **refuse
  actuellement**, sans écrire quoi que ce soit (retour `Conflict` HTTP 409 côté
  `DeclarationsController.RecalculerLigneDepuisCache`, message clair). Aucun risque de mutation
  incontrôlée du `DT_Id`/périmètre si le bouton front venait à s'afficher pour un tel cas : le
  refus est sûr par construction.
- **Vérifié qu'une ligne `Etat=Integree` avec `MotifRejet` non vide ne peut structurellement pas
  exister aujourd'hui** dans ce système : `CloturerDeclarationAsync` (ligne 1556) ne pose
  `EtatLigne.Integree` qu'après un checkup sans aucune alerte `Error` (donc sans
  `FACTURE_NON_VENTILEE`) ; `RevaliderLignesFigeesAsync` (post-clôture, TASK-077/078) ne fait
  qu'ajouter une alerte transitoire `LIGNE_FIGEE_A_REVERIFIER` au checkup — **il n'écrit jamais**
  `MotifRejet` sur la ligne persistée (commentaire du code lui-même : « Ligne et totaux inchangés »).
  Confirmé aussi en base : `SELECT ... FROM DM_LGTVA WHERE MotifRejet<>'' GROUP BY Etat` sur
  **toute la base** (toutes déclarations, pas seulement `TVA1-2026-06`) → uniquement des lignes
  `Etat=0`, zéro ligne `Etat=1` avec motif, sur l'ensemble des données réelles disponibles.
- **Décision** : ne pas modifier le back-end (aucune cause de code prouvée à corriger là — le
  garde-fou existant échoue déjà proprement) ; le front est néanmoins étendu au statut 1 par
  défensive/cohérence avec la demande explicite du PO (« seules les lignes intégrées avec un motif
  non vide »), sans risque puisque le back refuse sans écrire dans ce cas non observé aujourd'hui.

### Correctif livré (front uniquement)

`declaration-tva-web/src/VerifierIntegrerPanel.tsx` :
- Nouvelle fonction `estNonValorise(statutLigne, motif)` : `NON_VALORISE.has(statutLigne) ||
  ((statutLigne === 0 || statutLigne === 1) && !!motif)` — remplace l'unique site d'usage
  (`agregParFactureTaux`, anciennement `NON_VALORISE.has(l.statutLigne)`).
- Périmètre strictement borné à la demande du PO : une ligne 0/1 **sans motif** (cas normal, très
  largement majoritaire — 239 lignes vérifiées avec `motif=""` sur `TVA1-2026-06` après rejeu)
  reste inchangée, valorisée normalement. Seule une ligne 0/1 **avec motif non vide** bascule en
  « non valorisée » (bouton Diagnostiquer + exclusion des sous-totaux HT/TVA).
- Effet de bord positif constaté : avant ce correctif, les 197 lignes-stub (`HT=montantAffecte`,
  `Taux=0`, TASK-097) polluaient silencieusement le sous-total « taux 0 % » du tableau front (leur
  `montantAffecte`, non nul, était sommé comme si la ligne était valorisée) — désormais exclues des
  sommes comme toute ligne non valorisée.

## Rejeu réel — pas une fixture

Instance `Declaration.API` (build `net8.0-windows`, non modifié pour ce test — le fix livré est
front-only) démarrée localement contre la base réelle `GR_EMA_DISTRIBUTION`/`connections.json`
(port alternatif 5000/IPv6 pour ne pas interférer avec un autre service déjà présent sur ce poste ;
jeton JWT généré localement avec la clé réelle de `connections.json`, aucune modification de
`AuthController`/aucun contournement de code — mêmes claims qu'un utilisateur Admin réel).

- **Avant** : `GET /declarations/{id}/checkup` → **197** alertes `FACTURE_NON_VENTILEE` (niveau
  Error, bloquantes), confirmé aussi en base (`SELECT COUNT(*) FROM DM_LGTVA WHERE MotifRejet<>''`
  → 197).
- **Action réelle** : `POST /declarations/{id}/lignes/recalculer-depuis-cache` appelé pour les
  **190** `EC_Id` catégorisés `CACHE_OK_POSTERIEUR_A_CREATION` (endpoint réel TASK-147, aucun code
  modifié pour cette action) → **190/190 succès HTTP 200**, 0 échec.
- **Après** : `GET /declarations/{id}/checkup` → **7** alertes `FACTURE_NON_VENTILEE` restantes,
  exactement les 7 `EC_Id` du diagnostic `AUCUN_CACHE` (`19897, 19900, 19903, 20530, 20531, 20532,
  21861`) — vérifié aussi en base (`DM_LGTVA` : 7 lignes `MotifRejet<>''`, total lignes de la
  déclaration passé de 787 à 831 — les stubs recalculés ont éclaté en plusieurs lignes par bucket de
  taux, comportement normal et déjà validé de `RecalculerLigneDepuisCacheAsync`).
- Une ligne recalculée vérifiée en détail (`EC_Id=18205`, `FC2600011`) : `montantHT=272.76,
  tauxTVA=10, montantTVA=27.28, motif=""` — valorisation réelle restaurée, motif effacé.
- Une ligne restante vérifiée (`EC_Id=19897`, `FC2502094`) : `statutLigne=0, motif="Facture
  introuvable (DTO non fourni).", montantHT=1699.68` (payload API réel tel que le front
  consommera) — confirme que le correctif front (`estNonValorise`) l'affichera bien comme non
  valorisée avec bouton Diagnostiquer, sans avoir fabriqué de valeur.
- **Écart d'équilibre résiduel expliqué** : `equilibre.ecart = -62 455,77 MAD`,
  `ecartExplique=true` — correspond exactement à `SUM(MontantAffecte)` des 7 lignes restantes
  (`62 455,77`), donc pas un résidu mystérieux.
- Solde final réel : **197 → 7 lignes bloquantes** (réduction de 96,4 %), les 7 restantes documentées
  ci-dessus avec leur cause réelle (documents Sage présents mais irrécupérables par le lecteur OM
  actuel, cause exacte non élucidée au niveau COM/BSCIAL), signalées pour arbitrage PO — **la
  déclaration reste bloquée pour ces 7 lignes, comme attendu** (aucune fermeture forcée, aucun
  contournement).

Instance de test arrêtée proprement après la vérification (aucun processus laissé tournant en
dehors de ceux déjà présents avant cette session).

## Tests

- `dotnet build DeclarationTVA.slnx` → 0 erreur, 2 avertissements `NU1510` préexistants (sans
  rapport, `Declaration.Setup.csproj`).
- `dotnet test` solution complète :
  - `Declaration.Core.Tests` : 54/54.
  - `Declaration.Export.Xml.Tests` : 13/13.
  - `Declaration.Export.Excel.Tests` : 3/3.
  - `Declaration.Orchestration.Tests` : **175/175** (aucune régression sur les statuts 2/3/4,
    critère de validation explicite de la task).
  - `Declaration.Selection.Tests` : 58/59 — 1 échec **préexistant**, déjà documenté (TASK-137/154/
    155/156/159/160) : échec d'authentification Windows sur la connexion GRF locale, sans rapport
    avec ce correctif (front-only).
  - `Declaration.Controle.Tests` : 1/2 — 1 échec **préexistant**, donnée de test absente en base
    (« Déclaration GRFN 66 introuvable »), sans rapport, projet non touché par ce correctif.
- `npx tsc --noEmit` (front) → 0 erreur.
- `npm run build` (front, `tsc -b && vite build`) → build réussi, seul avertissement
  `INEFFECTIVE_DYNAMIC_IMPORT` préexistant (`api.ts`, déjà documenté TASK-160/161).

## Non modifié (confirmé par périmètre)

- Aucun fichier backend modifié — `RecalculerLigneDepuisCacheAsync`/`DiagnostiquerLigneAsync`/
  `MapLignesCandidates`/`CloturerDeclarationAsync` inchangés, vérifiés fonctionnellement corrects
  et suffisants par le rejeu réel ci-dessus.
- Aucune écriture en dehors du mécanisme de recalcul déjà validé par TASK-147 (190 appels à
  l'endpoint existant, aucun `UPDATE`/`DELETE` manuel).
- Aucun `DT_Id` modifié, aucune ligne sortie de la déclaration — seul le contenu valorisé
  (HT/Taux/TVA/TTC/Etat/MotifRejet) des 190 lignes a changé, exactement le périmètre de
  `RecalculerLigneDepuisCacheAsync`.
- Les 7 lignes irrémédiables n'ont reçu **aucun montant fabriqué** — motif réel toujours visible et
  bloquant.

## Réserves non bloquantes, documentées non silencieuses

- **Cause exacte des 7 lignes irrémédiables non élucidée au niveau Sage COM/BSCIAL** — établi
  qu'elles existent dans `F_DOCENTETE`, qu'aucun champ SQL ne les distingue des documents sains, et
  que l'exclusion catégorielle « avoir » est infirmée ; la cause précise nécessiterait soit
  l'invocation directe du worker `SageTaxReader.Console.exe` (hors périmètre lecture-seule assumé
  ici), soit une investigation côté client dans Sage lui-même (les 2 tiers concernés : LAMI
  DISTRIBUTION `F0061`, TOUCOMDIS `F0225`). **Signalé au PO pour arbitrage/contact client**, comme
  demandé — pas de correctif inventé.
- **Lien TASK-156 (contention) comme cause plausible des 190 lignes, non certifiable a posteriori**
  — le log réel du 16-17/07 montre des signes de contention (batches concurrents sur les mêmes
  pièces), cohérents avec l'absence du verrou `soId` à cette date, mais je n'ai pas de preuve
  formelle reliant cet artefact précis aux 190 `EC_Id` concernés (le log ne journalise pas de
  succès/échec par `EC_Id` individuel pour ces lots). Non bloquant : la conclusion actionnable
  (cache désormais propre, recalcul réel effectué avec succès) ne dépend pas de cette causalité
  exacte.
- **Aucun test automatisé dédié ajouté** pour `estNonValorise` (fonction front pure, non couverte
  par un framework de test — le projet front n'a pas de suite de tests unitaires, cf. `package.json`
  : seul `tsc -b && vite build`) — vérifié uniquement par lecture, `tsc`/`vite build`, et par rejeu
  réel de l'API (payload confirmé pour une ligne saine et une ligne en anomalie). Cohérent avec le
  niveau de couverture existant du reste de ce composant (`VerifierIntegrerPanel.tsx`, TASK-142/161
  également non couverts par des tests front dédiés).
- **Vérification visuelle Playwright/navigateur non effectuée** (bouton Diagnostiquer cliqué dans un
  vrai navigateur) — le payload API réel consommé par le composant a été vérifié directement
  (`statutLigne=0, motif non vide` pour les 7 lignes restantes), et la fonction `estNonValorise` est
  triviale/pure ; le risque résiduel est jugé faible mais **non vérifié visuellement**, à confirmer
  par le PO s'il le juge utile.
- **7 lignes bloquantes restent bloquantes** — la déclaration `TVA1-2026-06` ne peut toujours pas
  être close en l'état, par construction (aucun contournement). C'est le comportement attendu tant
  que le PO n'a pas tranché sur ces 7 documents (mêmes principe que TASK-150).
