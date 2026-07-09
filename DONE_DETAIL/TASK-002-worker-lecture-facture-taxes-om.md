# TASK-002 — Worker : lecture facture achat/vente + taxes via Objets Métier Sage

## Contexte
Premier module du nouveau système de déclaration TVA (cf. `MODULE_DECLARATION_TVA.md`, `CAHIER_DES_CHARGES.md`). Objectif de CE worker : la **brique de base** — se connecter à Sage, lire une **facture d'achat** et une **facture de vente** par leur numéro, et en extraire le **détail des taxes** (base HT / taux / montant TVA / TTC, **par taux**).

Le détail TVA n'est **pas** stocké en base GRF : il doit être lu **exactement** depuis Sage via les Objets Métier, sans aucun recalcul ni arrondi (exigence anti-erreur du cahier des charges).

### Périmètre STRICT de cette task
- **Uniquement** : connexion Sage + lecture facture achat + lecture facture vente + extraction taxes + restitution.
- **Exclu** : sélection des règlements/affectations (SQL GRF), calcul de proration, cache, Excel, XML, UI. Viendront après.

### Positionnement / cycle de vie (déterminant pour l'architecture)
- **Objectif immédiat = TVA.** Ce worker est la **1re brique** consommée ensuite par l'application de déclaration TVA (fournit le détail taxes exact par facture).
- **Phase transitoire.** À terme, deux issues possibles (phase ultérieure, hors périmètre ici) :
  1. **Intégration** dans le connecteur Sage `D:\APBS FlowMaster\APBS-FlowMaster\.sage100-connector` (le worker devient un service/lecteur du SDK OM), OU
  2. **Retour d'expérience** : le worker reste séparé et alimente le connecteur d'un REX (comme `SAGE_OBJETS_METIERS_GUIDE.md` / `REX_GOCOM_*`).
- **Conséquence d'architecture (à appliquer dès maintenant, sans sur-ingénierie)** : isoler un **cœur réutilisable** — une petite lib « lecteur OM lecture seule » (connexion STA + lecture document + extraction taxes) — **séparé** de l'hôte (console/worker qui n'est qu'un point d'entrée). Le cœur ne doit dépendre que de `Objets100cLib` (aucune dépendance GRF/TVA), pour être **portable tel quel** vers le connecteur. L'hôte console est jetable.

## Règle de dépendance — RÉFÉRENCE UNIQUE
- **Source de vérité = documentation officielle Sage Objets Métier** :
  - `D:\_vibe\objetmetiers\sage 100c objets métiers.pdf` (référence principale)
  - `D:\_vibe\objetmetiers\ModificationsOM100c_version12_00.pdf` (+ v10/v11 si besoin d'interfaces versionnées)
  - `D:\_vibe\objetmetiers\Optimiser la création et la modification des documents avec les Objets Métiers.pdf`
  - `D:\_vibe\objetmetiers\V1210_Sage 100_Structure des fichiers.pdf` (mapping tables/champs)
- **NE RIEN réutiliser de GRFN** (aucune DLL `Erp.Sage.*`, `Erp.Extern.*`, `SageCompta.*`, aucun code décompilé).
- Le guide `.sage100-connector/DOCS/SAGE_OBJETS_METIERS_GUIDE.md` et le projet GOCOM sont **uniquement un exemple de patron d'appel COM** (thread STA, `Open()`, `Marshal.ReleaseComObject`, collections 1-based). Ils **n'imposent aucune API métier** : toute méthode/objet utilisé doit être **justifié par la doc officielle** ci-dessus.

## Objectif
Un exécutable .NET autonome qui, pour un **numéro de document** (achat ou vente) donné en entrée, lit le document dans Sage et restitue pour chacun :
```
DO_Piece | DO_Type (type réel trouvé) | sens (Achat|Vente) | pour chaque taux : { taux%, base HT, montant TVA, TTC } | totaux HT / TVA / TTC
```

### Généricité — le worker renvoie ce qu'il trouve, l'appli classe
- Le worker est un **lecteur générique** : il **ne filtre pas** sur « facture ». Il lit le document et renvoie son **type réel** (`DO_Type`) + ses taxes. C'est l'**application appelante** (TVA) qui décide s'il s'agit d'une facture ou d'un autre document. (Cohérent avec le principe déjà acté : le worker récupère tout, l'appli décide.)
- **⚠️ « Facture » = 2 types physiques dans Sage.** Une facture **comptabilisée change de `DO_Type`** : vente **6 → 7**, achat **16 → 17** (`DocumentTypeVenteFactureComptabilisee` / `DocumentTypeAchatFactureComptabilisee` — **valeurs/noms exacts à confirmer doc officielle**). Résoudre une facture par numéro **doit sonder les DEUX types** (facture **et** facture comptabilisée) et ne conclure « introuvable » que si absente des deux. Ne chercher que le type « facture » = rater toutes les comptabilisées → **saut silencieux à proscrire** (exactement le type de bug que le module doit éliminer).

## Contraintes techniques (imposées par le COM Sage)
- Cible `net*-windows`, plateforme **x86**, build via **MSBuild** (pas `dotnet build` — le COMReference n'est pas résolu autrement).
- Référence `Objets100cLib` avec `EmbedInteropTypes=True` → **accès typé uniquement** (jamais `dynamic`, pas d'IDispatch).
- Tout appel COM sur un **thread STA** ; session ouverte **et** fermée sur le même thread.
- **Timeout** sur l'appel STA (un document ouvert dans l'UI Sage fige l'appel).
- **Lecture seule** : aucun `Write()`, aucune écriture (ni Sage ni GRF).
- `Marshal.ReleaseComObject` sur **chaque** objet COM (document, lignes, taxes) en `finally`.
- Collections COM **indexées à 1**.

## Bases & accès (lecture seule)
- Serveur : `.\sql2022`
- Base commerciale Sage : `DISTRI_DEMO`
- Utilisateur applicatif Sage : `<Administrateur>` (mot de passe souvent vide).
- **Paramètres de connexion et numéros de facture = configuration** (fichier `appsettings`/args), **jamais en dur** (principe multi-client du cahier des charges). Pour le POC, un `appsettings.json` suffit.

## Étapes
1. **Squelette worker** : projet console .NET `net*-windows` x86, référence COM `Objets100cLib` (`EmbedInteropTypes=True`), helper `RunOnStaThread<T>(func, timeout)`, ouverture/fermeture de session (`BSCIALApplication100c` → `CompanyServer`/`CompanyDatabaseName`, `Loggable.UserName/UserPwd`, `Open()`/`Close()`). Sonder la disponibilité de la base (mono-utilisateur → `COMException` traduite).
2. **Lecture document VENTE** : via `session.FactoryDocumentVente`, patron `ExistPiece(...)` puis `ReadPiece(...)`. Pour résoudre une **facture** par numéro, **sonder les deux types** : `DocumentTypeVenteFacture` (`DO_Type = 6`) **puis** `DocumentTypeVenteFactureComptabilisee` (`DO_Type = 7`). Renvoyer le `DO_Type` réellement trouvé ; « introuvable » seulement si absent des deux.
3. **Lecture document ACHAT** : identifier dans la **doc officielle** la factory et les types de document d'achat (factory documents d'achat + type « facture fournisseur » **et** « facture fournisseur comptabilisée ») et les interfaces versionnées correspondantes. Même logique de sondage des **deux types** (`DocumentTypeAchatFacture` = 16 **puis** `DocumentTypeAchatFactureComptabilisee` = 17). ⚠️ **Non couvert par le guide GOCOM** → noms/valeurs exacts des objets/énums à **confirmer dans le PDF officiel** avant usage (ne rien inventer).
4. **Extraction des taxes d'un document** — cœur de la task : identifier dans la **doc officielle** l'API d'accès aux taxes d'un document (collection de taxes du document et/ou par ligne) et lire, **par taux** : le taux, la base (HT), le montant de TVA, le TTC. Restituer les valeurs **telles que Sage les calcule** (aucun recalcul).
   - Points de vigilance à traiter explicitement (issus de retours terrain sur la lecture des taxes OM, à valider contre la doc) : **taxe à montant 0**, **taxe de type Para/TTC** (≠ TVA sur HT), **remises en cascade** sur les lignes, documents **multi-taux**.
5. **Restitution** : affichage console lisible + dump JSON `{ piece, doType, sens, lignesTaxe:[{taux, type, baseHT, montantTva, ttc}], docTotalHT, docTotalTtc, docEscompte, totalTaxeBase, totalTva, totalTaxeTtc }` — inclure : `doType` (type réel), le **`type` de chaque taxe** (`TVA*` vs parafiscale `TPHT`/`TPTTC`) pour que l'appli filtre, et les **totaux document** (`DO_TotalHT`, `DO_TotalTTC`, escompte) **à côté** des totaux reconstitués depuis les taxes. Sert de base aux étapes suivantes (proration, contrôle, exports).
6. **Contrôle de cohérence** — ⚠️ `Σbase + Σtva = TTC` **n'est PAS un invariant valide** sur les vrais documents. Le TTC document diffère de `HT + taxes` à cause de : **escompte** (baisse le net), **lignes exonérées / non taxées** (présentes dans le TTC, absentes des taxes), **taxes parafiscales** `TPHT`/`TPTTC` (bases non homogènes avec les bases TVA → non sommables). Donc le contrôle doit **décomposer l'écart, pas seulement le signaler** :
   - séparer les taxes **TVA** (`TaxeTypeTVA*`) des **parafiscales** (`TPHT`/`TPTTC`) — ne jamais additionner leurs bases ;
   - rapprocher `DO_TotalTTC` de `DO_TotalHT + Σ(toutes taxes) − escompte` et **expliquer le résidu** (part exonérée / non taxée) ;
   - tout écart **inexpliqué** = anomalie bloquante (révèle une taxe ou une part de document non captée). Un écart **expliqué** (escompte chiffré, montant exonéré identifié) est acceptable et documenté.

## Livrables
- **Cœur réutilisable** : petite lib « lecteur OM lecture seule » (connexion STA + lecture document + extraction taxes), dépendant **uniquement** de `Objets100cLib` → portable vers `.sage100-connector`.
- **Hôte** : worker/console .NET compilable (MSBuild x86) + `appsettings.json` d'exemple (connexion + 2 numéros de facture). Jetable, ne contient aucune logique métier.
- `VERIFY/TASK-002_verify.md` : sortie réelle pour 1 facture achat et 1 facture vente de `DISTRI_DEMO`, avec le détail par taux, le contrôle de cohérence, et **la référence de page** de la doc officielle justifiant l'API de taxes et l'API document d'achat retenues.
- **REX / note d'acquis** (format aligné sur `.sage100-connector/DOCS`) : les objets/énums OM effectivement utilisés (nom exact + interface versionnée + page doc officielle), les pièges rencontrés sur la lecture des taxes (montant 0, Para/TTC, remises cascade, multi-taux) et sur les documents d'achat. C'est ce livrable qui alimente le connecteur en phase ultérieure.

## Critères de validation
- Build MSBuild x86 OK ; exécution lecture seule (aucune écriture).
- Facture vente ET facture achat lues ; taxes extraites par taux.
- Résolution d'une facture par numéro **sonde facture ET facture comptabilisée** (vente 6/7, achat 16/17) ; `DO_Type` réel renvoyé dans la sortie.
- Contrôle de cohérence : tout écart entre `DO_TotalTTC` et `DO_TotalHT + taxes − escompte` est **décomposé et expliqué** (escompte / part exonérée / parafiscal) ; aucun écart inexpliqué.
- Chaque API métier utilisée est **justifiée par la doc officielle Sage** (page citée), pas par GRFN ni par un nom deviné.
- Aucune dépendance à une DLL/logique GRFN ou GOCOM.

## Risques / dépendances
- **Bloquant exécution** : disposer d'**un n° de facture achat** et **un n° de facture vente réels** existant dans `DISTRI_DEMO` (base démo → vérifier qu'au moins une facture de chaque sens avec TVA existe ; sinon demander une base fournie).
- **API taxes non documentée dans le guide GOCOM** : la découvrir dans le PDF officiel est l'inconnue principale de la task (Étape 4). Si le PDF ne suffit pas, remonter le blocage (ne pas se rabattre sur du code GRFN).
- **Documents d'achat** absents du guide GOCOM : interfaces/énums à confirmer doc officielle (Étape 3).
- Mono-utilisateur : `Open()` échoue si la base est ouverte ailleurs → sonde + message métier.
- Ne bloque pas les autres analyses, mais **conditionne** tout le calcul TVA aval (le détail taxes exact est la matière première du module).
```