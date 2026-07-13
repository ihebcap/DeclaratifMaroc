# TASK-079 — Suppression d'une déclaration EnCours + enrichissement de l'écran liste

## Origine
Demande PO 13/07/2026 (capture écran liste des déclarations, référence implicite à une capture
GRC générique — style uniquement, cf. mémoire `grc-vs-declaration-modules-distincts`) : « améliorer
l'écran de la liste des déclarations » + « pouvoir supprimer une déclaration tant que non
clôturée » + « qui parmi les utilisateurs peut supprimer » + « la suppression libère les
règlements, les affectations et les factures ».

**Ne pas confondre avec TASK-073** : TASK-073 gouverne la **réouverture** d'une déclaration déjà
**clôturée** (`ReouvriDeclarationAsync`, remise en `EnCours`, aucune donnée effacée). Cette task
couvre un cas différent : la **suppression physique** d'une déclaration encore **`EnCours`**
(jamais clôturée, donc jamais tamponnée `DT_Id`).

## Constat (preuve code)
1. Aucune suppression physique de déclaration n'existe dans le code actuel — ni endpoint, ni
   méthode repository (`IDeclarationRepository.cs`), ni bouton front.
2. Le tampon `DT_Id` (verrou TASK-028/064, `003_Verrou_DT_Id.sql`) n'est posé **qu'à la clôture**
   (`DeclarationWorkflowService.cs:659`, `TamponnerAffectationsAsync`). Tant qu'une déclaration est
   `EnCours`, `RT_AFFECTATION.DT_Id` reste `NULL` pour toutes ses lignes — **rien n'est verrouillé**
   au niveau règlement/affectation/facture pendant cette phase.
3. Conséquence : « libérer les règlements/affectations/factures » d'une déclaration `EnCours` se
   résume techniquement à supprimer l'entête (`DM_ENTTVA`) et ses lignes candidates (`DM_LGTVA`,
   `LigneCandidate`) — aucune opération sur `RT_AFFECTATION`/`RT_MOUVEMENT` n'est nécessaire, ces
   tables ne référencent la déclaration que via un `DT_Id` qui n'a jamais été posé. Confirmé par le
   PO (décision produit ci-dessous).
4. Écran liste actuel (`declaration-tva-web/src/DeclarationList.tsx`) : colonnes Numéro/Exercice/
   Période/Type/Statut, seule action = « Ouvrir ». Aucune colonne Lignes/Montant TVA, aucune action
   Supprimer — gap déjà identifié dans `TODO.md` (§ Écran §1 « Gestion déclarations », enrichissement
   annoncé mais jamais traité formellement, faute de task dédiée).
5. Précédent direct : TASK-073 a posé le mécanisme de garde+audit pour une opération sensible
   similaire (réouverture) — réservé `UT_Admin=1`, journalisé via `JournaliserAudit`
   (`DeclarationWorkflowService.cs:75`, ILogger + fichier `logs/`). À réutiliser à l'identique, pas
   de nouveau mécanisme de log.

## Décision produit — TRANCHÉE (PO 13/07/2026)
- **Qui peut supprimer** : réservé aux utilisateurs `UT_Admin = 1` (même garde que TASK-073).
- **Traçabilité** : obligatoire — log qui/quand/déclaration, réutilisant `JournaliserAudit`.
- **Portée technique de « libérer »** : suppression physique de l'entête `DM_ENTTVA` + toutes ses
  lignes `DM_LGTVA` de cette déclaration. Aucune écriture sur `RT_AFFECTATION`/`RT_MOUVEMENT`
  requise (rien n'y est tamponné avant clôture). Confirmé : pas d'autre portée attendue.

## Périmètre
### A. Backend — suppression (bloquant)
1. `IDeclarationRepository` : nouvelle méthode `DeleteAsync(Guid declarationId)` — supprime les
   lignes `LigneCandidate` de la déclaration puis l'entête `DeclarationEntete` (ordre FK).
2. `DeclarationWorkflowService.SupprimerDeclarationAsync(Guid declarationId, string utilisateur)` :
   - Garde métier : `InvalidOperationException` si `declaration.Statut != EnCours` (une déclaration
     clôturée ne se supprime pas — elle se rouvre, cf. TASK-073, puis peut être supprimée une fois
     revenue `EnCours` si on le souhaite, mais **pas en un seul appel** — pas de contournement de
     TASK-073 par ce nouvel endpoint).
   - Appelle `_repository.DeleteAsync`.
   - `JournaliserAudit($"[SUPPRESSION] Déclaration {declaration.Numero} ({declarationId}) supprimée par '{utilisateur}'.")`.
3. `DeclarationsController` : `DELETE /api/declarations/{id}`, garde `User.HasClaim("UT_Admin", "1")`
   → 403 sinon (même pattern que `POST .../reouverture`), 400 si statut invalide, 404 si absente.

### B. Front — enrichissement écran liste (bloquant)
`DeclarationList.tsx` :
1. Ajouter colonnes **Lignes** (count) et **Montant TVA** (somme) par déclaration — nécessite
   soit un enrichissement de `GET /api/declarations` (agrégats), soit un appel dérivé ; à trancher
   en conception (voir Risques).
2. Ajouter une colonne/zone **Actions** avec un bouton **Supprimer** :
   - visible uniquement si `dec.statut === 'EnCours'` **et** l'utilisateur courant a le claim
     `UT_Admin=1` (décodé du JWT, même logique que la garde back) ;
   - confirmation obligatoire avant appel (modale, pas de `window.confirm` — cohérence UI existante) ;
   - appelle `DELETE /api/declarations/{id}` puis rafraîchit la liste (`fetchDeclarations`).
3. Ne pas ajouter de bouton de réouverture ici (hors périmètre — TASK-073 périmètre C, non traité,
   à la discrétion du PO, ne pas dériver).

## Garde-fous
1. Ne toucher à aucun trigger SQL (`003_Verrou_DT_Id.sql`) — hors périmètre, la garde technique
   existante n'a pas besoin d'évoluer pour ce cas (rien n'y est tamponné avant clôture).
2. Ne pas permettre la suppression d'une déclaration `Cloturee` par ce nouvel endpoint, même par un
   `UT_Admin` — le chemin obligatoire reste réouverture (TASK-073) puis suppression séparée si
   souhaité, pour ne pas créer un raccourci qui contournerait la traçabilité de la réouverture.
3. Réutiliser `JournaliserAudit` existant — pas de nouvelle table/mécanisme de log (même garde-fou
   que TASK-073 §2).
4. Confirmer qu'aucune ligne `DM_LGTVA` de la déclaration supprimée ne porte de `DT_Id` non nul sur
   les affectations correspondantes avant suppression (vérification défensive, cas normalement
   impossible pour une déclaration `EnCours` mais à vérifier par une requête, pas supposé).

## Risques / points à trancher en conception
- **Agrégats Lignes/Montant TVA sur la liste** : calcul à la volée sur `GET /api/declarations`
  (potentiellement coûteux si beaucoup de déclarations/lignes) vs colonnes dénormalisées maintenues
  à la clôture. À évaluer par l'implémenteur selon volumétrie réelle (`GetAllAsync` actuel ne
  projette que l'entête) — ne pas dégrader la performance de l'écran d'accueil.
- **Décodage du claim `UT_Admin` côté front** : vérifier le mécanisme déjà utilisé par TASK-074
  pour exposer ce claim au front (probablement déjà fait pour d'autres boutons admin) plutôt que
  d'en créer un nouveau.

## Livrables de preuve (VERIFY)
1. Preuve réelle : suppression d'une déclaration `EnCours` par un `UT_Admin` → entête + lignes
   disparues, règlements/factures sous-jacents immédiatement re-sélectionnables (créer une nouvelle
   déclaration sur la même période, vérifier que les mêmes lignes réapparaissent).
2. Preuve réelle : tentative de suppression par un utilisateur non-admin → 403.
3. Preuve réelle : tentative de suppression d'une déclaration `Cloturee` → 400, message explicite.
4. Entrée de log/audit consultable après suppression (fichier `logs/`).
5. Capture écran liste enrichie (colonnes Lignes/TVA + bouton Supprimer visible seulement si
   `EnCours` + admin).
6. Build back + front + tests existants (aucune régression sur `IDeclarationRepository` ni le
   contrôleur).

## Dépendances
- **Dépend de** TASK-074 (authentification réelle, claim `UT_Admin`) — déjà livrée.
- **S'appuie sur** le mécanisme d'audit de TASK-073 (`JournaliserAudit`) — réutilisation, pas de
  nouvelle dépendance de build.
- **Aucune dépendance** sur TASK-028/064 (verrou) — confirmé hors périmètre (§ Garde-fous).
