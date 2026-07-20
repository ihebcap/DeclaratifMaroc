# VERIFY — TASK-152

Date: 2026-07-20
Agent: worker nocturne (Claude Code)

## BUILD

- Status: N/A — aucun code modifié, conformément au garde-fou explicite de la TASK.

## FICHIERS MODIFIÉS

- Aucun fichier de code modifié.
- Ce fichier VERIFY documente les options, comme demandé par la TASK.

## STATUT : EN ATTENTE D'ARBITRAGE PO — AUCUN DÉVELOPPEMENT DÉMARRÉ

Conformément à l'instruction explicite de la TASK (« cette TASK ne doit pas démarrer le
développement sans confirmer l'approche » et « ne doit pas être auto-approuvée par un worker
autonome — elle requiert un arbitrage produit que l'agent ne doit pas trancher seul »), ce worker
n'a écrit aucun code pour TASK-152. Le point est atteint et documenté ci-dessous, en attente de
décision.

## Rappel du contexte (pour la décision PO)

Le contrôle de collision `DO_Numero` existe déjà et fonctionne, exposé **à la demande** via le
diagnostic TASK-144 (bloc « Contrôle collision » de `DiagnosticModal.tsx`, confirmé toujours
opérationnel — testé indirectement lors de TASK-150, `CollisionDetectee=False` correctement rendu
pour les 5 cas sans collision). Cette TASK porte uniquement sur l'opportunité d'ajouter un signal
**visuel systématique** (avant même que le comptable ne clique « Diagnostiquer ») pour les lignes
dont le `DO_Numero` est partagé par un autre tiers.

## Options à arbitrer (rappel, inchangées depuis la rédaction de la TASK)

1. **Ne rien ajouter de systématique** — le diagnostic à la demande (TASK-144) suffit ; cette TASK
   se fermerait sans code.
2. **Contrôle par lot, en tâche de fond** (calculé une fois par déclaration au chargement du
   checkup, pas par ligne à chaque affichage) alimentant une colonne/badge discret.

## Élément factuel complémentaire apporté par cette nuit de travail (TASK-143/149/150)

Sur les **2 460 `EC_Id`** vérifiés par l'audit TASK-143 (inchangé depuis, aucune nouvelle collision
détectée pendant les diagnostics individuels de TASK-150), **un seul cas de collision `DO_Numero`**
a été trouvé (`FA2600106`, déjà résolu correctement par l'application). Ce taux très faible (1/2460)
est une donnée utile pour arbitrer le rapport coût/bénéfice de l'option 2 (calcul systématique) —
transmis ici pour éclairer la décision, sans trancher à la place du PO.

## Action requise

Le PO doit choisir l'option 1 ou 2 (ou une variante) avant qu'un développeur ne reprenne cette TASK.
Aucune action supplémentaire de ce worker.
