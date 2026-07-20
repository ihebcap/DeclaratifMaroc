# TASK-123 — Rétrogradation `net10.0` → `net8.0` (Microsoft.Data.SqlClient incompatible .NET 10)

## Contexte
Signalement PO (19/07/2026) : message « Serveur injoignable — vérifiez que l'API est démarrée »
persistant en écran de connexion, sur un déploiement réel (`DESKTOP-5BFKKEP:5500`). Diagnostic
architecte, par élimination successive, confirmé par reproduction directe :
- Service Windows + binaire correctement installés et démarrés (`DeclaratifMaroc` → `WinSW` →
  `Declaration.API.exe`), licence valide (`GET /api/licence/status` → 200, `estValide:true`).
- Connectivité SQL Server et requête métier exacte du contrôleur (`SELECT SO_Id, SO_RaisonSocial
  FROM P_SOCIETE ...`) rejouées manuellement et fonctionnelles.
- Le message front (`Auth.tsx:32,69`) est **trompeur** : il affiche « Serveur injoignable » pour
  n'importe quelle erreur capturée, y compris une 500 qui n'a rien à voir avec un arrêt de l'API.
- Cause réelle isolée et **reproduite à l'identique sur 3 environnements indépendants** (build
  déployé lancé localement par l'architecte, installation locale du PO sur `:5280`, déploiement
  distant `:5500` d'origine) : `GET /api/societes` → 500, avec la stack trace serveur suivante,
  levée **avant tout accès réseau**, au constructeur même de `SqlConnection` :
  ```
  System.PlatformNotSupportedException: Microsoft.Data.SqlClient is not supported on this platform.
     at Microsoft.Data.SqlClient.SqlConnection..ctor(String connectionString)
     at Declaration.Infrastructure.Factories.DbConnectionFactory.CreateGrfConnection() ... :line 23
     at Declaration.API.Controllers.DeclarationsController.GetSocietes() ... :line 437
  ```
- Hypothèses écartées et vérifiées explicitement avant la cause retenue : VC++ Redistributable
  x64 (déjà installé sur la machine cible — n'a rien changé), résolution de nom d'hôte
  cross-VM (non pertinent, service et SQL Server colocalisés sur la même machine).

## Cause racine
`Declaration.API`, `Declaration.Infrastructure` et `Declaration.Application` ciblent tous
`net10.0`/`net10.0-windows`. Le paquet `Microsoft.Data.SqlClient` **7.0.2**
(`Declaration.Infrastructure.csproj:9`, aussi référencé par `Declaration.Orchestration`,
`Declaration.Selection`) — **dernière version publiée sur NuGet.org à ce jour** (confirmé via
`dotnet list package --outdated` : aucune mise à jour disponible) — ne fournit d'assemblies
managées que jusqu'à `net9.0` (vérifié dans le cache NuGet local : dossiers `lib/net462`,
`lib/net8.0`, `lib/net9.0`, `lib/netstandard2.0` — **aucun** `lib/net10.0`). NuGet résout donc vers
l'asset `net9.0` (confirmé dans `Declaration.API.deps.json` :
`"runtimes/win/lib/net9.0/Microsoft.Data.SqlClient.dll"`), et ce build refuse de fonctionner sous
un hôte `.NET 10` qu'il ne reconnaît pas comme validé : garde-fou interne du paquet, pas une vraie
limitation de plateforme — il lève `PlatformNotSupportedException` au premier `new
SqlConnection(...)`, systématiquement, sur tout déploiement du code actuel.

Conséquence : **toute** route qui construit une `SqlConnection`
(`DbConnectionFactory.CreateGrfConnection()` / `CreatePersistenceConnection()` /
`GetSageConnectionInfoAsync()`) est cassée — `GET /api/societes`, `POST /auth/login`, et par
extension l'ensemble du produit, sur tout poste, indépendamment de l'installation/configuration
locale.

## Objectif
```
Entrée  : solution actuelle ciblant net10.0/net10.0-windows, Microsoft.Data.SqlClient 7.0.2 cassé
Traitement : rétrogradation du TargetFramework vers net8.0/net8.0-windows sur les projets
             concernés + vérification de compatibilité des autres dépendances déjà en 10.x.x
Sortie : build + déploiement fonctionnels, GET /api/societes (et toute route SQL) opérationnelle,
         sans changement de comportement fonctionnel
```

## Choix de version — net8.0 plutôt que net9.0
Les deux TFM disposent d'un asset **dédié** dans `Microsoft.Data.SqlClient` 7.0.2 (`lib/net8.0` et
`lib/net9.0`), donc les deux corrigeraient techniquement le crash. Retenu : **`net8.0`**, pas
`net9.0`, pour une raison de cycle de support, pas de préférence arbitraire :
- **.NET 9** est un cycle **STS** (Standard Term Support, 18 mois) : sorti nov. 2024 → fin de
  support ~mai 2026. **À la date de cette task (19/07/2026), .NET 9 est déjà hors support.**
  Le retenir déplacerait le problème (runtime sans correctifs de sécurité) sans bénéfice.
- **.NET 8** est **LTS** (3 ans) : sorti nov. 2023 → fin de support nov. 2026. Encore actif à la
  date de cette task, et choix cohérent (LTS) en attendant que `Microsoft.Data.SqlClient`
  supporte officiellement `net10.0` (LTS, jusqu'à nov. 2028 — cible long terme du projet).
- Alternative écartée : revenir à `System.Data.SqlClient` (ancien driver ADO.NET) pour contourner
  le problème plutôt que de changer de TFM. **Rejetée** : ce paquet est déprécié depuis 2019, en
  maintenance pure (pas de nouvelles fonctionnalités, correctifs de sécurité minimaux), Microsoft
  recommande explicitement de migrer *vers* `Microsoft.Data.SqlClient`, pas l'inverse — remplacer
  un problème de compatibilité TFM bien identifié par un driver non maintenu est un mauvais
  compromis pour une application appelée à vivre plusieurs années.

## Périmètre STRICT
- **Inclus** :
  1. `Declaration.API.csproj` : `<TargetFramework>net10.0-windows</TargetFramework>` →
     `net8.0-windows`.
  2. `Declaration.Infrastructure.csproj` : idem → `net8.0-windows`.
  3. `Declaration.Application.csproj` : `<TargetFramework>net10.0</TargetFramework>` → `net8.0`.
  4. Tout autre projet de la solution ciblant `net10.0`/`net10.0-windows` et compilé dans la même
     chaîne de dépendance que l'API (`Declaration.Orchestration`, `Declaration.Selection`,
     `Declaration.Core`, `Declaration.Export.Xml`, etc. — à énumérer exhaustivement par un `grep
     -rn "TargetFramework>net10" --include=*.csproj` avant de commencer, périmètre non fermé tant
     que cet inventaire n'est pas fait).
  5. Vérification et, si nécessaire, repli de version pour chaque paquet actuellement épinglé en
     `10.x.x` transitivement entraîné par le TFM (au moins observés dans `deps.json` à ce jour :
     `Microsoft.Extensions.Hosting.WindowsServices 10.0.9`, `Microsoft.Extensions.Configuration.*`
     versions `10.0.9`/`10.0.10` — vérifier qu'une version `8.0.x` compatible existe pour chacun ;
     sinon la rétrogradation du seul TFM ne suffira pas à faire compiler la solution).
  6. `Deploy-All.ps1` : le commentaire ligne 58-62 explique pourquoi `-r win-x64
     --self-contained true` est obligatoire (sans rapport avec ce changement, à laisser tel quel)
     — vérifier seulement qu'aucune référence en dur à `net10.0` ne subsiste dans les chemins de
     publication.
- **Exclus / hors périmètre** :
  - Toute logique métier, requête SQL, contrôleur — aucun changement de comportement attendu, ce
    correctif est un changement de plateforme de compilation pur.
  - `Declaration.Setup` (`Declaration.Setup.csproj`) : cible aussi `net10.0-windows` mais ne fait
    aucun appel SQL direct (l'installeur ne construit pas de `SqlConnection`) — **à vérifier**
    plutôt qu'à changer par défaut ; ne rétrograder que si la compilation en solution l'exige
    (référence croisée) ou si un test révèle un besoin réel.
  - Retour à `System.Data.SqlClient` (écarté ci-dessus, décision actée).
  - Toute tentative de contournement via `AppContext`/switch runtime pour forcer
    `Microsoft.Data.SqlClient` à accepter `net10.0` : non officiel, risqué pour un composant qui
    touche chaque appel SQL de l'application — écarté.

## Étapes
1. `grep -rn "TargetFramework>net10" --include=*.csproj .` pour lister exhaustivement les projets
   concernés (fermer le point ouvert du périmètre, item 4 ci-dessus).
2. Changer le `TargetFramework` de chaque projet listé de `net10.0`/`net10.0-windows` vers
   `net8.0`/`net8.0-windows`.
3. `dotnet restore` puis `dotnet build` sur la solution complète : résoudre chaque incompatibilité
   de version de paquet remontée (repli vers la version `8.0.x`/compatible la plus proche de
   chaque paquet actuellement en `10.x.x`, sans changer de paquet ni de fonctionnalité).
4. `dotnet publish Declaration.API -c Release -o <dossier temporaire> -r win-x64 --self-contained
   true` : vérifier que l'asset résolu pour `Microsoft.Data.SqlClient` dans le `deps.json` généré
   est bien `lib/net8.0/Microsoft.Data.SqlClient.dll` (plus de fallback).
5. Lancer l'exécutable publié localement, appeler `GET /api/societes` et `POST /auth/login` :
   confirmer `200 OK` (fin du `PlatformNotSupportedException`).
6. Rejouer l'intégralité de la suite de tests (`Declaration.Orchestration.Tests` et tout autre
   projet de test de la solution) : 0 régression attendue, aucun changement de logique.
7. Rejouer `Deploy-All.ps1` de bout en bout (build front + API + workers + `DeclaratifMaroc.exe`) :
   confirmer un déploiement complet sans erreur avec le nouveau TFM.
8. Re-tester en conditions réelles sur au moins un poste ayant reproduit le bug initial
   (`DESKTOP-5BFKKEP`) : `GET /api/societes` opérationnel, écran de connexion fonctionnel de bout
   en bout (liste sociétés + login).

## Livrables
- Diff des `.csproj` modifiés (TFM + éventuels repli de version de paquets).
- Sortie `dotnet build` de la solution complète : 0 erreur.
- Sortie `dotnet publish` de `Declaration.API` : extrait du `deps.json` généré montrant l'asset
  `net8.0` de `Microsoft.Data.SqlClient` (pas de fallback `net9.0`/`netstandard2.0`).
- Résultat des suites de tests rejouées (0 régression).
- Preuve réelle : capture ou log de `GET /api/societes` → 200 sur le poste `DESKTOP-5BFKKEP` (ou
  environnement de reproduction équivalent), après redéploiement.
- `VERIFY/TASK-123_verify.md` documentant chaque étape ci-dessus avec preuves brutes (pas de
  résumé narratif substituant la preuve).

## Critères de validation
- Plus aucune occurrence de `net10.0`/`net10.0-windows` dans les projets listés à l'étape 1 du
  périmètre (grep de contrôle à 0 résultat, sauf exclusions explicitement actées).
- `GET /api/societes` et `POST /auth/login` répondent sans `PlatformNotSupportedException`, sur un
  environnement ayant reproduit le bug initial (pas seulement en dev).
- Build solution complète : 0 erreur, 0 nouvel avertissement lié à une incompatibilité de version.
- Suite de tests existante : 0 régression (mêmes résultats qu'avant le changement de TFM).
- `Deploy-All.ps1` rejoué de bout en bout sans erreur.
- Aucune modification de logique métier, de requête SQL ou de comportement fonctionnel — le diff
  attendu se limite aux fichiers de projet (`.csproj`) et, si nécessaire, aux versions de paquets
  NuGet transitifs.

## Risques / dépendances
- **Risque principal** : des paquets actuellement épinglés en version `10.x.x`
  (`Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.Extensions.Configuration.*`, etc.)
  peuvent ne pas exister en version `8.0.x` strictement compatible avec les API utilisées par le
  code actuel — un simple changement de `TargetFramework` pourrait ne pas suffire à faire compiler
  la solution. À traiter paquet par paquet à l'étape 3, sans improviser de version au hasard.
- **Risque de régression silencieuse** : un comportement subtil de `Microsoft.Data.SqlClient`
  pourrait différer entre l'asset `net8.0` et l'asset `net9.0`/fallback actuel (bien que non
  attendu, les deux étant des builds officiels du même paquet) — d'où l'exigence de rejouer la
  suite de tests complète et une preuve réelle sur base `GR_EMA_DISTRIBUTION`, pas seulement un
  build vert.
- **Dépendance amont** : ce correctif est bloquant pour toute exploitation du produit en l'état
  actuel (`net10.0` cassé) — priorité haute, à traiter avant toute nouvelle fonctionnalité back
  touchant la couche SQL.
- **Suivi distinct, hors périmètre de cette task** : `Auth.tsx` (lignes 32 et 69) affiche « Serveur
  injoignable — vérifiez que l'API est démarrée » pour **toute** erreur capturée, y compris une
  erreur HTTP 500 sans rapport avec un arrêt de l'API — ce message trompeur a retardé le diagnostic
  de plusieurs échanges. Corriger ce point (distinguer erreur réseau réelle vs erreur serveur avec
  code HTTP, afficher le détail utile) mériterait sa propre task front, signalée ici mais non
  traitée.
- **Retour à `net10.0`** : dès qu'une version de `Microsoft.Data.SqlClient` publiera un asset
  `net10.0` officiel, ré-évaluer un retour (nouvelle task, pas un simple revert — vérifier d'ici là
  si d'autres paquets auront eux aussi migré).
