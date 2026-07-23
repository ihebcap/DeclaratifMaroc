# TASK-155 — Câblage réel des endpoints de génération/téléchargement des fichiers de dépôt (XML + Excel)

## Contexte
Suite à TASK-137 (export XML mis en conformité avec le CDC DGI, ✅ approuvée), constat architecte
(23/07/2026) : **rien n'appelle réellement `DeclarationXmlExporter`/`Exporter` (Excel, TASK-010) en
dehors de leurs projets de tests.** Les deux endpoints prévus dans `Declaration.API` sont des stubs :

```csharp
// Declaration.API/Controllers/DeclarationsController.cs:467-473
[HttpPost("{id}/generation")]
public IActionResult Generation(Guid id)
    => StatusCode(501, new { Message = "Non implémenté — délégué à TASK-010/011 (Export.Xml + Export.Excel)." });

[HttpGet("{id}/fichiers/{type}")]
public IActionResult DownloadFichier(Guid id, string type)
    => StatusCode(501, new { Message = "Non implémenté — délégué à TASK-010/011." });
```

Côté front, `GenerationPanel.tsx` (atteignable en production via `DeclarationStepper.tsx` →
`App.tsx:296`) appelle bien ces endpoints, mais le bouton « Télécharger » est un stub
`alert(...)` (aucun appel réseau réel, l.24-27). **Aujourd'hui, un utilisateur ne peut pas obtenir
le fichier XML/Excel depuis l'application.**

## Cartographie déjà faite (architecte, lecture seule) — à réutiliser, pas à redécouvrir

1. **Aucune méthode existante ne reconstruit un `DeclarationModele` complet** (avec `Lignes`) depuis
   une déclaration `Cloturée`. `DeclarationWorkflowService.GetCheckupAsync` (l.734) lit déjà l'entête
   (`GetByIdAsync`) et les lignes (`GetLignesAsync(id, "Decaissement"/"Encaissement", 1, int.MaxValue,
   null, null)` → `DM_LGTVA`) mais ne construit que `ControleEquilibre`/`Alertes`, pas
   `List<LigneDeclarationEnrichie>`. **À écrire** : mapping `LigneCandidate`
   (`Declaration.Application/Entities/WorkflowEntities.cs:55`) → `LigneDeclarationEnrichie`
   (`Declaration.Core/Model.cs`), filtré sur les lignes réellement intégrées à la déclaration
   (`Etat == Integree`, à vérifier le nom exact du champ d'état sur `LigneCandidate`).
2. **Champ `Designation`** : confirmé **absent de `DM_LGTVA`** et de `LigneCandidate` — aucune colonne
   en base, le worker OM ne le restitue pas dans ce pipeline. **Hors périmètre de cette task** :
   garder `<des>` vide (comportement déjà prévu et accepté par TASK-011 : "vide + alerte, ne pas
   bloquer"), ne pas tenter de le sourcer ici (gap distinct, déjà documenté ailleurs).
3. **`EnTete.IdentifiantSociete`** : `GetCheckupAsync` (l.743) utilise aujourd'hui
   `declaration.SocieteId.ToString()` — c'est l'**`SO_Id` interne GRF**, pas l'identifiant fiscal réel
   de la société attendu par le tag XML `<identifiantFiscal>`. **Aucune colonne d'IF société n'a été
   repérée dans le code actuel** (`P_SOCIETE` n'est lu que pour `SO_Id`/`SO_RaisonSocial`/
   `SO_ErpDb`/`SO_ErpUserApp`/`SO_ErpPasswdApp`, jamais un identifiant fiscal). **Étape 0 obligatoire
   de cette task** : interroger le schéma réel de `P_SOCIETE` sur une base réelle pour vérifier si une
   colonne d'IF société existe déjà (ex. `SO_If`, `SO_IdentifiantFiscal`, nom exact inconnu) et
   inexploitée, ou si elle n'existe pas du tout. **Ne jamais insérer de valeur placeholder** (règle
   déjà actée, cf. TASK-151) — si la colonne n'existe pas, bloquer la génération avec un message
   explicite ("IF société non configuré, contacter le PO/l'ERP") plutôt que d'exporter le `SO_Id`
   interne à la place (ce qui produirait un XML avec un identifiant fiscal faux, silencieusement).
4. **Résolution de connexion** : réutiliser le pattern existant — `IDbConnectionFactory` injecté dans
   le contrôleur, `CreatePersistenceConnection()` pour `DM_ENTTVA`/`DM_LGTVA`,
   `GetSageConnectionInfoAsync(soId)` uniquement si une donnée Sage manquante devait être relue (a
   priori non nécessaire ici, tout doit déjà être en cache/persisté pour une déclaration `Cloturée`).
5. **Emplacement des fichiers générés** : aucune convention existante pour un dossier de sortie
   d'export. Réutiliser le pattern déjà en place pour les logs (`Path.Combine(AppContext.BaseDirectory,
   "logs")`, `DeclarationWorkflowService.JournaliserValorisation`) — créer un dossier équivalent (ex.
   `exports/`) sous `AppContext.BaseDirectory`, pas un chemin absolu codé en dur (le service tourne en
   Windows Service, cf. commentaires existants).
6. **Auth JWT front** : tous les appels passent par l'instance axios unique (`declaration-tva-web/src/
   api.ts`), interceptor posant `Authorization: Bearer <token>` depuis `sessionStorage`. **Un
   téléchargement binaire doit passer par `api.get(url, { responseType: 'blob' })`** puis
   `URL.createObjectURL` + clic déclenché sur un `<a>` — un simple `<a href=...>` direct ne poserait
   pas le token et échouerait (401).
7. **Aucun état "fichier déjà généré" persisté** en base (`DM_ENTTVA`/`DM_LGTVA` n'ont pas ce champ).
   La seule garde existante est le contrôle **fichier sur disque** déjà dans
   `DeclarationXmlExporter.GenererXml` (`ApplicationException` si le zip/xml existe déjà) — suffisant,
   ne pas ajouter de champ de persistance pour ce seul besoin (hors périmètre, sur-ingénierie).

## Périmètre STRICT
- **Inclus** : construction du `DeclarationModele` complet depuis une déclaration `Cloturée` ;
  câblage réel des deux endpoints (`POST {id}/generation`, `GET {id}/fichiers/{type}`) appelant
  `DeclarationXmlExporter.GenererXml` **et** `Declaration.Export.Excel.Exporter` (même sort — vérifier
  d'abord s'il est déjà appelable tel quel ou s'il a besoin d'un `DeclarationModele` équivalent) ;
  correction du front (`GenerationPanel.tsx`) pour un vrai téléchargement (blob + JWT) et un vrai
  affichage d'erreur (message renvoyé par le backend, pas un message générique).
- **Exclu** : toute modification de `DeclarationXmlExporter.cs`/`Declaration.Export.Excel/Exporter.cs`
  eux-mêmes (déjà corrects, TASK-137/TASK-010) ; sourcing de `Designation` (gap distinct documenté,
  hors périmètre) ; ajout d'un état "généré" persisté en base (non nécessaire, cf. point 7) ; le
  "Rapport d'anomalies PDF" visible dans `GenerationPanel.tsx` s'il n'a jamais eu d'implémentation
  réelle (à vérifier — si c'est un fantôme UI sans task d'origine, le signaler au PO plutôt que
  l'implémenter silencieusement dans cette task).

## Étapes
1. **Étape 0 (préalable, bloquant pour la suite)** : identifier la source réelle de l'identifiant
   fiscal de la société (schéma réel `P_SOCIETE` sur une base de test/réelle disponible). Si aucune
   colonne exploitable n'existe, documenter ce blocage et proposer une option au PO (ex. nouveau champ
   de configuration côté GRF, séparé de `P_SOCIETE` — à ne pas décider seul si ça touche une table
   possédée par l'application principale).
2. Écrire la méthode de reconstruction du `DeclarationModele` complet (entête + lignes intégrées) à
   partir d'une déclaration `Cloturée` — mapping `LigneCandidate` → `LigneDeclarationEnrichie`.
3. Câbler `POST {id}/generation` : vérifier que la déclaration est `Cloturée` (sinon 400 explicite,
   conforme à la séquence de génération du CDC §3.1) ; construire le `DeclarationModele` ; appeler
   `GenererXml` (et l'export Excel équivalent) vers le dossier de sortie choisi (point 5 ci-dessus) ;
   traduire les `ApplicationException` de l'exporter (fichier déjà existant, IF/ICE invalide, aucune
   ligne à exporter) en réponses HTTP explicites (400/409 avec le message), pas un 500 générique.
4. Câbler `GET {id}/fichiers/{type}` : retourner le fichier généré (zip XML, xlsx Excel) en flux
   binaire avec le bon `Content-Type` et nom de fichier ; vérifier le contrôle d'accès (même règle
   `SO_Id`/société que les autres endpoints de `DeclarationsController`).
5. Corriger `GenerationPanel.tsx` : `downloadFile` doit faire un vrai appel `api.get(url, {
   responseType: 'blob' })` puis déclencher le téléchargement (Blob + `URL.createObjectURL` + `<a>`
   temporaire) ; `handleGenerate` doit afficher le message d'erreur réel renvoyé par le backend (pas
   un texte générique "Erreur lors de la génération").
6. Tests : test(s) de la méthode de reconstruction du modèle (fixture `LigneCandidate` → assertions
   sur le `DeclarationModele` produit) ; test(s) d'intégration des deux endpoints (mock repository,
   pas de vraie base) couvrant : succès, déclaration non clôturée, IF société absent/bloquant, fichier
   déjà généré.
7. Vérifier manuellement (ou par test e2e si l'environnement le permet) le cycle complet : clôturer →
   générer → télécharger, sur au moins un cas réel si un environnement avec base réelle est
   disponible ; sinon documenter clairement cette limite dans le VERIFY (comme fait pour d'autres
   tasks récentes de ce module, ex. TASK-148/153).

## Livrables
- Méthode de reconstruction du `DeclarationModele` (emplacement à définir par le worker — probablement
  `DeclarationWorkflowService` ou une nouvelle classe dédiée dans `Declaration.Application`).
- `DeclarationsController.cs` : les deux endpoints réellement câblés (plus de 501).
- `GenerationPanel.tsx` corrigé (téléchargement réel + erreurs réelles).
- Tests unitaires/intégration associés.
- `VERIFY/TASK-155_verify.md` : preuve du cycle clôture→génération→téléchargement (réel si possible,
  sinon couverture de test + limite documentée), et **verdict explicite sur l'étape 0** (IF société
  trouvé où, ou blocage signalé au PO).

## Critères de validation
- `POST /declarations/{id}/generation` sur une déclaration `Cloturée` avec des lignes valides produit
  réellement un fichier XML conforme (réutilise TASK-137, non re-testé ici) et un fichier Excel.
- `GET /declarations/{id}/fichiers/{type}` renvoie le fichier binaire réel, téléchargeable depuis le
  front avec le token JWT.
- Génération sur déclaration non clôturée → erreur explicite (400), pas 500.
- IF société absent → blocage explicite et compréhensible, aucune valeur placeholder exportée.
- Aucune régression sur les endpoints existants de `DeclarationsController`.
- Build 0 erreur, tests verts (back + front build tsc/vite).

## Risques / dépendances
- **Dépend de TASK-137** (✅ approuvée) pour la correction du format XML lui-même — cette task ne
  touche pas `DeclarationXmlExporter.cs`.
- **Bloquant potentiel réel** : si `P_SOCIETE` n'a effectivement aucune colonne d'IF société
  exploitable, cette task ne peut pas produire un XML valide de bout en bout tant que le PO n'a pas
  tranché où sourcer cette donnée — à signaler immédiatement si constaté, ne pas contourner par un
  placeholder.
- Le sort de `Declaration.Export.Excel.Exporter` (câblé ou non aujourd'hui) est à vérifier en tout
  début de task — si son état diffère de ce qui est documenté ici (cartographie du 23/07/2026), ne
  pas supposer, relire le code réel.
