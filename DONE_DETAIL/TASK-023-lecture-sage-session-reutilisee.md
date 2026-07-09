# TASK-023 — Lecture Sage en session réutilisée (`EC_Type=0`) + cache des invariants

## Contexte
Après TASK-022, seules les factures **Sage (`EC_Type = 0`)** restent obligées de passer par l'OM (leur
détail TVA n'existe pas en SQL). Le coût n'est **pas** la lecture elle-même mais le fait que
`SageTaxReaderService` **ouvre ET ferme une session COM Sage complète (`BSCIALApplication100c.Open()`) à
chaque facture** (voir `SageTaxReader/SageTaxReader.Core/SageTaxReaderService.cs`, `LireFactureAchat`
/`LireFactureVente` : `new BSCIALApplication100c()` → `OpenSession` → `Close` à chaque appel).
L'`Open()` est le coût dominant ; `ReadPiece` est bien plus léger.

## Périmètre STRICT
- **`SageTaxReader.Core` + branchement orchestrateur.** Ajouter un **mode lot** : ouvrir la session **une
  fois**, lire toutes les factures Sage en boucle (`ReadPiece`), fermer **une fois**.
- **Cacher les invariants** actuellement relus à chaque facture : devise société (`ObtenirDeviseSociete`,
  aujourd'hui appelée à chaque `ExtraireTaxes`) et les codes taxe (`FactoryTaxe.ReadCode`).
- **Exclu** : cache/matérialisation persistante (TASK-024), FGR/routing (TASK-022), front.
- **❌ Interdit** : `Parallel.ForEach` sur l'OM — multiplie les sessions COM (licences, verrous,
  `TimeoutException` si un document est ouvert dans Sage). La réutilisation d'**une** session bat la
  parallélisation ici (décision d'architecture).

## Positionnement / architecture
- Nouvelle API batch dans `SageTaxReaderService`, ex :
  `IReadOnlyDictionary<string, DocumentTaxesInfo> LireFactures(IEnumerable<(string piece, SensAffectation sens)>)`,
  exécutée sur **un seul thread STA** gardé vivant pour tout le lot (le pattern `RunOnStaThread` actuel est
  réutilisé mais **hisse** l'ouverture/fermeture de session hors de la boucle).
- L'orchestrateur regroupe les affectations `EC_Type=0` et appelle le batch (au lieu d'un appel par ligne).

## Objectif
```
Entrée : lot de factures Sage (pièces) à lire
Traitement : 1 Open() → boucle ReadPiece (devise + codes taxe cachés) → 1 Close()
Sortie  : ventilation par pièce, identique à la lecture unitaire actuelle
```
Ordre de grandeur visé : `2085 × ~1 s ≈ 35 min` → `1 × Open + N × ~0,1 s` (≈ 10×), N réduit d'autant que
TASK-022 a sorti les FGR de l'OM.

## Contraintes techniques
- **Résultat identique** à la lecture unitaire (mêmes montants, même `DocumentTaxesInfo`) — non-régression
  stricte vs TASK-002/007.
- **Robustesse** : conserver le **timeout par pièce** ; si une pièce plante (doc ouvert dans Sage,
  introuvable…), **isoler l'échec sur cette pièce** (motif/alerte) **sans perdre le reste du lot**.
- Libération COM propre (`Marshal.ReleaseComObject`) des objets par pièce ; session libérée en fin de lot
  même en cas d'exception (finally).
- `net8.0-windows` (contrainte COM Sage existante du projet SageTaxReader) ; pas de changement de la
  frontière d'assembly.

## Étapes
1. Extraire `OpenSession`/`CloseAndRelease` hors de la boucle : nouvelle méthode batch tenant la session.
2. Cacher devise société (1 lecture/lot) et codes taxe (`FactoryTaxe` mémoïsé par code).
3. Isolation d'erreur par pièce (une pièce KO → entrée en erreur, lot poursuivi).
4. Brancher l'orchestrateur pour appeler le batch sur le sous-ensemble `EC_Type=0`.
5. Mesurer temps avant/après sur un lot réel de factures Sage → VERIFY.

## Livrables
- API batch `SageTaxReaderService` (session réutilisée + invariants cachés + isolation d'erreur).
- Orchestrateur branché sur le batch pour les Type 0.
- `VERIFY/TASK-023_verify.md` : mêmes montants qu'en unitaire sur un échantillon, **temps avant/après
  mesuré**, preuve qu'une pièce en erreur n'interrompt pas le lot.

## Critères de validation
- Ventilations Sage **identiques** à la lecture unitaire (aucune régression de montants).
- **Une seule** ouverture de session pour tout le lot (prouvé par log/trace).
- Gain de temps mesuré et consigné.
- Une pièce en échec est isolée (motif) sans faire tomber le lot.
- Build + tests verts.

## Risques / dépendances
- **Ordre** : après TASK-022 (pour ne traiter que le résiduel Type 0), mais **indépendante** techniquement
  (peut être développée en parallèle).
- **STA / COM long-vécu** : une session tenue longtemps peut être sensible aux verrous Sage → conserver le
  timeout par pièce et la libération propre.
- Ne remplace pas TASK-024 (cache persistant) ; c'est l'optimisation **du chemin OM lui-même**, pas sa
  suppression. Sert aussi d'**accélérateur au premier import** de TASK-024.
