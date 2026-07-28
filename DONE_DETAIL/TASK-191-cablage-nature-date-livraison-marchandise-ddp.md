# TASK-191 — Câblage réel `natureMarchandise` / `dateLivraisonMarchandise` (export DDP)

## Contexte
Dette documentée dès l'analyse CDC du 19/07/2026 (`CDC-DELAI-PAIEMENT-MAROC.md` §4.2/§4.3/§5.A-4, point
« non bloquant pour un premier livrable ») et reprise telle quelle par TASK-133 : dans le fichier XML de
dépôt Délai de Paiement, `<natureMarchandise>` est toujours vide et `<dateLivraisonMarchandise>` est en
réalité la date de facture (pas une vraie date de livraison) — cf.
`Declaration.Export.Xml\DeclarationDelaiPaiementXmlExporter.cs:114-115` et
`Declaration.Core\DeclarationDelaiPaiementXmlModele.cs:210` (commentaire `// dette assumée`).

Le CDC (§7.1) documente pourtant que le mapping existe déjà et est **configurable par société** dans
`P_SOCIETE` :
- `SO_ColValueNatureMarchandise` → nom de colonne `F_DOCENTETE` (nature marchandise, niveau document).
- `SO_ColValueDateLivraisonMarchandise` → nom de colonne `F_DOCENTETE` (date de livraison, niveau document).

Décision PO (28/07/2026) : requalifier cette dette en tâche planifiée (elle reste **non bloquante** pour
l'existant, mais ne doit plus rester indéfiniment en l'état) — le PO a explicité qu'à terme le module DDP
sera **indépendant**, GRF deviendra **une source parmi d'autres**, et une nouvelle source **grand livre
comptable** sera ajoutée (cohérent avec la roadmap V2 déjà actée côté TVA, `TODO.md` §« ROADMAP — V2 :
source du détail TVA = grand livre comptable », décision PO 24/07/2026). Dans cet horizon, laisser ces 2
champs non câblés maintenant ne fait qu'ajouter à la dette qu'il faudra de toute façon lever avant
d'ouvrir une deuxième source.

**Pattern déjà existant à répliquer, ne pas réinventer** : `GetIdentitesFiscalesTiersAsync` (même fichier,
`DeclarationDelaiPaiementRepository.cs:434-490`) résout déjà un champ société-configurable de la même
famille — `SO_ColValueNumRegistreCommerceFournisseur` — avec whitelist du nom de colonne
(`ValiderNomColonneOptionnelle`, l.499-512) et tolérance explicite à l'absence de configuration (retourne
`''`, jamais une erreur). C'est le contrat à reproduire à l'identique pour les 2 champs de cette tâche.

**Piste de jointure déjà en place** : `SelectionDelaiPaiementRepository.cs` (autour de l.94-98) projette
déjà `DO_Numero`/`DO_Date`/`DO_Reference` au niveau échéance (issu de TASK-187, colonne référence facture)
— cette clé documentaire existe donc déjà dans le flux et est le candidat naturel pour joindre
`F_DOCENTETE`. À confirmer/adapter par le worker, pas à considérer comme acquis sans vérification.

## Objectif
```
Entrée  : société (SO_Id), facture/échéance en cours d'export DDP
Traitement : lire SO_ColValueNatureMarchandise / SO_ColValueDateLivraisonMarchandise (P_SOCIETE, tolérant
             absence, whitelist nom de colonne) ; si configurées, lire la valeur réelle correspondante
             sur F_DOCENTETE (Sage, lecture seule) pour le document de la facture concernée
Sortie  : DeclarationDelaiPaiementXmlModele.NatureMarchandise / DateLivraisonMarchandise reflètent la
          valeur réelle si configurée, sinon comportement actuel inchangé (vide / date facture) — jamais
          d'erreur bloquante liée à l'absence de configuration
```

## Périmètre STRICT
- **Inclus** : lecture des 2 colonnes de config (`P_SOCIETE`), lecture des valeurs réelles sur
  `F_DOCENTETE` (Sage, lecture seule, whitelist du nom de colonne comme le pattern NumRc), câblage dans
  `DeclarationDelaiPaiementXmlModele`/`DeclarationDelaiPaiementXmlExporter`, tests unitaires (config
  absente → comportement actuel inchangé ; config présente → valeur réelle reflétée).
- **Exclu** : toute modification de schéma (aucune nouvelle table/colonne — tout existe déjà) ;
  généralisation multi-source (grand livre comptable) — mentionnée ici comme motivation, pas à
  implémenter dans cette tâche ; écran de configuration de ces colonnes (déjà couvert par l'écran WinForms
  legacy existant, `UcParamDeclarationTvaEncaissement`, hors périmètre GRF).

## Étapes
1. Étendre `GetSocieteInfoAsync` (ou méthode dédiée) pour lire `SO_ColValueNatureMarchandise` /
   `SO_ColValueDateLivraisonMarchandise`, whitelist identique à `ValiderNomColonneOptionnelle`.
2. Identifier/confirmer la clé de jointure facture → `F_DOCENTETE` (piste : `DO_Numero`/`DO_Reference`
   déjà projetés par TASK-187 côté `SelectionDelaiPaiementRepository`) ; lecture Sage en lecture seule,
   par lot (même principe que `GetIdentitesFiscalesTiersAsync`).
2b. **Si l'échéance sélectionnée ne porte pas de piste de jointure exploitable vers `F_DOCENTETE`** (ex.
   `DO_Numero`/`DO_Reference` absent ou ambigu sur le flux DDP), documenter le blocage précis en NOTES du
   VERIFY et **ne pas improviser** de jointure alternative non validée — signaler à l'architecte plutôt
   que de deviner.
3. Câbler le résultat dans `DeclarationDelaiPaiementXmlModele` (remplacer le hardcode vide de
   `natureMarchandise` et le fallback `dateEmission` de `DateLivraisonMarchandise` par la valeur réelle
   quand disponible, fallback identique à aujourd'hui sinon).
4. Tests unitaires : société sans colonne configurée (comportement actuel inchangé, non-régression),
   société avec colonne configurée mais valeur absente sur le document, société avec valeur réelle
   présente (câblage effectif).

## Livrables
- Lecture des 2 colonnes de config + valeurs réelles Sage (repository).
- Câblage modèle XML.
- Tests unitaires (3 cas ci-dessus).

## Critères de validation
- Aucune régression sur le comportement actuel quand la config est absente (legacy émettait vide/date
  facture — doit continuer à le faire dans ce cas précis).
- Quand la config est présente et la valeur existe sur `F_DOCENTETE`, le XML généré reflète la valeur
  réelle, pas le fallback.
- Aucune modification de schéma. Build 0 erreur, tests verts.

## Risques / dépendances
- Dépend de la fiabilité de la clé de jointure vers `F_DOCENTETE` (à vérifier en premier, cf. étape 2/2b)
  — si aucune piste fiable n'existe sur le flux DDP actuel, cette tâche peut rester bloquée sur ce seul
  point ; ne pas la fermer en dégradant silencieusement le contrat (ex. jointure approximative par date).
- Aucun impact sur le contrôle bloquant IF/ICE (TASK-132) — ces 2 champs n'en font pas partie.
- Motivation long terme (indépendance du module, multi-source, grand livre comptable) documentée en
  Contexte à titre d'information — ne justifie aucune abstraction anticipée dans le périmètre de cette
  tâche.
