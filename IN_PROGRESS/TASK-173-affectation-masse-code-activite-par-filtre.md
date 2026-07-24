# TASK-173 — Affectation en masse du code activité (sélection multi-lignes par filtres)

## Contexte
Demande PO directe (24/07/2026) : le mécanisme d'auto-résolution du code activité (TASK-161/171,
mapping par tiers) n'est à son avis pas le levier réellement utilisé en pratique. Le vrai besoin
opérationnel : permettre à l'utilisateur de sélectionner plusieurs lignes de taxe (filtrées par
fournisseur, par facture, etc.) et de leur affecter un **même code activité en une seule action**, plutôt
que de poser le code ligne par ligne.

## Constat (vérifié en lisant le code, pas supposé)
- L'affectation du code activité existe aujourd'hui **uniquement ligne par ligne** :
  `PATCH {id}/lignes/{ligneId}/code-activite` (TASK-161, `DeclarationsController.cs`) — cible une seule
  `ligneId` à la fois. Côté front, `VerifierIntegrerPanel.tsx:41` rend la colonne "Code activité"
  éditable (select) directement dans la grille, cellule par cellule.
- Une mécanique de sélection en masse **existe déjà** pour une autre action (changement d'état de
  ligne) : `POST {id}/lignes:bulk` (`DeclarationsController.cs:278`, `BulkUpdateEtatRequest`) — accepte
  **soit** une liste explicite de `LigneIds`, **soit** un couple `Domaine` + `Filter` (objet de filtres
  arbitraire, TASK-012 "action de masse intelligente" avec bannière "sélectionner tout le filtre" côté
  front). Cette mécanique ne sert aujourd'hui que pour l'`Etat` de la ligne, jamais pour le code activité.
- Référentiel des codes : `P_DECTVAACTIVITE` (`DTA_Id, DTA_Code, DTA_Intitule, DTA_Domaine, DTA_Taux,
  DTA_Famille, DTA_Prorata`, cf. requête fournie par le PO) — confirmé en base, seuls `DTA_Code`/
  `DTA_Intitule` sont exploités aujourd'hui côté GRF (cf. TASK-172, filtrage par `DTA_Domaine` pas encore
  fait).

## Objectif
1. Étendre (ou dupliquer selon le choix technique) la mécanique `:bulk` déjà en place pour accepter une
   action **"assigner code activité"** : même sélection (liste d'IDs *ou* domaine+filtres) qu'aujourd'hui
   pour l'État, appliquée à `CodeActiviteModifieManuellement`/`CodeActivite` de chaque ligne sélectionnée
   (mêmes champs que la surcharge manuelle unitaire TASK-161 : `CodeActiviteModifiePar`/
   `CodeActiviteModifieLe` doivent être posés identiquement, traçabilité qui/quand déjà en place à
   réutiliser, pas dupliquer).
2. Front : réutiliser l'UI de sélection multi-lignes déjà existante pour l'action de masse sur l'État
   (bannière "sélection de tout le filtre", filtres fournisseur/facture déjà disponibles dans la grille
   `DomainGrid`/`codeActiviteColumns`) — ajouter l'action "Affecter un code activité" à côté de l'action
   de masse déjà existante, plutôt que construire un second mécanisme de sélection parallèle.
3. Respecter le filtrage par domaine de TASK-172 : le sélecteur de code activité proposé dans l'action de
   masse ne doit proposer que les codes du domaine des lignes sélectionnées (Encaissement ou
   Décaissement) — **dépendance directe sur TASK-172**, à livrer avant ou en même temps.
4. **Décision PO à trancher** : que se passe-t-il si la sélection multi-lignes mélange des lignes
   Encaissement ET Décaissement (sélection inter-onglets, si possible) ? **Recommandation architecte** :
   bloquer l'action de masse "code activité" à un seul domaine à la fois (comme le fait déjà `:bulk`
   aujourd'hui pour l'État, qui prend un `Domaine` unique en paramètre) — évite un choix de code
   activité ambigu sur une sélection mixte.
5. Une fois livré, vérifier en conditions réelles qu'une sélection filtrée (ex. toutes les lignes d'un
   fournisseur donné) reçoit bien le même code activité après l'action, avec la traçabilité qui/quand
   correcte sur chaque ligne, sans toucher aux lignes hors sélection.

## Garde-fous
- Réutiliser strictement le mécanisme de sélection par filtre déjà livré (TASK-012/`:bulk`) — ne pas
  construire un second système de filtrage/sélection multi-lignes parallèle.
- Jamais de changement d'état (`Etat`) ni de `DT_Id`/périmètre de déclaration par cette action — un seul
  champ concerné (`CodeActivite` + métadonnées de traçabilité).
- Blocage si déclaration `Clôturée` — même garde-fou que le `PATCH` unitaire (TASK-161 :
  `DeclarationWorkflowService.ModifierCodeActiviteLigneAsync` lève déjà cette exception, à reproduire
  pour la variante bulk, pas à contourner).
- Dépend de TASK-172 (filtrage par domaine) pour que le sélecteur de code proposé dans l'action de masse
  soit correct — ne pas livrer cette task avant ou sans TASK-172.

## Files
- [Declaration.API/Controllers/DeclarationsController.cs](../Declaration.API/Controllers/DeclarationsController.cs) (`POST {id}/lignes:bulk`, `BulkUpdateEtatRequest` — nouvelle variante ou extension pour code activité).
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`ModifierCodeActiviteLigneAsync`, `UpdateLignesEtatBulkAsync`/`UpdateLignesEtatBulkByIdsAsync` comme patron de référence).
- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs) (nouvelle méthode bulk code activité, par analogie aux méthodes bulk État existantes).
- [declaration-tva-web/src/VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) / [DomainGrid.tsx](../declaration-tva-web/src/DomainGrid.tsx) (UI de sélection multi-lignes déjà existante, à étendre avec la nouvelle action).
- Dépendance : [TASK-172](TASK-172-referentiel-code-activite-non-filtre-par-domaine.md) (filtrage du référentiel par domaine).

## Validation
- [ ] Build back + front OK.
- [ ] Sélection par filtre (ex. un fournisseur donné) puis affectation en masse d'un code activité →
      toutes les lignes du filtre reçoivent le même code, vérifié en base (`DM_LGTVA` ou équivalent) et
      au navigateur.
- [ ] Traçabilité qui/quand posée sur chaque ligne affectée, identique au comportement unitaire TASK-161.
- [ ] Blocage confirmé si la déclaration est `Clôturée` (409 explicite, pas un 500).
- [ ] Sélecteur de code activité de l'action de masse ne propose que les codes du domaine concerné
      (dépend de TASK-172).
- [ ] Décision PO tracée sur le blocage domaine mixte (§4).
- [ ] Aucune régression sur l'action de masse existante (changement d'État, `:bulk` actuel).

## Dépendances / risques
- **Dépend de TASK-172** (filtrage par domaine du référentiel) — sans elle, l'action de masse proposerait
  des codes incohérents avec le domaine des lignes sélectionnées.
- Risque principal : si la sélection par filtre porte sur un grand nombre de lignes, l'écriture en masse
  doit rester une opération SQL batch (pas une boucle ligne par ligne côté application) — même exigence
  de performance que celle déjà appliquée aux méthodes bulk État existantes.
- Aucune dépendance sur TASK-171 (cascade auto-résolution) — cette task couvre la saisie manuelle en
  masse, un besoin indépendant du sort de la cascade automatique.
