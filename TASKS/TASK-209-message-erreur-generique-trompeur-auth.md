# TASK-209 — `Auth.tsx` : message « Serveur injoignable » affiché pour toute erreur, y compris 500

Status: 🆕 à faire
Priority: LOW (n'affecte que la qualité du diagnostic affiché, aucune perte de fonctionnalité)
Module: declaration-tva-web

> **Origine :** suivi distinct signalé lors de la résolution de TASK-123 (19/07/2026, incident
> `Microsoft.Data.SqlClient`/déploiement) — non tracé en task séparée à l'époque. Ce message trompeur
> avait retardé le diagnostic réel de TASK-123. Confirmé toujours présent le 09/08/2026.

## Cause identifiée (preuve de code)

[declaration-tva-web/src/Auth.tsx](../declaration-tva-web/src/Auth.tsx) affiche le même texte fixe
« Serveur injoignable — vérifiez que l'API est démarrée » dans **deux** blocs `catch` distincts, sans
distinguer la nature réelle de l'erreur :

- Ligne 32 — chargement de la liste des sociétés (`GET /societes`) : tout `catch` (réseau **ou** 500
  applicatif) affiche ce message.
- Ligne 69 — soumission du login (`POST /auth/login`) : le code distingue déjà `err?.response` présent
  (ligne 66-67, affiche le message d'erreur réel du serveur) du cas sans réponse (ligne 68-69, message
  fixe) — mais **le bloc `err?.response` présent avec un statut 500 générique** (pas d'identifiants
  incorrects, une vraie panne serveur) retombe quand même sur un message "Identifiants incorrects" par
  défaut (ligne 67, `|| 'Identifiants incorrects'`), tout aussi trompeur dans ce cas précis.

**Effet observable** : une erreur 500 côté API (panne réelle du serveur, pas un problème de démarrage)
s'affiche comme si l'API n'était pas démarrée, ou comme un problème d'identifiants — aucun des deux
n'oriente vers la vraie cause.

## Périmètre STRICT

- **Inclus** :
  1. Distinguer, dans les deux blocs `catch`, au moins 3 cas : pas de réponse réseau du tout (API
     réellement injoignable, message actuel conservé) ; réponse avec statut 5xx (message dédié, ex.
     « Erreur serveur (500) — contactez le support » ou équivalent) ; réponse avec statut 4xx sur le
     login (message métier existant, ex. identifiants incorrects).
  2. Conserver le message métier existant pour les cas déjà correctement gérés (ex. 401/403 sur login).
- **Exclu** :
  - Pas de refonte du composant `Auth.tsx` au-delà de la gestion d'erreur des deux blocs `catch`.
  - Pas de changement du format de réponse d'erreur de l'API (`err.response.data?.Message`), uniquement
    la façon dont le front l'interprète/l'affiche.

## Livrables

- Les deux blocs `catch` de `Auth.tsx` affichent un message distinct selon le statut HTTP reçu (ou son
  absence).

## Critères de validation

- Simuler (ou observer) une 500 sur `GET /societes` et sur `POST /auth/login` : le message affiché ne
  doit plus être « vérifiez que l'API est démarrée » ni « Identifiants incorrects ».
- Simuler une vraie coupure réseau (API arrêtée) : le message actuel (« Serveur injoignable ») reste
  affiché, sans régression.
- Identifiants réellement incorrects (401 login) : message métier inchangé.

## Files

- [declaration-tva-web/src/Auth.tsx](../declaration-tva-web/src/Auth.tsx) (lignes 29-34 et 65-70).
