# TASK-129 — Convention de délai de paiement par tiers (back)

## Contexte
Sous-fonctionnalité 2 du périmètre Délai de Paiement Maroc (CDC §3.2). Table existante réutilisée telle
quelle : `RT_CONVENTIONTIERS` (`CP_Id, SO_Id, CT_No, CT_Code, CP_Date, CP_Numero, CP_DateDebut, CP_DateFin,
CP_FileName, CP_File, CP_DelaisPaiement, CP_Domaine, CP_Type [0=Convention,1=Facture], CP_FactureNo`).
Développement neuf (back : `Declaration.Application`/`Declaration.Core`, pas de réutilisation DLL).

## Référence legacy (comportement à reproduire, avec 2 corrections actées PO)
`SocieteManager.Complement.cs:471-589` (`ConventionDelaisPaiementTiersCreate`/`Terminer`) :
- Deux types : **Convention** (plage `DateDebut`/`DateFin`) et **Facture** (dérogation ponctuelle liée à
  un numéro de facture, sans limite de validité).
- Plafond **180 jours**, contrôle applicatif (pas de contrainte base) — **déjà en couche service** dans le
  legacy (l.491-492), à reproduire au même niveau (pas seulement en validation front).
- Type Facture : la facture référencée doit exister et être `NonPaye` ; une seule convention par facture.
- Type Convention : `DateFin ≥ DateDebut` ; une seule convention par tiers dont la plage couvre une même
  date de début (contrôle **à corriger**, voir ci-dessous).
- Clôture anticipée (`ConventionDelaisPaiementTiersTerminer`, l.564-589) : nouvelle `DateFin` doit être
  comprise entre `DateDebut` et l'ancienne `DateFin`.
- Validité d'affichage : Convention → `DateFin ≥ aujourd'hui` ; Facture → présence du numéro de facture
  (pas d'expiration).
- Pièce jointe PDF : **reste optionnelle** au niveau service (décision PO 19/07/2026 — pas de durcissement,
  cohérent avec le comportement actuel où `fileName`/`file` ne sont vérifiés qu'en cohérence mutuelle,
  jamais en présence obligatoire, l.482-483/489).

## Anomalie à corriger (découverte en code, absente du CDC §4, décision PO 19/07/2026 : corriger)
**Contrôle de chevauchement incomplet.** Le legacy (l.511) ne vérifie que si la **date de début** de la
nouvelle convention tombe dans la plage d'une convention existante — il ne vérifie ni la date de fin de
la nouvelle convention, ni le cas où elle engloberait entièrement une convention existante. Un
`// TODO: verifier le chauvochement des date` (l.531) confirme que ce contrôle a toujours été reconnu
incomplet par les développeurs d'origine, jamais corrigé.

Exemple de trou : convention existante A = 01/02→28/02, nouvelle convention B = 01/01→31/03 (démarre
avant A, la recouvre entièrement) → le legacy laisse passer B sans erreur, car A ne contient pas le
01/01. Résultat : deux conventions actives simultanément sur la même période avec des délais différents,
et le calculateur (TASK-127) prend la première trouvée sans ordre garanti → délai appliqué imprévisible.

**Correction à implémenter** : contrôle de chevauchement bidirectionnel standard —
```
chevauchement si (nouvelle.DateDebut ≤ existante.DateFin) ET (existante.DateDebut ≤ nouvelle.DateFin)
```
appliqué contre **toutes** les conventions actives du même tiers/domaine (pas seulement celle dont la
date de début contiendrait la nouvelle date de début). Bloquant à la création, avec message explicite
citant la convention en conflit.

## Objectif
```
Entrée  : tiers, société, domaine (Fournisseur/Client), type (Convention/Facture), dates, délai (jours),
          pièce jointe optionnelle
Traitement : valider plafond 180j, valider chevauchement bidirectionnel (type Convention) ou unicité
             facture (type Facture), créer/lister/clôturer par anticipation/supprimer
Sortie  : convention persistée dans RT_CONVENTIONTIERS, consommable par TASK-127 (résolution du délai)
```

## Périmètre STRICT
- **Inclus** : CRUD conventions (Create/Get/GetAll/Delete/Terminer), contrôle 180j, contrôle chevauchement
  bidirectionnel corrigé, contrôle unicité facture, repository Dapper sur `RT_CONVENTIONTIERS` (table
  existante, aucune migration).
- **Exclu** : UI (TASK-130), résolution du délai pour une facture donnée (TASK-127, déjà livrée en amont),
  pièce jointe : stockage tel quel (`varbinary`/`CP_File`), pas de nouvelle validation de format.

## Étapes
1. Entité + repository Dapper sur `RT_CONVENTIONTIERS` (mapping identique aux colonnes listées ci-dessus).
2. Service de création : plafond 180j, contrôle chevauchement bidirectionnel (nouveau, corrige le TODO
   legacy), contrôle unicité facture (type Facture), validation dates (`DateFin ≥ DateDebut`).
3. Clôture anticipée (`Terminer`) : mêmes contraintes que le legacy (nouvelle `DateFin` entre `DateDebut`
   et l'ancienne `DateFin`).
4. Suppression : reproduire le comportement legacy (suppression directe — vérifier avec l'architecte si
   une garde doit être ajoutée pour empêcher la suppression d'une convention déjà utilisée par une ligne
   DDP intégrée, le legacy n'en a aucune ; à confirmer avant de coder une garde non demandée).
5. Tests unitaires hors DB : plafond 180j, chevauchement bidirectionnel (les 4 configurations : contenue,
   englobante, partielle gauche, partielle droite), unicité facture, clôture anticipée (bornes invalides).

## Livrables
- Service + repository conventions.
- Tests unitaires (couverture des 4 configurations de chevauchement).

## Critères de validation
- Aucune convention chevauchante ne peut être créée, dans les 4 configurations de test.
- Plafond 180j et clôture anticipée identiques au comportement legacy validé.
- Build 0 erreur, tests verts.

## Risques / dépendances
- Dépend de TASK-127 uniquement pour l'intégration finale (le calculateur lit les conventions) —
  peut être développé en parallèle si l'interface de lecture est stubée.
- **Bloquant pour** TASK-130 (front).
- Question ouverte (non bloquante, à trancher avant TASK-130) : valeur par défaut de la date de fin
  proposée à l'écran lors de la saisie d'une date de début — le legacy a un bug d'incohérence entre le
  modèle (+6 mois calculé) et l'affichage (+12 mois proposé, CDC §4.7) ; choisir **une seule** valeur
  cohérente entre back et front avant l'implémentation front.
