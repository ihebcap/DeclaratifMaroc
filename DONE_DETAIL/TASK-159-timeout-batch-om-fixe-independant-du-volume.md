# TASK-159 — Timeout batch OM fixe (300s) indépendant du volume : échec systématique au premier peuplement du cache sur un grand nombre de pièces

## Contexte
Signalement client (23/07/2026, ~15:11-15:16), même déclaration que l'incident TASK-156 :
```
2026-07-23 15:11:34 [VALO] batch OM : 361 pièce(s) EC_Type=0 à lire.
2026-07-23 15:16:38 [VALO] batch OM en exception, repli individuel : Timeout lors de l'exécution du worker en mode batch.
```
Écart exact : 5 min 04 s — correspond au timeout batch fixe de `WorkerInvoker.cs:101`
(`WaitForExit(300000)`), pas à un blocage anormal.

**TASK-156 déjà écartée comme cause** :
1. Build vérifié directement dans le code source : le correctif B (cache servi indépendamment du
   token de paiement) est bien présent
   ([OrchestrateurDeclaration.cs:409-426](../Declaration.Orchestration/OrchestrateurDeclaration.cs#L409-L426),
   commentaire explicite référençant TASK-156).
2. Contention explicitement écartée par un test contrôlé du PO : logs purgés, mise à jour appliquée,
   **un seul calcul lancé** depuis la déclaration (aucun appel concurrent). Le timeout se reproduit
   quand même, seul, sans compétition avec un autre appelant.

**Cause racine (confirmée en code, pas supposée)** :
1. Le timeout du batch OM est une **constante fixe** (`WorkerInvoker.cs:101`,
   `bool exited = process.WaitForExit(300000);`), totalement indépendante du nombre de pièces
   demandées. Les lectures OM en mode batch sont **séquentielles** côté worker (un seul thread STA
   COM, `SageTaxReader.Core/SageTaxReaderService.cs`) — le temps total croît donc linéairement avec le
   nombre de pièces. Un incident antérieur à 165 pièces n'avait pas timeout ; celui-ci à 361 pièces
   dépasse les 300s disponibles. Rien n'indique une régression : c'est un volume qui dépasse la
   capacité de la fenêtre fixe.
2. Quand le batch time-out, [OrchestrateurDeclaration.cs:113-117](../Declaration.Orchestration/OrchestrateurDeclaration.cs#L113-L117)
   capture l'exception mais **ne récupère aucun résultat partiel** : le process est tué
   (`process.Kill()`, `WorkerInvoker.cs:105`) avant d'avoir pu renvoyer quoi que ce soit — protocole
   stdin/stdout tout-ou-rien (`ReadToEndAsync` + désérialisation d'un seul JSON final). Tout travail
   déjà effectué par le worker au moment du kill est perdu.
3. Le véritable repli individuel ne se déclenche qu'ensuite, **pièce par pièce, à la demande**, dans
   `resoudreFactureBrute`
   ([OrchestrateurDeclaration.cs:258-272](../Declaration.Orchestration/OrchestrateurDeclaration.cs#L258-L272)) :
   jusqu'à 361 lancements de process séparés **en séquence stricte**, chacun avec son propre coût de
   démarrage process + initialisation COM. C'est structurellement plus lent que le batch qui vient
   d'échouer, pas un filet de sécurité efficace pour un problème de volume.

**Décision PO (23/07/2026)** : rester sur un appel **synchrone** côté déclaration (pas de passage à un
traitement asynchrone/arrière-plan pour cette task) — la cible est un timeout qui s'adapte réellement
au volume, avec le meilleur repli possible dans cette contrainte.

## Objectif
```
Entrée  : un batch OM de grand volume (ex. 361 pièces, jamais encore en cache) time-out
          systématiquement au bout de 300s fixes, puis retombe sur un repli individuel séquentiel
          encore plus lent (jusqu'à N lancements de process), rendant la valorisation quasi
          inutilisable au premier peuplement du cache pour une déclaration à fort volume
Traitement : dimensionner le timeout batch sur une mesure réelle du coût par pièce (pas une valeur
             supposée), tenter de sauver les résultats déjà obtenus par le worker avant un kill, et
             ne recourir au repli individuel que pour les pièces réellement manquantes — en parallèle
             borné si Sage le permet (à vérifier, pas à supposer)
Sortie : la déclaration ayant motivé ce signalement (361 pièces EC_Type=0, même soId) se valorise
         sans exception de timeout, dans un délai mesuré et documenté, sans perte de pièce silencieuse
```

## Périmètre STRICT
- **Inclus** :
  1. `Declaration.Orchestration/WorkerInvoker.cs` — remplacer la constante `300000` par un timeout
     calculé à partir du nombre de requêtes du batch (formule dérivée d'une mesure réelle, cf. Étapes).
  2. `SageTaxReader.Core/SageTaxReaderService.cs` — étudier la possibilité de retourner les résultats
     déjà lus au moment d'une interruption (actuellement le mécanisme interne « Lot interrompu »
     dégrade déjà gracieusement en cas de blocage sur UNE pièce ; vérifier s'il peut aussi servir à
     renvoyer un résultat partiel exploitable avant que le process ne soit tué depuis l'extérieur).
  3. `Declaration.Orchestration/OrchestrateurDeclaration.cs` — exploiter tout résultat partiel
     récupérable ; ne lancer le repli individuel que pour les pièces confirmées manquantes (le code
     de détection existe déjà, lignes 104-111) ; si l'étape 2 des Étapes ci-dessous confirme que Sage
     tolère plusieurs process concurrents, paralléliser ce repli avec un degré de parallélisme borné
     déterminé par cette vérification (pas une valeur arbitraire).
- **Exclus / hors périmètre** :
  - Toute logique de verrou de contention ou de cache par token de paiement (TASK-156, déjà terminée,
    ne pas y retoucher).
  - Passage à un traitement asynchrone/arrière-plan (tranché par le PO : hors périmètre de cette task).
  - `Declaration.Setup`/WinSW (TASK-157, chantier distinct).

## Étapes
1. **Mesurer le coût réel de lecture d'une pièce OM** sur un environnement représentatif du client
   (plusieurs échantillons, overhead COM inclus) — produire un chiffre mesuré, jamais une estimation.
   Cette mesure sert de base à la formule de timeout adaptatif ; ne pas la deviner a priori.
2. **Vérifier si Sage tolère plusieurs process `SageTaxReader.Console.exe` concurrents** contre la
   même base (lancer 2-3 instances en parallèle, vérifier l'absence d'erreur COM ou de limite de
   licence/session Sage) — condition nécessaire avant de paralléliser le repli individuel à l'étape 5.
   Documenter le résultat quel qu'il soit (autorisé avec quel degré, ou refusé).
3. Investiguer pourquoi la dégradation gracieuse interne du worker (« Lot interrompu », exit 0,
   `SageTaxReaderService.cs`, timeout de 30s par pièce) ne s'est pas manifestée avant les 300s externes
   sur ce cas réel — vérifier si le cumul de 361 pièces à un rythme normal dépasse simplement la
   fenêtre, ou s'il y a un blocage plus profond sur une pièce précise qu'il faudrait traiter
   séparément.
4. Implémenter le timeout batch adaptatif dans `WorkerInvoker.cs` (formule = marge fixe + N pièces ×
   budget mesuré à l'étape 1), remplaçant la constante 300000.
5. Adapter le repli individuel pour ne traiter que les pièces manquantes après un batch partiel/en
   échec, avec parallélisme borné SI l'étape 2 le permet ; sinon repli séquentiel inchangé mais le
   timeout adaptatif de l'étape 4 doit suffire à éviter l'échec initial pour ce volume connu.
6. Rejouer le scénario exact du signalement (361 pièces EC_Type=0, même déclaration/soId si accessible,
   sinon volume équivalent reconstitué) — mesurer et documenter le temps total avant/après.
7. Tests couvrant : volume dépassant l'ancien seuil fixe de 300s, absence de perte de pièce (aucune
   facture omise silencieusement), comportement correct si Sage refuse la parallélisation (repli
   séquentiel toujours fonctionnel).
8. Build (`Declaration.Orchestration`, `SageTaxReader.Core`) + suite `Declaration.Orchestration.Tests` :
   0 erreur, 100% vert.

## Livrables
- Code modifié dans le périmètre ci-dessus.
- `VERIFY/TASK-159_verify.md` avec :
  - la mesure réelle du coût par pièce (étape 1) et la formule de timeout qui en découle (jamais un
    nombre inventé) ;
  - le résultat de la vérification de parallélisation Sage (étape 2), confirmé ou infirmé ;
  - la preuve du rejeu du scénario réel (étape 6) : log avant/après, durée totale ;
  - résultat des tests et du build.

## Critères de validation
- La déclaration ayant motivé l'incident (361 pièces EC_Type=0) se valorise sans exception
  `Timeout lors de l'exécution du worker en mode batch`, dans un délai mesuré et documenté.
- Aucune pièce n'est perdue silencieusement (le log de détection des pièces manquantes,
  lignes 104-111, reste actif et exploité).
- Aucune régression sur TASK-156 (verrou de contention et cache indépendant du token de paiement
  toujours actifs et non modifiés).
- Build + tests `Declaration.Orchestration.Tests` : 0 erreur.

## Risques / dépendances
- **Dépend de TASK-156** (terminée) — ce correctif s'applique après elle, sur un cas qu'elle ne
  couvrait pas (volume, pas contention).
- **La parallélisation du repli individuel est incertaine** : si Sage refuse plusieurs process
  concurrents (licence, session unique), le repli reste séquentiel — le timeout adaptatif seul doit
  alors suffire pour le volume connu (361 pièces), mais **ne garantit rien pour un volume encore plus
  grand à l'avenir**. Signaler explicitement cette limite dans le VERIFY, ne pas la présenter comme
  une solution illimitée.
- **Dépendance à une mesure réelle** (étape 1) : toute formule de timeout basée sur un chiffre supposé
  plutôt que mesuré serait une nouvelle version du même bug avec une constante différente — ne pas
  livrer sans cette mesure.
