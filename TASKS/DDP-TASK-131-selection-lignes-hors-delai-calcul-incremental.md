# TASK-131 — DDP : sélection des lignes hors délai + calcul incrémental anti-double-déclaration

## Contexte
Cœur métier de la Déclaration délai de paiement (DDP, CDC §3.1). C'est la tâche la plus à risque du
périmètre : le legacy contient **3 anomalies non documentées au CDC §4**, découvertes en lisant le code
source pendant la session d'analyse du 19/07/2026, dont une **absence totale de mécanisme anti-double-
déclaration** — confirmée à tous les niveaux (service, requête de sélection, contrainte base). Décision
PO : corriger les 3.

Table réutilisée telle quelle : `RT_DECLARATIONDELAISPAIEMENTLG` (`DDPL_Id, DDP_Id, EC_Id, AF_Id,
DDPL_Depassement, DDPL_EcheanceLegale`) — l'historique des déclarations passées (y compris celles faites
via l'ancien applicatif, si le client l'utilisait) est **nativement présent** dans cette table partagée,
aucune migration de données n'est nécessaire pour en tirer parti.

## Référence legacy (algorithme à reprendre, avec les 3 corrections ci-dessous)
`LigneControleDelaisPaiementController.GetAll` (`Tresorerie.UIDeclarationTva/DeclarationDelaisPaiement/
LigneControleDelaisPaiementController.cs:60-293`).

Seuils légaux **en dur, à reproduire à l'identique** (dates statutaires de la loi marocaine, pas de
paramétrage par client) : `dateDebDecLoi = 2023-07-01`, `dateLimiteMontant = 2024-12-31`, seuil montant
`10 000` (devise société) applicable uniquement entre ces deux dates.

**Trois cas de figure** (repris du legacy, algorithme complet vérifié en code) :
1. Échéance hors période (échéance légale < début de période), **non payée** (`Etat == NonPaye`).
2. Échéance hors période, **payée pendant la période courante** (recherche par affectation/règlement,
   distinction pièce — chèque/traite/virement, sur date de pointage — vs espèce/autre, sur date de
   règlement).
3. Échéance **dans la période**, avec la même distinction pièce/espèce sur la date déterminante, plus le
   solde restant non affecté si la facture reste partiellement impayée en fin de période.

## Anomalies découvertes (hors CDC §4) — décision PO 19/07/2026 : corriger les 3

### 1. Absence de mécanisme anti-double-déclaration (le plus important)
`DeclarationDelaisPaiementLigneAjouter` (`SocieteManager.Complement.cs:891-935`) ne vérifie que le statut
de la déclaration cible — **aucune vérification qu'une échéance/affectation n'est pas déjà présente dans
une autre déclaration**. L'INSERT (`LigneDeclarationDelaisPaiementRepository.Script.cs:58-76`) ne porte
aucune contrainte d'unicité. La requête de sélection ne fait aucune jointure d'exclusion contre
`RT_DECLARATIONDELAISPAIEMENTLG`. Résultat : une même échéance peut être réintégrée indéfiniment dans des
déclarations successives, avec un `Depassement` recalculé sans tenir compte de ce qui a déjà été déclaré.

**Règle validée avec le PO** : le `Depassement` déclaré = écart entre la borne actuelle (fin de période, ou
date de rapprochement si payée dans la période) et la **dernière borne déjà déclarée pour cette échéance**
(recherche du max `DDP.DDP_DateFin` parmi les lignes `RT_DECLARATIONDELAISPAIEMENTLG` liées à cet `EC_Id`,
toutes déclarations confondues, y compris antérieures à ce système). Si aucune ligne antérieure n'existe :
- si l'échéance légale est **postérieure ou égale** à la date de mise en route de la société (TASK-128) :
  borne de référence = l'échéance légale elle-même (comportement "première déclaration", cumul normal
  depuis la date légale) ;
- si l'échéance légale est **antérieure** à la date de mise en route **et** sans reprise manuelle saisie
  (TASK-128) : **exclure l'échéance du calcul automatique**, la signaler explicitement dans l'écran de
  contrôle (TASK-134) plutôt que de produire un chiffre silencieusement faux.

Si la borne de référence calculée est **postérieure ou égale** à la borne actuelle (rien de nouveau à
déclarer depuis la dernière fois), **exclure la ligne** de la sélection plutôt que de proposer un
`Depassement` nul ou négatif.

Exemple validé avec le PO : facture déclarée en T1 avec 60 jours de retard (non payée à l'époque) ;
payée réellement avec 75 jours de retard (rapprochement en T2) → T2 doit proposer **15** (75 − 60), pas 75.

### 2. Affectation partielle ignorée dans le bucket "hors période, non payée"
Le legacy porte un `// TODO: verifier les affectations (une affectation peut etre dans la periode`
(l.110, jamais résolu) : le bucket 1 (échéance hors période, non payée) ne regarde que l'état global
`Etat == NonPaye` de l'échéance, sans vérifier si une **affectation partielle** de cette échéance est en
fait tombée dans la période courante. Une facture partiellement payée doit voir sa partie payée traitée
comme le bucket 2 (échéance hors période, payée pendant la période — pour la part affectée) et son solde
restant traité comme le bucket 1 (pour la part non affectée uniquement).

### 3. `Depassement` du bucket 1 structurellement constant
`Depassement = (int)(dateFin - dateDebut.GetMaxFrom(echeance.DatePevuePaiement)).TotalDays` (l.127) —
comme ce bucket filtre déjà `DatePevuePaiement < dateDebut` (l.83), `GetMaxFrom` (= `Max`, vérifié
`DateHelper.cs:36`) renvoie **toujours** `dateDebut` : le retard affiché est donc **constant**
(= longueur de la période) pour toute facture non payée de ce bucket, indépendamment de son ancienneté
réelle. À remplacer par le calcul incrémental décrit au point 1 (borne actuelle = fin de période ;
borne de référence = dernière déclaration ou échéance légale ou garde-fou de mise en route).

## Objectif
```
Entrée  : société, période de déclaration (bornée par une déclaration EnCours existante — jamais une
          plage libre, cf. TASK-134)
Traitement : appliquer les seuils légaux (date/montant), les 3 cas de figure (corrigés), calculer le
             Depassement incrémental par rapport à la dernière borne déjà déclarée (historique
             RT_DECLARATIONDELAISPAIEMENTLG + garde-fou de mise en route TASK-128)
Sortie  : liste des lignes candidates (EC_Id, AF_Id?, Depassement incrémental, EcheanceLegale, statut
          "reprise manuelle requise" le cas échéant), Depassement > 0 uniquement
```

## Périmètre STRICT
- **Inclus** : requête de sélection corrigée (3 anomalies), calcul incrémental, intégration du garde-fou
  TASK-128, lecture seule stricte sur `RT_ECHEANCE`/`RT_AFFECTATION`/`RT_MOUVEMENT`/
  `RT_DECLARATIONDELAISPAIEMENTLG`.
- **Exclu** : écriture (`IntergerLigne`, TASK-132), cycle de vie déclaration (TASK-132), export XML
  (TASK-133), UI (TASK-134).

## Étapes
1. Porter l'algorithme des 3 cas de figure (lecture seule), avec les seuils légaux en dur.
2. Corriger le bucket 1 : scinder part affectée (traitée comme bucket 2) / part non affectée (reste
   bucket 1).
3. Implémenter le calcul incrémental (point 1) pour les 3 buckets — remplace tous les calculs de
   `Depassement` legacy.
4. Brancher le garde-fou de mise en route (TASK-128) : marquer les lignes nécessitant une reprise
   manuelle, jamais les inclure dans le calcul tant que la reprise n'est pas saisie.
5. Tests unitaires hors DB : rejouer le double-comptage évité (exemple T1/T2 ci-dessus), affectation
   partielle correctement scindée, exclusion des lignes sans nouveau jour à déclarer, garde-fou de mise
   en route (avec et sans reprise manuelle).

## Livrables
- Service de sélection corrigé + calcul incrémental.
- Tests unitaires couvrant les 3 anomalies corrigées + le garde-fou de bascule.

## Critères de validation
- Aucune échéance ne peut produire un `Depassement` cumulatif supérieur à son retard réel total (test
  de non-régression explicite sur le scénario T1/T2 du PO).
- Aucune ligne antérieure à la mise en route sans reprise manuelle n'apparaît avec un chiffre calculé.
- Build 0 erreur, tests verts.

## Risques / dépendances
- Dépend de TASK-127 (résolution délai/échéance légale) et TASK-128 (garde-fou de mise en route).
- **Bloquant pour** TASK-132 (cycle de vie, l'intégration consomme ces lignes) et TASK-134 (front).
- Risque principal : si un client a des déclarations passées faites via l'ancien applicatif dans une
  base **différente** de celle utilisée par GRF (pas de partage réel de `RT_DECLARATIONDELAISPAIEMENTLG`),
  l'historique ne serait pas visible et le garde-fou de mise en route (TASK-128) devient la seule
  protection — à vérifier avec le PO client par client avant mise en route réelle.
