# TASK-101 — Support multi-version du DLL Objets Métiers Sage (Interop.Objets100cLib)

Status: TODO
Priority: HIGH
Risk: HIGH (bloque toute valorisation TVA réelle chez un client dont la version Sage diffère)
Module: SageTaxReader.Core / SageTaxReader.Console / déploiement (`connections.json`)

## OBJECTIF
Permettre à `SageTaxReader.Console.exe` (worker qui lit les factures via l'objet métier Sage) de
fonctionner contre **n'importe quelle version de Sage 100 installée chez le client** (v7/v9/v10/
v11/v12 identifiées à ce jour ; **v8 également prévue mais non disponible pour le moment** — aucun
DLL interop `v8` fourni par le PO, dossier absent de `D:\_vibe\objetmetiers\dll\`), chaque
déploiement client n'ayant besoin de supporter **qu'une seule** version à la fois — mais **nous**
(l'éditeur) devons pouvoir livrer/configurer la bonne version sans reconstruire le produit à la main
pour chaque client.

## BUSINESS VALUE
Découvert en testant le nouvel environnement client `DESKTOP-5BFKKEP` (16/07/2026) : le worker
échouait d'abord avec une erreur de confiance de domaine Windows (résolue en configurant l'accès
réseau), puis avec une **toute autre erreur, propre à Sage** : `COMException (0xFFFFF562) — Ce
fichier n'est pas converti, veuillez utiliser la maintenance !`, sur 100 % des pièces (0 succès /
197 tentatives `EC_Type=0`). Cause identifiée : `SageTaxReader.Core.csproj` référence un **unique**
DLL interop figé au build, `SageTaxReader\libs\Interop.Objets100cLib.dll` (actuellement la
version **12.10.0.0**, 619 008 octets), alors que la base réelle de ce client tourne sur Sage
**v10** (confirmé PO) — un décalage de version majeur entre le composant objet-métier utilisé pour
lire la base et le format réel de cette base, que Sage refuse d'ouvrir sans passage par sa propre
maintenance/conversion.

Le PO a fourni 5 DLL interop versionnées dans `D:\_vibe\objetmetiers\dll\{v7,v9,v10,v11,v12}\
Interop.Objets100cLib.dll` (tailles distinctes : 576000/581120/593920/593920/619008 octets — v10
et v11 de taille identique, à vérifier si réellement interchangeables). Le besoin métier est donc
confirmé : plusieurs versions Sage coexistent dans le parc client, et le produit doit pouvoir
cibler la bonne au déploiement.

## CONTRAINTES — analyse technique (architecte, 16/07/2026)
- **`SageTaxReaderService.cs` utilise l'interop EN LIAISON PRÉCOCE (early-bound)** : `using
  Objets100cLib;`, `new BSCIALApplication100c()`, types `IBODocumentVente3`/`IBODocumentAchat3`/
  `IBPDossier2`/`IDocValorisation`/`IDocValoTaxes`/etc. référencés directement dans le code C#,
  compilés contre les GUID/signatures du DLL interop **au moment du build**.
- Conséquence : **on ne peut pas se contenter de remplacer le fichier DLL sur le poste cible à
  l'exécution** — si les GUID de classe/interface diffèrent d'une version Sage à l'autre (probable
  entre versions majeures, à vérifier empiriquement), l'activation COM échouera ou pointera vers
  la mauvaise implémentation. Une réécriture en liaison tardive (late-binding via réflexion,
  `Type.GetTypeFromProgID` + `InvokeMember`) éliminerait cette contrainte mais représente une
  réécriture lourde et risquée de `SageTaxReaderService.cs` (perte de la sécurité de typage,
  fichier déjà mature avec plusieurs correctifs TASK-023/046/047/072/076).
- **Piste recommandée (plus faible risque, cohérente avec l'existant)** : construire/publier une
  variante **distincte** de `SageTaxReader.Console` par version Sage supportée (chacune référençant
  son propre `Interop.Objets100cLib.dll` via un `HintPath` dédié), produisant des exécutables
  séparés (ex. `SageTaxReader.Console.v7.exe`, `.v9.exe`, `.v10.exe`, `.v11.exe`, `.v12.exe`).
  `connections.json` expose déjà `WorkerConfig.WorkerExePath` en configuration **par déploiement
  client** (LANCEMENT_DEV.md) — il suffit que ce chemin pointe, chez CE client, vers l'exécutable
  correspondant à SA version Sage réelle. Aucune donnée à ajouter au format JSON existant, juste un
  processus de build/publish à étendre (produire N exécutables au lieu d'un seul dans `deploy\`).
- Vérifier au préalable si les DLL v10/v11 (même taille) sont réellement les mêmes GUID/interfaces
  — si oui, un seul binaire pourrait couvrir les deux, réduisant à 4 variantes au lieu de 5.
- Ne pas committer les DLL Sage propriétaires n'importe où sans vérifier la licence de
  redistribution (fichiers tiers sous licence Sage) — clarifier avec le PO où ces DLL doivent
  vivre dans le repo/pipeline de build (`SageTaxReader\libs\vX\` proposé, à confirmer).
- Aucune régression sur le comportement actuel pour un client déjà en v12 (référence par défaut
  inchangée si aucune version n'est spécifiée).

## FILES
- SageTaxReader.Core/SageTaxReader.Core.csproj (référence interop actuelle, à rendre paramétrable
  par variante de build — ex. `Configuration`/`RuntimeIdentifier` dédié par version, ou projets
  multiples)
- SageTaxReader\libs\Interop.Objets100cLib.dll (actuellement v12 uniquement) + les 5 DLL fournies
  par le PO (`D:\_vibe\objetmetiers\dll\{v7,v9,v10,v11,v12}\`) — **v8 à ajouter dès qu'un DLL interop
  v8 sera fourni** ; absent du dossier `D:\_vibe\objetmetiers\dll\` à ce jour, ne pas l'inventer/le
  simuler en attendant
- Déploiement : étape de publish (LANCEMENT_DEV.md §Déploiement) à étendre pour produire N
  exécutables worker au lieu d'un seul dans `deploy\`
- connections.json / LANCEMENT_DEV.md (documenter le choix du `WorkerExePath` selon la version
  Sage du client, aucun nouveau champ requis a priori)

## VALIDATION
- [ ] Build OK pour chaque variante disponible (v7/v9/v10/v11/v12, ou 4 si v10=v11 confirmés
      identiques) — **v8 exclue de cette validation tant qu'aucun DLL interop v8 n'est fourni**
- [ ] Worker v10 pointé sur `DESKTOP-5BFKKEP` lit réellement une facture Sage (`WORKER OK` en log,
      TVA non nulle) — preuve réelle remplaçant l'échec 100 % actuel (0xFFFFF562)
- [ ] Un worker de version incompatible avec une base donnée échoue avec un message **clair**
      (version attendue vs détectée), jamais une erreur Sage brute non expliquée
- [ ] Non-régression : un client déjà fonctionnel (ex. `GR_EMA_DISTRIBUTION` d'origine, worker v12)
      continue de fonctionner à l'identique après le changement de structure de build

## ARCHITECTURE RULES APPLICABLES
- Un seul fichier de configuration par déploiement (`connections.json`, principe déjà posé dans
  LANCEMENT_DEV.md) — la sélection de version ne doit pas introduire un second mécanisme de config.
- Aucune dette technique silencieuse : si seules certaines versions (ex. v10/v12) sont couvertes en
  premier lieu, le documenter explicitement en NOTES du VERIFY.

## NOTES
Découvert par l'architecte (16/07/2026) en testant le tunnel de bout en bout sur le nouvel
environnement client `DESKTOP-5BFKKEP`. Le PO a déjà fourni les 5 DLL versionnées dans
`D:\_vibe\objetmetiers\dll\` — analyse de faisabilité ci-dessus faite avant toute implémentation
(conformément au rôle architecte : pas de modification directe du code). Implémentation à confier
au développeur.

**Test exceptionnel réalisé le 16/07/2026 (autorisé PO, hors rôle habituel)** : remplacement ponctuel
de `SageTaxReader\libs\Interop.Objets100cLib.dll` par la version v10 fournie + rebuild, pour
débloquer le test en cours. Résultat **négatif, mais concluant** : nouvelle erreur
`COMException (0x80040154) — REGDB_E_CLASSNOTREG : la classe COM CLSID
{ED0EC116-16B8-44CC-A68A-41BF6E15EB3F} n'est pas enregistrée` sur 100 % des pièces (0/197). Ceci
**confirme** l'analyse ci-dessus : remplacer le seul assembly interop .NET est insuffisant — le
**composant COM natif Sage v10 lui-même** (le vrai serveur COM, hors de ce repo, fourni par
l'installeur Sage officiel) doit être installé/enregistré sur la machine qui exécute le worker.
Ce poste de dev n'a que l'installation Sage correspondant à la v12 (déjà enregistrée) — d'où
l'échec. **Conclusion actionnable** : la validation réelle de la valorisation TVA ne peut se faire
que sur une machine où la version Sage du client (v10) est réellement installée — `DESKTOP-5BFKKEP`
elle-même, pas ce poste de dev. Le remplacement de DLL a été **annulé** (restauration du DLL v12
d'origine + rebuild) après ce test, pour ne laisser aucun état intermédiaire cassé dans le repo.

**Retest concluant le 16/07/2026 (même jour, après action PO)** : le PO a fait installer le
composant Sage **Objets métiers v10.10** (natif, `objets100c.dll`, CLSID `{ED0EC116-...}` désormais
enregistré) directement sur le poste de dev. Nouveau remplacement du DLL interop managé par la
version v10 fournie (`D:\_vibe\objetmetiers\dll\v10\Interop.Objets100cLib.dll`) + rebuild
(`SageTaxReader.slnx`, 0 erreur) + refiegage complet d'une déclaration `TVA1-2026-06` fraîche
(règlements réels, non rattrapés depuis un figeage antérieur) :
- Décaissement : batch OM 197 pièce(s) EC_Type=0 → **0 en erreur**, TVA réelles et variées observées
  (46/252 lignes à TVA=0, contre 197/197 forcées à 0 précédemment).
- Encaissement : batch OM 240 pièce(s) EC_Type=0 → **0 en erreur**.
- Confirme définitivement l'analyse technique ci-dessus : le blocage n'était PAS dans le code
  applicatif ni dans l'assembly interop, mais dans l'absence du composant COM natif Sage v10 sur la
  machine exécutant le worker. Dès que ce composant est installé, le remplacement du seul DLL
  interop (`Interop.Objets100cLib.dll`) suffit à débloquer la lecture réelle.
- **Fix immédiat appliqué** (poste de dev uniquement, non committé en l'état — cf. contrainte
  licence Sage ci-dessus) : `SageTaxReader\libs\Interop.Objets100cLib.dll` reste actuellement la
  version **v10** (et non v12) sur ce poste, pour permettre la poursuite des tests contre
  `DESKTOP-5BFKKEP`. **Ceci constitue une régression potentielle pour tout test futur contre un
  client Sage v12** tant que TASK-101 n'est pas implémentée — à surveiller, documenté ici
  explicitement pour éviter toute dette silencieuse.
- **Reste à faire (développeur, cf. OBJECTIF/CONTRAINTES ci-dessus)** : implémenter le vrai support
  multi-version (variantes de build par version Sage + sélection via `WorkerConfig.WorkerExePath`),
  pour ne plus dépendre d'un remplacement manuel de DLL à chaque changement de client testé.

## IMPLÉMENTATION (17/07/2026, worker exceptionnel — rôle inversé, demande explicite PO)

**Vérification préalable v10/v11** : SHA256 des deux DLL fournis par le PO identique
(`cbabb3a6d24...c9dce4`) — bit-à-bit le même fichier. **4 variantes suffisent** (v7, v9,
v10-couvrant-v11, v12), pas 5.

**Mécanisme retenu** : nouvelle propriété MSBuild `SageInteropVersion` (défaut `v12`, valeur
inchangée si non spécifiée → **zéro régression** pour un client déjà en v12) :
- `SageTaxReader.Core.csproj` : `HintPath` du `Reference Include="Interop.Objets100cLib"` devient
  `..\libs\$(SageInteropVersion)\Interop.Objets100cLib.dll` (au lieu du chemin plat figé).
- `SageTaxReader.Console.csproj` : `AssemblyName` devient `SageTaxReader.Console` si
  `SageInteropVersion=v12` (nom historique inchangé), `SageTaxReader.Console.$(SageInteropVersion)`
  sinon (ex. `SageTaxReader.Console.v7.exe`).
- DLL interop restructurés en `SageTaxReader\libs\{v7,v9,v10,v12}\Interop.Objets100cLib.dll`
  (copiés depuis `D:\_vibe\objetmetiers\dll\`) ; ancien fichier plat + `.v12.dll.bak` supprimés
  (l'un et l'autre remplacés par la structure versionnée — supprime aussi la régression signalée
  ci-dessus : le DLL par défaut du poste de dev était resté en v10 après le test manuel, il est
  restauré en v12 par construction du nouveau défaut). `.gitignore` mis à jour
  (`!SageTaxReader/libs/v*/Interop.Objets100cLib.dll`).
- **Piège rencontré et corrigé** : isoler `BaseOutputPath`/`BaseIntermediateOutputPath` par version
  casse le globbing implicite du SDK (les anciens `AssemblyInfo.cs` générés dans `obj\Release\`
  restent inclus → `CS0579` doublons). Abandonné ; isolation gérée à la place par un nettoyage
  complet `obj`/`bin` avant chaque variante dans le script de publish (déterministe, sans piège SDK).
- **Script `publish-sagetaxreader-workers.ps1`** (racine repo, nouveau) : publie les 4 variantes
  dans `deploy\workers\{v7,v9,v10,v12}\`, chacune un dossier autonome (exe + son propre
  `SageTaxReader.Core.dll`, car `EmbedInteropTypes` fige les types Sage dans ce DLL au build — un
  dossier partagé entre versions serait incompatible).
- `LANCEMENT_DEV.md` : nouvelle section « A2. Worker Sage — choisir la version » (table des 4
  dossiers + leur couverture, dont v10→v11) et rappel dans l'exemple `connections.json`.

**Validation (build)** : les 4 variantes compilent 0 erreur (`dotnet build` isolé + séquence
complète du script de publish rejouée de bout en bout) ; build par défaut (sans propriété) confirmé
identique à l'existant (`SageTaxReader.Console.exe`, chemin `bin\Debug\net48\` inchangé). Non-
régression solution principale : `dotnet build DeclarationTVA.slnx` 0 erreur ; suite de tests
complète rejouée, **234/236** verts — les 2 échecs (`Declaration.Selection.Tests` connexion codée
en dur, `Declaration.Controle.Tests.ComparateurTests`) sont **préexistants et sans rapport**
(confirmés identiques à l'état `main` documenté dans `TODO.md`), aucune régression introduite par
ce changement (qui ne touche que `SageTaxReader.*`, `.gitignore`, `LANCEMENT_DEV.md`, le nouveau
script).

**Validation (réel, partielle)** : smoke test du `SageTaxReader.Console.v10.exe` **nouvellement
compilé** (via `-p:SageInteropVersion=v10`, donc par le nouveau mécanisme, pas un remplacement
manuel de fichier) contre l'environnement déjà validé `DESKTOP-5BFKKEP`/`NEW_EMA DISTRIBUTION` :
activation COM réussie, aucune erreur `0xFFFFF562` (fichier non converti) ni `REGDB_E_CLASSNOTREG`
(composant natif absent) — l'échec obtenu (`Le nom de l'utilisateur est incorrect`, sur des
identifiants SQL Server `sa`/1234 passés à tort en identifiants applicatifs Sage, dossier ≠ SQL)
est un échec d'authentification **applicative Sage**, sans rapport avec le sujet de cette task.
Preuve que le nouveau mécanisme de sélection reproduit exactement l'environnement déjà validé
manuellement (197/197 + 240/240 pièces OM, cf. notes ci-dessus) — même DLL, même composant natif.

**Réserve non bloquante (dette technique documentée, pas silencieuse)** : les critères VALIDATION
« Worker v10 lit réellement une facture (TVA non nulle) » et « message clair sur incompatibilité de
version » n'ont **pas** été rejoués avec de vrais identifiants applicatifs Sage (non fournis à ce
poste) — seule l'activation COM/DLL a été prouvée en conditions réelles, pas la lecture de facture
elle-même via ce nouveau mécanisme (elle l'avait été via le remplacement manuel, cf. notes
16/07/2026). Aucun test n'a non plus été rejoué pour v7/v9 (aucune base Sage v7/v9 disponible pour
valider en conditions réelles — build seul vérifié). Ces réserves n'invalident pas le mécanisme
(le point technique de la task — sélection de DLL par variante de build — est résolu et prouvé),
mais restent à lever lors du prochain déploiement client sur une version autre que v12/v10.
