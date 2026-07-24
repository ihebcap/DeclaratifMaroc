# TASK-172 — Référentiel « codes activité » non filtré par domaine (Encaissement/Décaissement)

## Contexte
Suite directe de TASK-161/171. Le PO signale (24/07/2026) que `P_DECTVAACTIVITE` porte une colonne
`DTA_Domaine` (1 = Encaissement, 2 = Décaissement) et s'interroge sur son usage réel. Vérification
architecte (lecture code) : cette colonne existe bien en base mais **n'est jamais lue ni exploitée** par
GRF — le référentiel exposé à l'écran mélange les codes des deux domaines sans distinction.

## Constat (vérifié en lisant le code, pas supposé)
- `DeclarationRepository.GetReferentielCodesActiviteAsync` (`DeclarationRepository.cs:1560`) :
  ```sql
  SELECT DTA_Code AS Code, DTA_Intitule AS Libelle FROM P_DECTVAACTIVITE ORDER BY DTA_Code
  ```
  Ne sélectionne ni `DTA_Domaine`, ni `DTA_Taux`, ni `DTA_Famille`, ni `DTA_Prorata` — uniquement
  `Code`/`Libelle`. Aucun paramètre de domaine dans la signature de la méthode.
- `CodeActiviteReferentielRow` (`WorkflowEntities.cs:143-147`) : modèle C# limité à `Code`/`Libelle`,
  aucun champ `Domaine`.
- `GET /api/codes-activite` (`DeclarationsController.cs:180`) : endpoint sans paramètre de domaine,
  retourne l'intégralité de la table.
- Front (`VerifierIntegrerPanel.tsx:298-314`) : `codeActiviteOptions` chargé **une seule fois**
  (`useEffect(..., [])`), utilisé tel quel pour peupler le select éditable de la colonne "Code activité"
  aussi bien sous l'onglet Encaissement que Décaissement (même état, aucune re-fetch/filtre au
  changement d'onglet).

**Conséquence concrète** : si `P_DECTVAACTIVITE` contient des codes propres à l'Encaissement (TVA
collectée) et d'autres propres au Décaissement (TVA déductible) — ce que confirme l'existence même de
`DTA_Domaine` — un utilisateur éditant une ligne Décaissement voit dans le menu déroulant des codes
réservés à l'Encaissement, et inversement. Rien n'empêche aujourd'hui de sélectionner un code du mauvais
domaine sur une ligne — aucune validation serveur ne recroise `DTA_Domaine` avec le domaine de la ligne
au moment de l'écriture (`PATCH .../code-activite`, TASK-161).

## Objectif
1. `GetReferentielCodesActiviteAsync` : ajouter `DTA_Domaine` à la sélection (et un paramètre de filtre
   optionnel), afin que l'API puisse retourner uniquement les codes du domaine demandé — ou retourner le
   domaine dans chaque ligne pour un filtrage côté front, au choix de l'implémentation.
2. `GET /api/codes-activite` : accepter un paramètre `domaine` (Encaissement/Décaissement) et ne
   retourner que les codes compatibles (+ éventuellement les codes sans domaine renseigné, si ce cas
   existe en base — à vérifier, ne pas les exclure silencieusement sans vérification).
3. Front : recharger/refiltrer `codeActiviteOptions` selon l'onglet actif (Encaissement/Décaissement),
   pas une seule fois de façon globale.
4. **Décision PO à trancher** : faut-il aussi valider **côté serveur**, au moment du `PATCH
   .../code-activite`, que le code choisi correspond bien au domaine de la ligne (garde-fou dur), ou se
   contenter de restreindre l'affichage (le serveur reste permissif) ? **Recommandation architecte** :
   valider aussi côté serveur — un filtre uniquement côté front n'empêche pas un appel API direct avec un
   code du mauvais domaine.

## Garde-fous
- Lecture seule stricte de `P_DECTVAACTIVITE` (référentiel déjà en lecture seule GRF, TASK-161 — aucun
  changement à ce principe).
- Ne pas exclure silencieusement les codes dont `DTA_Domaine` serait NULL/0/valeur inattendue — vérifier
  d'abord les valeurs réellement présentes en base avant d'écrire le filtre, et signaler si un cas
  imprévu existe plutôt que de le masquer.
- Aucune régression sur la surcharge manuelle déjà en place (TASK-161) — ce correctif ne fait que
  restreindre les *options proposées*, jamais les valeurs déjà enregistrées sur des lignes existantes.

## Files
- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs) (`GetReferentielCodesActiviteAsync`).
- [Declaration.Application/Entities/WorkflowEntities.cs](../Declaration.Application/Entities/WorkflowEntities.cs) (`CodeActiviteReferentielRow`).
- [Declaration.API/Controllers/DeclarationsController.cs](../Declaration.API/Controllers/DeclarationsController.cs) (`GET /api/codes-activite`, `PATCH .../code-activite` si validation serveur retenue §4).
- [declaration-tva-web/src/VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) (chargement de `codeActiviteOptions`).

## Validation
- [ ] Build back + front OK.
- [ ] Vérification réelle en base des valeurs distinctes de `DTA_Domaine` présentes dans
      `P_DECTVAACTIVITE` (pas seulement 1/2 supposés — confirmer qu'aucune autre valeur/NULL n'existe).
- [ ] Menu déroulant "Code activité" sous l'onglet Décaissement ne propose plus de codes Encaissement,
      et inversement — vérifié au navigateur.
- [ ] Décision PO tracée sur la validation serveur (§4).
- [ ] Aucune régression sur les codes déjà affectés à des lignes existantes (une ligne dont le code a été
      posé avant ce correctif reste affichée telle quelle, même si son domaine ne correspondrait plus —
      pas de purge rétroactive).

## Dépendances / risques
- Risque principal si non traité : erreurs de saisie invisibles (code activité incohérent avec le sens
  de la ligne), sans blocage ni alerte — non bloquant pour le dépôt DGI (le code activité n'entre pas
  dans le XML, décision PO TASK-161 point 3) mais peut fausser des récapitulatifs internes par activité
  (TASK-160).
- Indépendant de TASK-171 (cascade de résolution automatique) — ce correctif porte sur la liste des
  options proposées à la saisie manuelle, pas sur la résolution automatique par défaut.
