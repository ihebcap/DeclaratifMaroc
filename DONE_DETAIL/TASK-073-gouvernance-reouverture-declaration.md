# TASK-073 — Gouvernance et traçabilité de la réouverture d'une déclaration clôturée

> **Origine :** remarque PO 13/07/2026 — « Déclaration intégrée : cette étape est figée (lecture
> seule). On doit avoir un retour en arrière toujours. » Vérification architecte (13/07/2026) : le
> retour en arrière **existe et fonctionne techniquement** (verrou TASK-028/064 + réouverture), mais
> il n'est **ni gouverné ni tracé** — un trou de contrôle interne sur une donnée fiscale déjà déclarée.

## Constat (preuve code, aucune supposition)
1. **Le verrou fonctionne** — `Declaration.Infrastructure/SQL/003_Verrou_DT_Id.sql:51-120` (trigger
   `TR_RT_AFFECTATION_Immuabilite`) bloque tout UPDATE/DELETE sur une affectation tamponnée
   (`DT_Id NOT NULL`), sauf la transition `DT_Id : valeur → NULL` (dé-tamponnage pur, L86-93).
   Rejoué réellement sur 555 affectations (TASK-071, DONE.md).
2. **La réouverture fonctionne mais sans garde ni trace** —
   `Declaration.Application/Services/DeclarationWorkflowService.cs:455-475`
   (`ReouvriDeclarationAsync`) : seule vérification = `declaration.Statut != Cloturee` →
   `InvalidOperationException`. Aucune vérification de rôle, aucun motif requis, aucun log d'audit
   (qui a rouvert, quand, pourquoi).
3. **L'endpoint n'a aucune garde de rôle spécifique** —
   `Declaration.API/Controllers/DeclarationsController.cs:223-239`
   (`POST /api/declarations/{id}/reouverture`) : uniquement `[Authorize]` générique hérité de la
   classe (ligne 16) — tout utilisateur authentifié peut rouvrir n'importe quelle déclaration
   clôturée.
4. **Non exposée en UI** — aucune occurrence de "reouverture"/"Rouvrir" dans
   `declaration-tva-web/src` ; TASK-057 avait explicitement laissé ce point hors périmètre
   (« exposer seulement si le workflow PO le prévoit »). Aujourd'hui accessible uniquement via
   API/DB directe.
5. **Aucun test d'intégration réel du cycle complet** — `Task071DeblocageIntegrationTests.cs` mocke
   les triggers ; le scénario intégrer→refus→rouvrir→OK n'a été vérifié qu'une fois manuellement
   (TASK-071), jamais automatisé.

## Objectif
Rendre la réouverture d'une déclaration clôturée **gouvernée** (qui a le droit) et **traçable**
(qui, quand, pourquoi), sans toucher au mécanisme technique de verrou/dé-tamponnage déjà validé.

## Décision produit préalable — TRANCHÉE (PO 13/07/2026)
**Qui a le droit de rouvrir une déclaration clôturée ?** → **réservé aux utilisateurs `UT_Admin = 1`.**
Le PO a confirmé que « supprimer une déclaration » / « supprimer l'intégration des règlements »
désigne bien la réouverture existante (`ReouvriDeclarationAsync` : remise en `EnCours` +
dé-tamponnage des affectations, **aucune donnée n'est physiquement effacée**) — pas une suppression
physique en base, qui n'existe pas dans le code et n'est pas demandée ici. Le périmètre A ci-dessous
se limite donc à : garde `UT_Admin = 1` sur `POST /api/declarations/{id}/reouverture`, sans motif
obligatoire (non demandé par le PO).

**Bloquant :** cette garde ne peut être posée que sur une identité réelle — dépend entièrement de
[TASK-074](TASK-074-authentification-reelle-grf.md) (l'authentification GRF actuelle est un mock
complet, `AuthController.cs:23-57`, sans `UT_Id`/`UT_Admin` réel dans le token). TASK-074 doit être
livrée avant ou avec le périmètre A de cette task.

## Périmètre proposé
### A. Garde d'accès (bloquant)
Restreindre `POST /api/declarations/{id}/reouverture` aux utilisateurs `UT_Admin = 1` (claim JWT
fourni par TASK-074). Refus explicite (403) sinon.

### B. Traçabilité (bloquant)
Journaliser chaque réouverture : utilisateur (`UT_Id`/login), horodatage, déclaration concernée.
Pas de motif obligatoire (non demandé par le PO). Réutiliser un mécanisme de log existant du projet
plutôt qu'en créer un nouveau si un équivalent existe déjà pour d'autres actions sensibles
(ex. clôture).

### C. Exposition UI (non bloquant, à la discrétion du PO)
Ajouter un point d'entrée dans l'écran ④ Intégration (ou ailleurs) si le workflow PO le prévoit
désormais. Sinon rester API/DB-only comme aujourd'hui.

### D. Test d'intégration réel (non bloquant, recommandé)
Automatiser le scénario intégrer→modif refusée (trigger réel)→rouvrir→modif acceptée contre une
vraie base, plutôt que des fakes.

## Garde-fous
1. **Ne pas toucher** aux triggers SQL (`003_Verrou_DT_Id.sql`) ni à la logique de dé-tamponnage
   existante (`DetamponnerAffectationsAsync`) — périmètre strictement gouvernance/traçabilité.
2. **Pas de nouvelle table/mécanisme de log** si un équivalent existe déjà pour la clôture — vérifier
   avant de créer.
3. Périmètre C/D non bloquants : peuvent être livrés séparément ou différés sans invalider A/B.

## Livrables de preuve (VERIFY)
1. Preuve réelle : tentative de réouverture par un utilisateur non autorisé → refusée (si garde de
   rôle retenue en A).
2. Preuve réelle : une réouverture effectuée → entrée de log/audit consultable (utilisateur,
   horodatage, déclaration, motif).
3. Confirmation qu'aucun fichier SQL de verrou n'a été modifié (diff limité au périmètre A/B).
4. Si C ou D traités : capture d'écran / test correspondant.

## Dépendances / risques
- **Dépend de** TASK-028, TASK-064 (verrou déjà livré, non touché).
- **Dépend de** [TASK-074](TASK-074-authentification-reelle-grf.md) pour le périmètre A (garde
  d'accès) : sans authentification réelle, aucune garde de rôle n'est applicable — actuellement
  tout le monde est « User » via le mock.
- **Risque** : si la décision produit préalable n'est pas tranchée avant développement, risque de
  refaire le travail de garde d'accès.
- **Aucun risque sur le mécanisme de verrou lui-même** — déjà validé en conditions réelles.
