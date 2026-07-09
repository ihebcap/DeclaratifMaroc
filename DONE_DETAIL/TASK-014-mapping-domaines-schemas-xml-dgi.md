# TASK-014 — Cadrage : mapping domaines → formulaires & schémas XML DGI

## Contexte
Le workflow (TASK-013) et l'API (TASK-012) prévoient une **génération multi-fichiers : un XML par
domaine** (décaissement, encaissement, dépense, frais bancaire). Or **un seul** schéma est aujourd'hui
documenté : le **relevé de déductions** décaissement fournisseur (`DeclarationReleveDeduction`, cf.
`MODULE_DECLARATION_TVA.md` §3, issu de la décompilation de `DeclarationTvaEncaissementFileGenerator`).

Les autres domaines ne sont **pas** cadrés :
- **Encaissement (client / TVA facturée / chiffre d'affaires)** relève très probablement d'un **autre
  formulaire DGI** (déclaration du CA / TVA collectée), **schéma XML inconnu**.
- **Dépense** et **frais bancaire** (TVA déductible) sont **probablement** dans le même relevé de
  déductions que le décaissement — **à confirmer** (mêmes balises ? même fichier ou fichier distinct ?).

Tant que ce mapping n'est pas figé, **la génération (TASK-011/012/013) ne peut pas être finalisée** :
on ne sait ni combien de fichiers produire, ni sous quel schéma, ni comment les nommer.

## Périmètre STRICT
- **Uniquement** : cadrage documentaire — identifier, pour chaque domaine, le **formulaire DGI cible**,
  le **schéma XML** (balises, racine, règles), le **nommage** et le **conditionnement** (zip).
- **Exclu** : toute implémentation de génération (reste TASK-011) ; tout calcul (déjà `Declaration.Core`).

## Objectif
Produire un **document de référence** (annexe à `MODULE_DECLARATION_TVA.md`) répondant à :

1. **Table de mapping** :

   | Domaine | Formulaire DGI | Racine XML | Fichier(s) | Statut schéma |
   |---|---|---|---|---|
   | Décaissement fournisseur | Relevé de déductions | `DeclarationReleveDeduction` | 1 | ✅ documenté (§3) |
   | Dépense (avec TVA) | ? (déductions ?) | ? | ? | ⬜ à confirmer |
   | Frais bancaire (avec TVA) | ? (déductions ?) | ? | ? | ⬜ à confirmer |
   | Encaissement client | ? (TVA facturée / CA) | ? | ? | ⛔ inconnu |

2. **Schéma exact par domaine non documenté** : racine, balises `<rd>`/équivalent, types/formats
   (séparateur décimal `.`, dates `yyyy-MM-dd`), en-tête (IF, année, période, régime), règles de
   validation Maroc (IF 8 car., ICE 15 car., sans espaces), lignes exclues (`IsReport`).

3. **Règle de regroupement** : décaissement + dépense + frais = **un seul** relevé de déductions
   (probable) **ou** fichiers séparés ? ⇒ tranche le « un XML par domaine » vs « un XML par formulaire ».

4. **Nommage & conditionnement** : convention de nom par fichier (référence : décaissement =
   `{Numero}-{Exercice}-{M|T}{période}.xml` + `.zip`, UTF-8 sans BOM, pas de `<?xml?>`) déclinée aux
   autres formulaires.

## Sources d'investigation
- **Décompilation GRFN** : chercher un générateur analogue à `DeclarationTvaEncaissementFileGenerator`
  pour l'**encaissement/vente** (nom probable : `...EncaissementClient...` / `...TvaFacturee...` /
  générateur de déclaration CA). ilspy sur les DLL trésorerie déjà utilisées.
- **Documentation DGI Simpl-TVA** : XSD/spécifications officielles du télé-service (relevé de
  déductions **et** déclaration du CA / TVA).
- **Fichiers réels** éventuellement présents chez le client EMA Distribution (exemples de dépôts
  passés) — repères de structure.

## Étapes
1. Confirmer le regroupement dépense/frais/décaissement (même relevé de déductions ?) via
   décompilation + un exemple réel.
2. Localiser le générateur **encaissement/CA** dans GRFN (ou la spec DGI) et en extraire le schéma.
3. Rédiger la table de mapping + les schémas par domaine + nommage/zip.
4. Répercuter les décisions dans TASK-011 (génération), TASK-012 (endpoint génération) et TASK-013
   (nombre de fichiers exposés au front).

## Livrables
- Annexe `MODULE_DECLARATION_TVA.md` : table de mapping + schéma XML par domaine + règles
  nommage/validation/zip.
- Note de répercussion (ce que TASK-011/012/013 doivent ajuster).

## Critères de validation
- Chaque domaine a un **formulaire DGI identifié** et un **schéma XML** (ou une décision explicite
  « pas de fichier pour ce domaine »).
- La question « un fichier par domaine vs un par formulaire » est **tranchée**.
- Le nommage/zip est spécifié pour tous les fichiers produits.
- Aucun trou : si un schéma reste introuvable, il est **signalé comme bloquant nommé** avec la source
  à obtenir.

## Risques / dépendances
- **Bloque la génération** : TASK-011 (export XML), et la partie génération de TASK-012/013 ne peuvent
  être finalisées avant ce cadrage (le front/API peuvent avancer sur le reste du workflow).
- **Dépend d'une source externe** : schéma encaissement peut n'être disponible que dans la doc DGI ou
  la décompilation GRFN → si introuvable, escalade PO pour obtenir la spec Simpl-TVA CA.
- Doc OM/DGI = source unique ; **aucune réutilisation de code/DLL** GRFN (décompilation = lecture pour
  comprendre le format, pas reprise de code — cohérent avec la règle projet).
