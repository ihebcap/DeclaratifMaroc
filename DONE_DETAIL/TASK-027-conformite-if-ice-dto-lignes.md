# TASK-027 — Conformité IF/ICE par ligne dans le DTO (2ᵉ vague de TASK-020, §2)

## Contexte
TASK-020 a été scindée en deux vagues (décision PO 08/07/2026). La **1ʳᵉ vague (§1, n° de règlement
`NumeroRapprochement`) est livrée et clôturée** (voir `DONE.md`, `DONE_DETAIL/TASK-020…md`), débloquant le
regroupement par règlement pour TASK-019. Le **§2 — état de conformité IF/ICE — a été explicitement
reporté** en 2ᵉ vague : cette task le prend en charge.

**Le manque (vérifié dans le code 08/07/2026) :** l'entité `LigneCandidate`
(`Declaration.Application/Entities/WorkflowEntities.cs`) porte déjà les valeurs brutes
`TiersIdentifiantFiscal` et `TiersICE`, mais **aucun état de conformité** n'est calculé ni exposé. Le front
ne peut donc pas alimenter l'interrogation *Conformité IF/ICE* de TASK-019 sans réimplémenter une règle de
son côté (risque de divergence avec ce que l'export DGI accepte réellement).

**Point d'architecture déterminant :** la validation IF/ICE de TASK-011 existe mais **inline dans
l'exporter** (`Declaration.Export.Xml/DeclarationXmlExporter.cs`, lignes ~51-55) — elle lève une
`ApplicationException` (IF = 8 caractères sans espaces, ICE = 15 caractères sans espaces, non vide). Elle
n'est **pas** exposée comme composant réutilisable. La contrainte « source unique de vérité, pas de règle
divergente » impose donc d'**extraire** cette règle en un validateur partagé, puis de brancher l'export
ET la conformité dessus. Sans extraction, on dupliquerait une règle qui divergerait tôt ou tard.

## Périmètre STRICT
- **Uniquement le back** `Declaration.*` : (a) extraire la validation IF/ICE de TASK-011 en composant
  partagé ; (b) ajouter un champ **calculé, lecture seule** d'état de conformité sur `LigneCandidate` ;
  (c) l'exposer dans le DTO `GET /declarations/{id}/lignes`.
- **Réutiliser** la règle IF/ICE de TASK-011 comme **unique source de vérité** — l'exporter XML doit
  consommer le même validateur après extraction (aucune règle divergente ne subsiste).
- **Exclu** : toute modif de la **sélection**/éligibilité, du calcul de ventilation/montants
  (worker OM, orchestrateur, lecteur FGR), du front (TASK-019), du §1 déjà livré. Aucune écriture
  supplémentaire, aucune nouvelle table. Le sens des champs existants reste inchangé.

## Objectif
Exposer, **par ligne**, un état de conformité fournisseur cohérent au caractère près avec ce que l'export
XML DGI accepte/rejette — **sans rien changer d'autre**.

### 1. Extraction du validateur IF/ICE (source unique)
- Extraire la règle inline de `DeclarationXmlExporter` en une fonction/composant pur réutilisable
  (ex. `ValidationIdentiteFiscale` dans une couche partagée `Declaration.Core`/`Declaration.Application`
  selon les dépendances autorisées par la Clean Architecture existante).
- **Rebrancher l'exporter XML** sur ce composant : comportement de rejet **strictement identique** à
  aujourd'hui (mêmes messages/mêmes seuils) — non-régression prouvée par les tests XML existants
  (`Declaration.Export.Xml.Tests`).

### 2. Champ calculé d'état de conformité
- Ajouter sur `LigneCandidate` un état **calculé** (pas de colonne persistée nouvelle si évitable ;
  privilégier une propriété dérivée / un calcul à la projection du DTO), dérivé de
  `TiersIdentifiantFiscal` / `TiersICE` via le validateur du §1.
- Valeurs proposées (à confirmer PO) : `Conforme` / `IfManquant` / `IceManquant` / `FormatInvalide`.
  Un fournisseur dont IF **et** ICE sont valides = `Conforme` ; sinon le motif le plus précis.
- **Lecture seule** : aucune écriture, cohérent avec l'interrogation *Conformité* de TASK-019
  (on n'altère pas la donnée fournisseur, on la qualifie).

### 3. Exposition DTO
- Exposer l'état dans `GET /declarations/{id}/lignes` (par ligne), à côté de `NumeroRapprochement`.
- Aucun champ existant retiré/renommé ; champ ajouté uniquement.

## Contraintes techniques
- **Source unique de vérité** : après extraction, il ne doit exister **qu'une** implémentation de la règle
  IF/ICE, consommée par l'export XML **et** la conformité. Toute divergence = échec de la task.
- **Non-régression export XML** : les tests `Declaration.Export.Xml.Tests` (validation IF 8 / ICE 15 sans
  espaces, rejets) doivent rester **verts sans modification de leurs assertions**.
- **Compat historique** : pour les déclarations déjà figées (IF/ICE vides ou `NULL`→`""` via §1), l'état
  calculé doit se dériver sans crash (ex. vide → `IfManquant`/`IceManquant`), sans recalcul rétroactif
  imposé.
- Aucune régression du contrat existant : champ ajouté, aucun champ retiré/renommé.
- `net10.0`, conventions Clean Architecture GRC_WEB existantes ; tests unitaires du calcul + du branchement.
- **Transparence (TASK-013) intacte** : on qualifie, on n'agrège rien silencieusement, aucune ligne
  masquée.

## Étapes
1. Extraire la validation IF/ICE de `DeclarationXmlExporter` en composant partagé pur ; rebrancher
   l'exporter dessus.
2. Ajouter l'état de conformité **calculé** sur `LigneCandidate` (propriété dérivée / projection DTO),
   basé sur ce composant.
3. Exposer l'état dans le DTO `GET /declarations/{id}/lignes`.
4. Tests : (a) le validateur extrait donne le même verdict que l'ancienne règle inline (table de cas
   IF 8 / ICE 15 / espaces / vide) ; (b) l'export XML rejette exactement comme avant (tests existants
   verts) ; (c) l'état exposé sur une ligne conforme = `Conforme`, sur IF vide = `IfManquant`, etc. ;
   (d) une ligne d'historique IF/ICE vide ne crashe pas.
5. **Cohérence prouvée** : sur un échantillon, un tiers **rejeté par l'export XML** est bien marqué
   non-`Conforme` par le DTO, et un tiers **accepté** est `Conforme`.

## Livrables
- Back : validateur IF/ICE extrait (source unique) + exporter XML rebranché + `LigneCandidate` enrichi
  d'un état calculé + DTO `GET /lignes` exposant l'état.
- Tests unitaires : équivalence validateur extrait ↔ règle d'origine, non-régression export XML,
  mapping des états, tolérance historique.
- `VERIFY/TASK-027_verify.md` : preuve que l'état de conformité par ligne **correspond exactement** à ce
  que l'export XML TASK-011 accepte/rejette (aucune divergence), idéalement illustré sur
  `GR_EMA_DISTRIBUTION` / `SO_Id=1` (à défaut, échantillon avec cas conforme + cas rejeté).

## Critères de validation
- Une **seule** implémentation de la règle IF/ICE subsiste, partagée par l'export et la conformité.
- `GET /lignes` renvoie un **état de conformité par ligne**, cohérent au caractère près avec la
  validation XML TASK-011 (accepté ⇒ `Conforme` ; rejeté ⇒ non-`Conforme`).
- Aucune régression : tests `Declaration.Export.Xml.Tests` verts **sans modifier leurs assertions** ;
  aucun champ existant modifié dans son sens.
- Historique (IF/ICE vides) qualifié sans crash, sans recalcul rétroactif imposé.
- Build + tests verts.

## Risques / dépendances
- **Débloque l'interrogation *Conformité IF/ICE* de TASK-019** (avec §1 déjà livré). Sans TASK-027, cette
  interrogation reste infaisable côté front sans réinventer une règle divergente.
- **Risque n°1 — divergence de règle** : si l'extraction est bâclée (copie au lieu de partage), l'export
  et la conformité dériveront. Le cœur de la task est l'**extraction propre**, pas l'ajout d'un champ.
- **Non-régression XML** : l'exporter est déjà validé/clôturé (TASK-011). Rebrancher sa validation ne doit
  **rien** changer à son comportement observable — vérifier via ses tests existants intacts.
- **Placement Clean Architecture** : choisir la couche du validateur (probable `Declaration.Core` ou
  `Declaration.Application`) de sorte que **et** l'exporter (`Declaration.Export.Xml`) **et** le workflow/DTO
  puissent en dépendre sans inversion de dépendance interdite. Confirmer le graphe de dépendances avant
  de placer.
- **Valeurs de l'énumération d'état** (`Conforme`/`IfManquant`/`IceManquant`/`FormatInvalide`) : premier
  jet, à confirmer/affiner PO — cohérent avec le vocabulaire des alertes existantes du modèle
  (`TIERS_SANS_IF`/`TIERS_SANS_ICE`).
