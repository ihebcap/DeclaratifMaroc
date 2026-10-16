# TASK-238 — V2 : journalisation complète et pack de diagnostic

Status: 🆕 à faire
Priority: HIGH (condition du modèle « peu de support par client »)
Risk: MEDIUM (données sensibles dans les logs)
Module: Declaration.API + Declaration.Compta.* + Declaration.Infrastructure + declaration-tva-web
Dépend de : TASK-225 (dossier, base `DeclaratifMaroc`)

> **Origine :** PO 16/10/2026 — « avoir un log complet pour simplifier le diagnostic » ; objectif commercial : outil facile, peu cher, vendu en volume → un incident doit se diagnostiquer sans intervention sur place.

## Décisions de conception

1. **Journal structuré** (JSON, une ligne par événement) via le mécanisme de logging .NET existant, sans nouvelle dépendance lourde. Champs obligatoires : horodatage UTC, niveau, `CorrelationId` (un par action utilisateur, propagé front → API → lecteur SQL), dossier, période, utilisateur, composant, code événement stable (ex. `LECT-0001`), message lisible en français, durée.
2. **Événements à tracer** (liste minimale) : démarrage/migrations (version appli, version base), test de connexion Sage, chaque lecture comptable (requête nommée, paramètres, **nombre de lignes**, durée), chaque calcul (entrées, règle appliquée, résultat), chaque alerte levée/acquittée, chaque **export** (`ExportId`, empreinte) et **import** (statut par ligne, rejets avec raison), chaque confirmation/déclaration définitive, toute erreur avec pile technique.
3. **Journal d'audit métier distinct du journal technique** : actions utilisateur ayant un effet (validation d'écart, acquittement, décision, déclaration) stockées dans `DeclaratifMaroc` (EF), consultables dans l'application, **non supprimables**.
4. **Aucun secret dans les logs** : mots de passe, chaîne de connexion complète, jetons masqués ; IF/ICE et noms de tiers tronqués ou hachés dans le journal technique (visibles dans l'audit métier).
5. **Rotation et rétention** configurables (taille, jours), défaut raisonnable ; un disque plein ne doit jamais bloquer l'application.
6. **Pack de diagnostic en un clic** : archive `.zip` contenant journaux de la période choisie, version appli/base/format Excel, configuration **sans secrets**, résultat des contrôles de paramétrage (TASK-234), dernières erreurs, liste des exports/imports, et un `LisezMoi` expliquant le contenu. Téléchargeable par l'utilisateur et envoyable au support.
7. **Écran « Journal »** (XGrid, charte xGR) : filtre par dossier, période, niveau, `CorrelationId`, code événement ; clic sur une erreur affiche tout le fil de l'action concernée.
8. **Messages d'erreur utilisateur** : jamais « erreur générique » (leçon TASK-209) ; chaque erreur affichée porte son code événement et son `CorrelationId` pour retrouver la trace.
9. Le journal V1 existant n'est pas modifié.

## Périmètre STRICT

- **Inclus** : infrastructure de journalisation, codes d'événements documentés (table dans la documentation, TASK-240), journal d'audit métier EF, pack de diagnostic, écran Journal, masquage des secrets, tests.
- **Exclu** : télémétrie envoyée à l'extérieur (aucune donnée ne quitte le poste sans action de l'utilisateur), outil de support à distance.

## Livrables

- Journalisation + audit + pack + écran + catalogue des codes d'événements.

## Critères de validation

- Un scénario complet (export → import → déclaration) est reconstituable en suivant un seul `CorrelationId` dans le journal.
- Fouille automatique du journal et du pack : aucun mot de passe ni chaîne de connexion complète (preuve par recherche jointe).
- Pack de diagnostic produit sur un incident provoqué (ex. fichier importé corrompu) : la cause est identifiable sans autre information.
- Disque plein / dossier de logs inaccessible simulé : l'application continue de fonctionner et le signale.
- Build + lint 0 erreur ; **checklist UI** ; preuve par critère datée.

## Files

- `Declaration.API/Program.cs`, `Declaration.Compta.*`, `declaration-tva-web/src/` (lire `DOCS/UI_STANDARDS.md`).
- `TASKS/TASK-209-message-erreur-generique-trompeur-auth.md` (leçon).
