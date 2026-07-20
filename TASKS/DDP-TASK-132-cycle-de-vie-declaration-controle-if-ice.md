# TASK-132 — DDP : cycle de vie de la déclaration + contrôle IF/ICE bloquant

## Contexte
Gestion de l'entête de déclaration (CDC §3.1, table `RT_DECLARATIONDELAISPAIEMENT` réutilisée telle
quelle : `DDP_Id, DDP_Numero, DDP_Date, DDP_Exercice, DDP_DateDebut, DDP_DateFin, DDP_Statut [0=EnCours,
1=Clôturé], DDP_Type [1=Annuelle,2=Trimestrielle], DDP_Periode, SO_Id, UT_Id/DDP_DateCreation,
UT_IdModif/DDP_DateModif, DDP_IsDepose, DDP_IsGeneretedFile, DDP_Libelle`). Seules les colonnes `Statut`,
`ModificateurNo/DateModif`, `IsDepose`, `Libelle`, `IsGeneretedFile` sont modifiables après création.

## Référence legacy (cycle de vie à reproduire)
`SocieteManager.Complement.cs:601-...` (`DeclarationDelaisPaiementCreate/Cloture/AnnulerCloture/Depose/
FichierGenerer/FichierAnnulerGeneration`) :
- **Création** : saisie Exercice/Type/Trimestre/Libellé ; calcul auto `DateDebut`/`DateFin` selon le type
  (année civile complète ou trimestre calendaire — bornes exactes déjà vérifiées en code, l.617-653) ;
  **unicité de période par société/exercice** — le legacy vérifie l'**égalité exacte** des bornes calculées
  (l.655, `Get(Societe.No, exercice, dateDebut, dateFin)`), suffisant car les bornes sont toujours calculées
  automatiquement (jamais saisies librement) — pas de vrai risque de chevauchement à gérer ici.
- **Clôture** : exige au moins une ligne intégrée (`HasLignes`) ; déclôture possible tant que le fichier
  n'est pas généré.
- **Génération fichier** : exige `Clôturé` + fichier pas déjà généré (TASK-133 pour le contenu du fichier).
- **Dépôt** : flag manuel (`DDP_IsDepose`) posé après export — **aucune intégration de plateforme externe**
  (décision PO §5.A-2, confirmée 19/07/2026).
- **Suppression** : possible uniquement si `EnCours` et sans aucune ligne intégrée.

## Contrôle bloquant IF/ICE (décision PO §5.A-5 — corrige l'anomalie §4.4 du CDC)
Le legacy porte un bloc de validation **entièrement commenté** (`DeclarationDelaisPaiementFileGenerator.cs:
78-91`) : identifiant fiscal fournisseur (8 caractères, sans espace), ICE fournisseur (15 caractères, sans
espace). À **réactiver et rendre réellement bloquant** avant génération du fichier (TASK-133), avec un
message explicite citant le(s) fournisseur(s) fautif(s) — valider **toutes** les lignes de la déclaration
avant de commencer à écrire quoi que ce soit (le legacy lève une exception ligne par ligne en cours de
génération, ce qui produit un fichier partiel avant l'échec — à éviter dans la réécriture : valider
d'abord, générer ensuite).

## Objectif
```
Entrée  : société, exercice, type (Annuelle/Trimestrielle), trimestre si applicable, libellé
Traitement : cycle de vie complet (création → intégration lignes TASK-131 → clôture → contrôle IF/ICE
             → génération TASK-133 → dépôt manuel)
Sortie  : entête DDP + lignes intégrées, statuts cohérents, blocages explicites
```

## Périmètre STRICT
- **Inclus** : CRUD entête, intégration de lignes (consomme TASK-131), cycle de vie complet, contrôle
  IF/ICE bloquant (validation, pas génération — TASK-133 génère).
- **Exclu** : sélection des lignes candidates (TASK-131, déjà livrée en amont), génération XML/ZIP
  (TASK-133), UI (TASK-134).

## Étapes
1. Entité + repository `RT_DECLARATIONDELAISPAIEMENT` (mapping identique, table existante).
2. Service de création : calcul auto des bornes de période selon type/trimestre, contrôle d'unicité par
   égalité exacte (suffisant, cf. ci-dessus).
3. Intégration de lignes : consomme la sortie de TASK-131 (avec calcul incrémental déjà résolu),
   persiste dans `RT_DECLARATIONDELAISPAIEMENTLG`.
4. Cycle de vie : clôture (garde `HasLignes`), déclôture (garde fichier non généré), suppression (garde
   `EnCours` + 0 ligne), dépôt (flag manuel, aucun appel externe).
5. Contrôle IF/ICE : validation complète de toutes les lignes de la déclaration avant d'autoriser
   `FichierGenerer` — retourne la liste des fournisseurs fautifs si échec, ne génère rien tant que le
   contrôle n'est pas 100 % passé.
6. Tests unitaires hors DB : cycle de vie complet, gardes de clôture/suppression/dépôt, contrôle IF/ICE
   (cas valide, cas invalide avec liste des fautifs, cas multi-fournisseurs fautifs).

## Livrables
- Service + repository entête déclaration.
- Contrôle IF/ICE bloquant réutilisable par TASK-133.
- Tests unitaires.

## Critères de validation
- Cycle de vie complet testé (création → intégration → clôture → contrôle IF/ICE → dépôt).
- Aucune génération de fichier possible avec un IF/ICE fournisseur absent ou mal formaté ; message
  explicite listant les fournisseurs fautifs.
- Build 0 erreur, tests verts.

## Risques / dépendances
- Dépend de TASK-131 (lignes candidates avec calcul incrémental correct).
- **Bloquant pour** TASK-133 (génération fichier) et TASK-134 (front).
