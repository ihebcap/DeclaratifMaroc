# TASK-130 — Convention de délai de paiement par tiers (front)

## Contexte
Écran front pour la sous-fonctionnalité Convention (CDC §3.2, §6), consommant le back livré en
TASK-129. React 19 / Vite / TS, cohérent avec `declaration-tva-web` (densité "0 espace perdu", pas de
panneau latéral, mêmes composants de grille que le reste de l'app — `DomainGrid`/`ExcelFilter`/
`ColumnSelector` déjà disponibles, TASK-068).

## Objectif
```
Entrée  : sélection société (contexte déjà géré par l'app)
Écran   : liste des conventions (CRUD Convention/Facture) + action "Terminer" (clôture anticipée)
Sortie  : convention créée/modifiée/clôturée via l'API TASK-129
```

## Périmètre STRICT
- **Inclus** : écran liste (grille dense, filtres/tri, sélecteur de colonnes réutilisé), formulaire de
  création (type Convention/Facture, upload PDF optionnel), action "Terminer" une convention Convention.
- **Exclu** : branchement au menu (TASK-136), résolution du délai (déjà côté back), écran de sélection
  DDP (TASK-134).

## Étapes
1. Grille liste conventions : colonnes tiers, type (Convention/Facture), dates ou n° facture, délai
   (jours), statut de validité (Convention : `DateFin ≥ aujourd'hui` ; Facture : toujours valide si
   rattachée), pièce jointe (lien de téléchargement si présente, jamais bloquant si absente).
2. Formulaire de création : bascule des champs visibles selon le type choisi (Convention → dates ;
   Facture → sélection facture non payée du tiers), plafond 180j affiché en validation immédiate (pas
   d'attente serveur pour ce contrôle simple), upload PDF optionnel.
3. Message d'erreur explicite en cas de chevauchement détecté par le back (TASK-129) — citer la
   convention en conflit (numéro + période), pas un message générique.
4. Action "Terminer" (clôture anticipée) : modale de saisie de la nouvelle date de fin, bornée
   `[DateDebut, DateFin actuelle]` côté UI en plus du contrôle back.
5. Build tsc+vite / oxlint 0 erreur ; test e2e Playwright du parcours création + chevauchement rejeté +
   clôture anticipée.

## Livrables
- Écran liste + formulaire + action Terminer.
- Test e2e Playwright.

## Critères de validation
- Parcours complet testé (création Convention, création Facture, rejet chevauchement avec message
  explicite, clôture anticipée) sur données réelles ou fixture.
- Build 0 erreur, e2e vert.

## Risques / dépendances
- Dépend de TASK-129 (API).
- Valeur par défaut de la date de fin proposée à la création (cf. réserve TASK-129) — à trancher avant
  de coder le formulaire, sinon reproduction de l'incohérence legacy §4.7 par accident.
