# TASK-117 — Intégration vérification de licence (ApLicence) dans Declaration.API (GRF)

## Contexte
Analyse en cours (17/07/2026, PO) du cahier des charges
`D:\_vibe\apbs-gr_winform\analayse\CDC-LICENCE-LECTURE-SEULE-APPLI-TIERCE.md` : une **librairie
partagée indépendante** (nouveau repo à part, pas encore créé) doit encapsuler la vérification de
licence en lecture seule (protocole `ApLicence.Core.Net`, sans consommation de siège) pour être
consommée par plusieurs applications tierces. Le CDC citait initialement `GRC_WEB` comme cas
concret déjà en prod (bypass à corriger, `TresorerieNinjectKernel.cs`). **Confirmation PO (17/07/2026,
cette session) : GRF (`Declaration.API`) est également candidat** — doit lui aussi vérifier une
licence active au démarrage et périodiquement (toutes les 24h, valeur fixe non paramétrable), et
bloquer l'accès fonctionnel (pas le process) si la licence est absente/invalide/expirée.

Ce TASK documente **le côté consommateur** (GRF) de cette future librairie — il ne construit pas la
librairie elle-même, qui est un projet séparé (autre repo).

## Périmètre STRICT
- **Inclus** :
  1. Référencer la future librairie de licence depuis `Declaration.API` (mode de distribution à
     confirmer : NuGet interne / référence projet — dépend de l'avancement du nouveau repo).
  2. Initialiser au démarrage (`Program.cs`) le check de licence : construire l'objet de config
     (adresse/port serveur `ApLicence.Server`, **subject** dédié à GRF — à confirmer avec le porteur
     du serveur de licence, cf. CDC §6), démarrer la vérification initiale + le recheck périodique.
  3. Ajouter le **point de contrôle unique** exigé par la CDC (§1.3) pour une API web : un middleware
     placé dans le pipeline HTTP de `Program.cs`, qui lit `LicenceStatus.EstValide` (valeur en cache,
     pas d'appel réseau synchrone) et bloque l'accès fonctionnel avec le message fixe
     **"Merci de vérifier la licence"** si invalide.
  4. Décider et documenter le **périmètre exact du blocage** : uniquement les routes `/api/...`, ou
     également le front statique servi depuis `wwwroot` (cf. TASK-116, `UseDefaultFiles`/
     `UseStaticFiles`) — voir point ouvert en Risques.
  5. Ajouter les clés `subject` + adresse/port du serveur `ApLicence.Server` dans `connections.json`
     (paramétrage au niveau déploiement, cf. CDC §4). **Exigence PO (17/07/2026)** : ces valeurs ne se
     saisissent jamais à la main (`notepad connections.json`) — elles sont écrites par le formulaire du
     setup GUI (`Declaration.Setup`, TASK-115), qui porte déjà les champs correspondants. `Program.cs`
     se contente de les **lire**. **Tranché (PO, 17/07/2026)** : adresse/port du serveur de licence ne
     sont pas uniques pour tout le parc (varient par client) — défaut proposé au formulaire
     `127.0.0.1` / `8003`, modifiable ; `subject` reste sans défaut (obligatoire).
  6. Côté front (`declaration-tva-web`) : afficher le message de blocage renvoyé par l'API (si le
     blocage se fait à ce niveau) et la bannière d'alerte J-30 (`AlerteProcheExpiration`/
     `JoursRestants`) — la librairie n'affiche rien elle-même (contrat CDC §1.3/§1.4), c'est à GRF de
     rendre ces données.
- **Exclu** :
  - Construction de la librairie de licence elle-même (nouveau repo séparé, hors GRF).
  - Toute modification de `apbs-gr_winform` ou du serveur `ApLicence.Server`.
  - Choix du subject définitif / création d'un nouveau `.lic` : décision à prendre avec le porteur du
    serveur de licence, pas par ce TASK.

## Objectif
```
Entrée  : Declaration.API sans aucune vérification de licence
Étapes  : référencer la lib de licence (une fois publiée) + middleware de blocage + config subject
          + rendu front (message de blocage + bannière J-30)
Sortie  : Declaration.API démarre toujours ; l'accès fonctionnel (API + écran) est bloqué avec un
          message explicite si la licence est absente/invalide/expirée ; alerte visible dès J-30
```

## Étapes
1. Attendre/suivre la disponibilité de la librairie de licence (repo séparé) et son mode de
   distribution (NuGet interne ou référence projet).
2. Confirmer avec le porteur du serveur `ApLicence.Server` le **subject** à utiliser pour GRF (dédié
   ou partagé) — cf. CDC §6, point non tranché.
3. Ajouter la référence + l'initialisation dans `Program.cs` (démarrage + recheck périodique délégués
   à la librairie).
4. Ajouter le middleware de blocage, en cohérence avec le pipeline existant (ordre par rapport à
   `UseDefaultFiles`/`UseStaticFiles` (TASK-116), `UseAuthentication`/`UseAuthorization`,
   `MapControllers`) — décider explicitement si le front statique est concerné.
5. Ajouter la clé `subject` à `connections.json` (et au gabarit `connections.json.exemple` de
   TASK-044).
6. Implémenter le rendu front (message de blocage + bannière J-30).
7. Rédiger `VERIFY/TASK-117_verify.md` avec preuve réelle (voir Livrables).

## Livrables
- `Program.cs` modifié (middleware de blocage + initialisation du check).
- `connections.json` / `connections.json.exemple` avec la clé `subject`.
- Composant front (bannière J-30 + écran/message de blocage).
- `VERIFY/TASK-117_verify.md` : preuve réelle —
  - démarrage avec licence valide : accès normal, pas de message de blocage ;
  - démarrage/rechecks avec serveur de licence injoignable ou subject invalide : accès bloqué,
    message "Merci de vérifier la licence" affiché, **process toujours démarré** (pas de crash) ;
  - licence à J-30 ou moins : bannière d'alerte visible, accès **non bloqué** ;
  - confirmation qu'aucun appel réseau synchrone n'est fait à chaque requête (lecture d'un état en
    cache uniquement).

## Critères de validation
- Le process démarre toujours, quel que soit l'état de la licence.
- Un seul point de contrôle dans le pipeline HTTP (pas de vérification dispersée par écran/endpoint).
- Message de blocage et bannière J-30 conformes au contrat `LicenceStatus` de la librairie.
- Aucun repli silencieux vers « licence valide » en cas d'erreur/exception/timeout (fail-closed,
  cf. CDC §5).

## Risques / dépendances
- **BLOQUÉ tant que la librairie de licence (nouveau repo) n'existe pas et n'est pas distribuable** —
  ce TASK ne peut pas démarrer avant cette livraison ; ne pas improviser une implémentation locale
  dupliquée dans GRF en attendant (irait à l'encontre du principe même de la CDC, §1.1).
- **Point ouvert non tranché** : le blocage doit-il couvrir le front statique (`wwwroot`, servi par
  TASK-116) ou seulement les routes `/api/...` ? Si seule l'API est bloquée, l'écran React doit quand
  même afficher le message (nécessite un appel API que le front interroge avant d'afficher l'écran
  fonctionnel) — à spécifier précisément avant codage, pas deviné en cours de route.
- **Dépend de TASK-116** (le pipeline HTTP doit déjà être fixé — `UseDefaultFiles`/`UseStaticFiles` —
  avant d'y insérer un middleware de blocage supplémentaire, pour éviter un ordre de middleware
  incohérent).
- **Dépend de TASK-044** pour la cohérence du modèle de configuration (`connections.json` en
  déploiement, clé `subject` à y intégrer).
- **Fait : signalé à TASK-115** (17/07/2026) — le formulaire de setup porte désormais les champs
  `subject` + adresse/port du serveur de licence, en plus du port/connexions déjà prévus. Champs sans
  effet fonctionnel jusqu'à ce que ce TASK-117 soit implémenté (cf. Risques de TASK-115).
- Subject dédié vs partagé (`/LIC/TRESO_GRC` ou nouveau `/LIC/GRF`) : décision externe au projet GRF,
  à obtenir du porteur du serveur `ApLicence.Server` avant de coder l'étape 2.
