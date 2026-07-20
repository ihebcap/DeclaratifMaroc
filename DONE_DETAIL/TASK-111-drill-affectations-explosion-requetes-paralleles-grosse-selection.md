# TASK-111 — Drill « Détail des lignes » (②) : explosion de requêtes HTTP parallèles sur grosse sélection (`ERR_INSUFFICIENT_RESOURCES`)

## Origine

Signalement via console navigateur (test PO, environnement `DESKTOP-5BFKKEP` / déclaration
`d80b6f0a-c09a-4086-9972-4223e12a0e73`) : ouverture du drill « Détail des lignes » avec une grosse
sélection de règlements déclenche des **centaines** de requêtes `GET
/api/declarations/{id}/lignes?domaine=...&filter={"numeroRapprochement":"..."}` simultanées, dont
la majorité échoue avec `net::ERR_INSUFFICIENT_RESOURCES` (épuisement des connexions HTTP du
navigateur), puis une `AxiosError: Network Error` remontée par `fetchLignesReglement`
(`AffectationsDrill.tsx:73`) et non rattrapée proprement (toast générique « Erreur lors du
chargement des affectations », `AffectationsDrill.tsx:347`, sans indication de combien de
règlements ont réellement échoué).

## Contexte

- Chemin non-`readOnly` de `AffectationsDrill` (drill à la demande depuis ①, TASK-092) :
  `useEffect` (`AffectationsDrill.tsx:369-371`) et `reload()` (`:337-339`) font tous deux :
  ```ts
  await Promise.all(
    selectedRows.map(async r => [r.numeroReglement, await fetchLignesReglement(declarationId, r.numeroReglement, getApiDomaine(r.domaine))] as const)
  );
  ```
  → **une requête HTTP par règlement sélectionné**, toutes lancées en parallèle, **sans aucune
  limite de concurrence** (pas de batching, pas de pool).
- Le log fourni montre l'échec sur une sélection dépassant la centaine de règlements distincts
  (`RF260500xx`/`RC260600xx`/... visibles jusqu'à ~150+ numéros différents) — cohérent avec le cas
  réel déjà documenté dans `TODO.md` (« 152 règlements réels », test 16/07/2026).
- Chrome/Chromium limite à ~6 connexions HTTP/1.1 simultanées par origine ; au-delà, les requêtes
  excédentaires échouent en `ERR_INSUFFICIENT_RESOURCES` plutôt que d'attendre en file — c'est un
  plafond navigateur, pas une erreur serveur (aucune preuve que l'API/`DeclarationsController`
  soit en cause).
- Le chemin `readOnly` (`fetchAllLignesDeclaration`, `:95-115`) n'a **pas** ce défaut : il fait une
  boucle séquentielle bornée à 2 appels (un par domaine), paginée. Seul le chemin non-`readOnly`
  (sélection active, écran ①→drill) souffre de la sérialisation par règlement introduite avec le
  découpage un-fetch-par-règlement.
- Aucune preuve que ce pattern existait avant TASK-092 (« ② Affectations → drill à la demande depuis
  ① ») — avant cette task, le drill se chargeait autrement (à confirmer par relecture git si besoin,
  hors périmètre analyse ici) ; en l'état actuel du code, c'est ce composant qui porte le défaut.

## Périmètre STRICT

- **Inclus** :
  1. Remplacer le `Promise.all` non borné (`AffectationsDrill.tsx:337-339` et `:369-371`) par un
     mécanisme qui **ne dépasse jamais** un nombre raisonnable de requêtes HTTP concurrentes (ex.
     traitement par lots / pool de concurrence bornée), quel que soit le nombre de règlements
     sélectionnés (152 dans le cas réel, potentiellement plus).
  2. Alternative à évaluer par le développeur (moins invasive si le backend le permet déjà) :
     regrouper les `numeroRapprochement` sélectionnés en **un seul** appel `/lignes` avec un filtre
     multi-valeurs, au lieu d'un appel par règlement — à vérifier contre
     `BuildLigneFilterWhere`/`DeclarationRepository.cs` (le filtre `numeroRapprochement` accepte-t-il
     déjà une liste, comme d'autres colonnes de la grille type `source` ? cf. TASK-110). Si oui,
     c'est la correction la plus propre (1 requête au lieu de N) ; si non, retenir l'option 1
     (lots bornés).
  3. Gestion d'erreur honnête si, malgré la borne de concurrence, une requête individuelle échoue
     (règlement isolé en erreur réseau/serveur) : ne pas faire échouer **tout** le drill sur l'échec
     d'un seul règlement — signaler lequel, jamais un message générique masquant lequel a échoué
     (cohérent avec le principe « aucune ligne silencieuse »).
- **Exclu** :
  - Le chemin `readOnly` (`fetchAllLignesDeclaration`), déjà sain (boucle séquentielle bornée par
    domaine), non touché.
  - Toute modification du endpoint `/lignes` côté back au-delà d'un éventuel support multi-valeurs
    du filtre `numeroRapprochement` déjà envisagé en option 2 — pas de nouveau calcul, pas de
    nouvelle route.
  - Le diagnostic « filtre » mentionné dans TASK-110 (composant différent, `DomainGrid`) — sans
    rapport avec ce signalement.

## Objectif

```
Entrée  : Sélection non-readOnly de N règlements (N grand, cas réel observé ~150+) → ouverture ou
          rechargement du drill « Détail des lignes »
Traitement : borner strictement la concurrence des requêtes HTTP émises (ou les regrouper en un
             seul appel serveur si le filtre le permet), sans dégrader le temps de réponse au point
             de rendre l'écran inutilisable
Sortie  : le drill se charge intégralement quel que soit N, sans ERR_INSUFFICIENT_RESOURCES ; en
          cas d'échec isolé, le règlement en cause est identifié à l'utilisateur, pas de message
          générique masquant une perte de données partielle
```

## Livrables

- `AffectationsDrill.tsx` : correction du `Promise.all` non borné (concurrence limitée, ou fusion en
  requête(s) unique(s) si le filtre back le permet).
- Si option filtre multi-valeurs retenue : vérification/ajustement du `filter` `numeroRapprochement`
  côté `DeclarationRepository.cs`/`DeclarationsController.cs` pour accepter une liste (cohérence
  avec le pattern déjà utilisé par d'autres colonnes, cf. TASK-110 §Contexte).
- Test reproduisant une sélection de taille comparable au cas réel (au moins ~150 règlements,
  mockée ou via jeu de données réel) démontrant l'absence d'échec de connexion.
- `VERIFY/TASK-111_verify.md` : build OK, preuve réelle (capture/trace réseau) sur une sélection de
  taille comparable au signalement, montrant l'absence de `ERR_INSUFFICIENT_RESOURCES` et un
  chargement complet des données.

## Critères de validation

- Une sélection de 150+ règlements distincts charge le drill sans aucune requête en échec réseau.
- Aucune régression sur une petite sélection (1 à quelques règlements) — comportement/temps de
  réponse inchangés.
- Aucune régression sur le chemin `readOnly` (relecture post-intégration, TASK-075/092), non touché
  par cette task.
- En cas d'échec réseau isolé et transitoire, l'utilisateur voit quel(s) règlement(s) n'ont pas pu
  être chargés, pas uniquement « Erreur lors du chargement des affectations ».

## Risques / dépendances

- Le choix entre « borner la concurrence » et « fusionner en un seul appel serveur » a un impact de
  conception différent (le second suppose une évolution du contrat `/lignes`, plus large que
  `AffectationsDrill.tsx` seul) — le développeur doit vérifier la faisabilité du filtre multi-valeurs
  avant de s'engager sur cette voie ; à défaut, se limiter à borner la concurrence (moins invasif,
  suffisant pour lever le blocage).
- `AffectationsDrill.tsx` est un fichier déjà dense, récemment modifié par plusieurs tasks
  (TASK-077/078/092/104/105/107/109) — vérifier l'absence de collision avant édition.
- Le volume réel constaté (152 règlements, TODO.md) n'est pas un cas extrême isolé — le PO a déjà
  testé avec ce volume, donc ce n'est pas un edge-case théorique : bloquant pour l'usage réel tant
  que non corrigé.

## NOTES

Découvert via un dump de console navigateur transmis directement (pas de signalement PO formulé en
texte) — trace complète : centaines de `GET .../lignes?domaine=...&filter={"numeroRapprochement":
"..."}` en `net::ERR_INSUFFICIENT_RESOURCES`, faisant suite à une `AxiosError: Network Error` sur
`fetchLignesReglement` (`AffectationsDrill.tsx:73`) appelée depuis le `Promise.all` de l'effet de
chargement (`:370`). Analyse de code uniquement (architecte, lecture directe de
`AffectationsDrill.tsx:67-89` et `:324-387`) — cause racine identifiée avec un haut degré de
confiance (le pattern d'URL du log correspond exactement à `fetchLignesReglement`, et le nombre de
requêtes distinctes correspond à une grosse sélection), mais **non reproduit manuellement** en
environnement réel depuis ce rôle (pas d'accès UI). Implémentation à confier au développeur.
