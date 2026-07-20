# TASK-152 — Désambiguïser l'affichage du tiers en cas de collision `DO_Numero` entre deux tiers (préventif)

Status: 🆕 à faire
Priority: LOW
Risk: LOW (affichage préventif, aucune correction de données)
Module: declaration-tva-web / Declaration.API

> **Origine :** TASK-143 — anomalie confirmée #3 : le contrôle « numéro de pièce dupliqué entre
> tiers » (nouveau contrôle TASK-143, désormais vivant via TASK-144) a trouvé 1 cas réel
> (`FA2600106`, tiers CT_No=166 avec document réel vs CT_No=188 orphelin) sur 2 460 `EC_Id`
> vérifiés. Ce cas précis n'a pas causé d'anomalie visible (la déclaration référence la bonne
> échéance), mais le risque de confusion existe structurellement tant que rien n'alerte
> visuellement l'utilisateur en dehors du diagnostic à la demande (TASK-144).

## Objectif

Le contrôle de collision est désormais exposé à la demande via le diagnostic TASK-144
(`DiagnosticModal`, bloc 3 "Contrôle collision"). Cette TASK est **préventive** : évaluer si un
signal visuel (badge discret, ex. sur la colonne Tiers de `DomainGrid`/`VerifierIntegrerPanel`)
doit être ajouté pour les lignes dont le `DO_Numero` est partagé par un autre tiers, **avant même**
qu'une anomalie ne se manifeste — pour que le comptable soit informé du risque sans avoir à cliquer
"Diagnostiquer" ligne par ligne.

**Décision d'implémentation à valider avec le PO avant de coder** (cette TASK ne doit pas démarrer
le développement sans confirmer l'approche) : un signal systématique impliquerait de calculer la
collision pour **toutes** les lignes affichées (coût cross-base Sage à chaque chargement d'écran),
ce qui contredit le garde-fou de TASK-144 (« lecture seule, coûteuse, à la demande, pas pour les
5 182 lignes à chaque affichage »). Options à arbitrer :
1. Ne rien ajouter de systématique — le diagnostic à la demande (TASK-144) suffit, cette TASK est
   annulée/fermée sans code.
2. Un contrôle **par lot, en tâche de fond** (ex. calculé une fois par déclaration lors du
   chargement du checkup, pas par ligne à chaque affichage), qui alimente une colonne/badge discret.

## Garde-fous

- Ne pas recalculer la collision pour chaque ligne à chaque affichage de grille (coût cross-base
  Sage prohibitif à l'échelle de 5 182 lignes).
- Ne jamais affirmer un lien de causalité entre la collision et une anomalie visible (même règle que
  TASK-144) — le signal, s'il est ajouté, doit être neutre ("info", pas "erreur").

## Files

- À déterminer selon l'option retenue avec le PO.

## Validation

- [ ] Décision explicite du PO obtenue et documentée avant tout développement (option 1 = fermeture
      sans code, option 2 = développement selon le détail arbitré).
- [ ] Si option 2 : aucune dégradation de performance mesurable au chargement de l'écran ② Vérifier
      & Intégrer.

## Dépendances / risques

- Dépend de TASK-144 (le contrôle de collision existant, déjà livré).
- **Cette TASK ne doit pas être auto-approuvée par un worker autonome** — elle requiert un arbitrage
  produit (systématique vs à la demande) qu'un agent ne doit pas trancher seul. Si le worker de nuit
  l'atteint sans réponse du PO disponible, il doit la laisser **en attente** avec les options
  documentées, pas choisir seul.
