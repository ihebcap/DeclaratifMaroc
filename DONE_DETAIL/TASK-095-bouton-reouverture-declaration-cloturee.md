# TASK-095 — Exposition UI du bouton « Réouvrir » (déclaration Clôturée)

## Origine
Demande PO 14/07/2026, suite à l'incident TASK-094 : « on ajoute le bouton avec traçabilité ».
Concrétise le Périmètre C de TASK-073 (« Exposition UI », explicitement laissé non bloquant/à la
discrétion du PO — voir `DONE_DETAIL/TASK-073-gouvernance-reouverture-declaration.md` §Périmètre C,
et la note correspondante dans `TODO.md` §🔐 Gouvernance & traçabilité : « pas de task de suivi
ouverte, à recréer si le PO le demande »). Cette task recrée ce suivi.

## Constat (preuve code, aucune supposition)
1. **L'endpoint existe et est gouverné** — `Declaration.API/Controllers/DeclarationsController.cs:315-334`
   (`POST /api/declarations/{id}/reouverture`) : garde `User.HasClaim("UT_Admin", "1")` → `403`
   sinon (TASK-073 §A, livré). Effet : `ReouvriDeclarationAsync` (`DeclarationWorkflowService.cs:878-900`)
   refuse si `Statut != Cloturee` (`400`), sinon détamponne les affectations liées puis repasse
   `Statut=EnCours`.
2. **La traçabilité backend existe déjà et n'a rien à refaire** — `JournaliserAudit`
   (`DeclarationWorkflowService.cs:83-94`, TASK-073 §B livré) écrit une ligne `[REOUVERTURE]
   Déclaration {Numero} ({Id}) rouverte par '{utilisateur}'` via `ILogger.LogWarning` **et**
   `logs/audit.log` (fichier, pour rester consultable en Windows Service). Appelée ligne 899, à
   chaque réouverture, sans exception possible (pas de chemin de code qui la contourne). **Aucun
   développement de traçabilité backend n'est nécessaire** — seulement l'exposer/consommer côté UI
   si le PO veut la voir affichée (§Périmètre ci-dessous).
3. **Aucun bouton n'existe côté UI** — confirmé par recherche (`grep "reouverture|Rouvrir"` sur
   `declaration-tva-web/src`) : zéro occurrence. Le seul point d'entrée actuel est l'appel API direct.
4. **Pattern UI existant directement réutilisable** — `declaration-tva-web/src/DeclarationList.tsx` :
   - Ligne 122 : `const peutSupprimer = isAdmin && dec.statut === STATUT_EN_COURS;` — garde
     identique en forme à celle nécessaire ici (`isAdmin && dec.statut === STATUT_CLOTUREE`).
   - `isAdmin` (prop, ligne 50/56) provient de `App.tsx:204` ← `Auth.tsx:63`
     (`Boolean(res.data.isAdmin ?? res.data.IsAdmin)`, résolu au login) — même source que le claim
     JWT `UT_Admin`, aucune nouvelle plomberie d'auth à créer.
   - `DeleteConfirmModal` (lignes 13-46) : modal de confirmation avec libellé d'avertissement +
     bouton d'action + état `loading` — gabarit direct pour une `ReopenConfirmModal` équivalente.
   - `STATUT_CLOTUREE = 1` (ligne 10) déjà défini.
5. **Convention de sérialisation** — le statut est un **nombre** côté API (`System.Text.Json` sans
   `JsonStringEnumConverter`, commentaire ligne 5-8 de `DeclarationList.tsx`) : toute comparaison doit
   utiliser les constantes numériques existantes, jamais une chaîne.

## Objectif
Ajouter, dans l'écran liste des déclarations, un bouton « Réouvrir » visible uniquement pour un
utilisateur `UT_Admin=1` sur une déclaration `Statut=Cloturee`, qui déclenche
`POST /api/declarations/{id}/reouverture` après confirmation explicite, et qui rend visible à
l'utilisateur la traçabilité déjà produite côté backend (qui/quand la réouverture a eu lieu).

## Périmètre proposé
### A. Bouton + garde d'affichage (bloquant)
Dans `DeclarationList.tsx`, ajouter une condition symétrique à `peutSupprimer` :
`const peutRouvrir = isAdmin && dec.statut === STATUT_CLOTUREE;` et un bouton (icône dédiée, ex.
`RotateCcw` de `lucide-react`, déjà utilisé ailleurs dans le dépôt) affiché seulement si
`peutRouvrir`, à côté du bouton Supprimer existant.

### B. Confirmation UX (bloquant)
Réutiliser le gabarit `DeleteConfirmModal` pour une modal de confirmation dédiée. Le texte
d'avertissement doit être **exact et non édulcoré** sur l'effet réel (cf. constat 1) : la
déclaration repasse `EnCours`, redevient modifiable, et les affectations tamponnées associées sont
libérées (`DT_Id → NULL`) — donc de nouveau éligibles à un rapprochement concurrent tant que la
déclaration n'est pas re-clôturée. Pas de motif de réouverture obligatoire (non demandé par le PO,
cohérent avec TASK-073 §Décision produit préalable).

### C. Restitution de la traçabilité en UI (bloquant, sur demande PO explicite « avec traçabilité »)
Le mécanisme d'audit backend existe déjà (constat 2) — ne pas le redévelopper. Deux façons possibles
d'en rendre l'information visible côté UI, à trancher en conception selon l'usage attendu :
- **C1 (minimal)** : après réouverture réussie, toast de confirmation reprenant l'utilisateur et
  l'horodatage (déjà connus côté client : utilisateur courant, heure de l'action) — aucun nouvel
  endpoint nécessaire.
- **C2 (plus complet)** : un endpoint de lecture exposant les dernières lignes pertinentes de
  `logs/audit.log` (ou un futur stockage structuré) pour affichage a posteriori dans l'écran liste
  (ex. tooltip « rouverte par X le Y » sur les déclarations `EnCours` ayant déjà été clôturées).
  Plus coûteux : nécessite de parser/exposer un fichier de log texte ou de créer un stockage
  structuré — **à ne pas entreprendre sans validation PO explicite du besoin** (le fichier de log
  actuel n'est pas conçu comme source consultable via API).

## Garde-fous
1. Ne toucher ni au trigger d'immuabilité (`003_Verrou_DT_Id.sql`) ni à `ReouvriDeclarationAsync` ni
   à `JournaliserAudit` — le mécanisme backend (garde + effet + audit) est déjà livré et validé
   (TASK-073 §A/§B), périmètre strictement front + éventuel endpoint de lecture seule pour C2.
2. Ne pas introduire de nouvelle notion de rôle : réutiliser `isAdmin` tel qu'exposé aujourd'hui.
3. Respecter la convention de sérialisation numérique du statut (constat 5) — pas de comparaison par
   chaîne.
4. Si C2 retenu : lecture seule stricte, aucune écriture nouvelle sur `logs/audit.log` depuis l'UI.

## Risques / points à trancher en conception
- Choix C1 vs C2 pour la restitution de traçabilité — C1 suffit à « on voit qui/quand vient de le
  faire », C2 répond à « on veut consulter l'historique plus tard » ; à trancher selon l'intention
  réelle du PO derrière « avec traçabilité ».
- Emplacement du bouton : liste (`DeclarationList.tsx`, comme Supprimer) vs écran détail/stepper —
  proposé en liste par cohérence avec le bouton Supprimer existant, à confirmer si un autre écran
  est jugé plus adapté.

## Livrables de preuve (VERIFY)
1. Capture d'écran : bouton visible pour un compte `UT_Admin=1` sur une déclaration `Cloturee`,
   absent pour un compte non-admin et absent sur une déclaration `EnCours`/autre statut.
2. Preuve réelle (pas mockée) : réouverture déclenchée depuis le bouton contre
   `.\sql2022`/`GR_EMA_DISTRIBUTION` → déclaration repasse `EnCours`, `logs/audit.log` contient la
   ligne `[REOUVERTURE]` correspondante, et la restitution UI (C1 ou C2) affiche l'information
   attendue.
3. Preuve : tentative par un compte non-admin refusée en amont par l'UI (bouton absent) **et** par
   le backend (403 si appel direct API contourné) — non-régression de la garde existante.
4. Build + tests front (`npm run build` / suite existante `declaration-tva-web`) verts.

## Dépendances
- **S'appuie sur** TASK-073 (garde d'accès + audit backend déjà livrés, non modifiés) et TASK-094
  (contexte de l'incident à l'origine de la demande).
- **Aucune dépendance bloquante** côté backend — périmètre A/B purement front, C2 (si retenu)
  ajoute un unique endpoint de lecture seule.
