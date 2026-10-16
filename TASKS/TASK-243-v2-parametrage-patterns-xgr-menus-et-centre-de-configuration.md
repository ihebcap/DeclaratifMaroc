# TASK-243 — V2 : paramétrage selon la logique xGR (organisation des menus, listes + drawer, formulaires à onglets, centre de configuration)

Status: 🆕 à faire
Priority: HIGH (structure tous les écrans de paramétrage V2 : 225, 234, 238, 239, 241, 242)
Risk: MEDIUM (cohérence transversale, risque de refaire les écrans plusieurs fois)
Module: declaration-tva-web (`src/components/administration/`, menu, écrans de paramétrage) + Declaration.API
Dépend de : TASK-231 (charte), TASK-232 (XGrid pour les grilles métier) ; consommée par 225, 234, 238, 239, 241, 242

> **Origine :** PO 16/10/2026 — « pour la partie paramétrage, on doit faire la même logique que xGR, surtout l'organisation des menus ». Référence normative : `D:\_vibe\xGR\xGR\Necessaire\ADMIN_PATTERNS.md` (patterns validés 03/06/2026) et `Tresorerie.Vue/src/layout/appMenuModel.js`, `AppMenu.vue`, `AppMenuItem.vue`. **À relire en entier avant tout code.**

## Règle de conformité

Mêmes exigences que TASK-231 : **charte APBS suivie à la lettre** (même entreprise, PO 16/10/2026). Les patterns `ADMIN_PATTERNS.md`, `ACTION_PATTERNS.md`, `FILTER_PATTERNS.md` sont repris sans interprétation ; tout écart est listé, justifié et validé par le PO ; cas non couvert → question au PO.

## Décisions de conception

1. **Pattern Type 1 — Liste + tiroir** pour les référentiels de faible volume (dossiers, utilisateurs, droits par dossier, règles de plausibilité, codes activité mémorisés…) : composants React `AdminConfigTable` (table légère, **pas XGrid**, comme dans xGR : « ne jamais utiliser XGrid dans les écrans admin ») et `AdminFormDrawer` (tiroir droit ~460 px, modes `view` / `edit` / `create`, non fermable par accident en édition, boutons Enregistrer/Annuler, suppression optionnelle). Ligne en sommeil grisée, ligne sélectionnée avec barre verte à gauche.
2. **Pattern Type 2 — Formulaire à enregistrement unique** pour les options d'un dossier (connexion Sage, colonnes IF/ICE, préfixe des comptes de trésorerie, seuils) : boutons **Modifier / Enregistrer / Annuler dans l'en-tête** (toujours visibles), motif `draft` (copie profonde, annulation = on jette), **onglets obligatoires dès 3 sections**, bandeau « Mode modification ».
3. **Aide sur chaque champ** : icône ⓘ avec infobulle (libellé, description, dépendances) alimentée par une **base de connaissance JSON unique** (équivalent de `configPriorities.json` de xGR : domaines, champs, dépendances, règles de statut). La même source nourrit la documentation (TASK-240) : une seule vérité, pas de texte dupliqué.
4. **Centre de configuration / diagnostic** (équivalent du health check xGR) : liste des domaines de paramétrage avec statut **bloquant / socle / à vérifier / optionnel / configuré** (rouge / orange / ambre / gris / vert), évalué par des règles JSON. Il intègre le diagnostic de dossier (TASK-225 test de connexion, TASK-234 alertes de trésorerie) et sert d'**assistant d'onboarding** : un client neuf voit exactement ce qu'il reste à configurer.
5. **Organisation des menus à la xGR** : sidebar noire avec sections par **verbe d'action** (comme Piloter / Encaisser / Payer / Trésorerie), entrée racine « Accueil », libellés courts, une icône par entrée, sections repliables, entrées verrouillées visibles si le droit manque (TASK-242). Proposition de départ, **à valider par le PO avant code** :
   - **Accueil** (état du dossier actif, alertes, centre de configuration en résumé)
   - **Déclarer** : Exporter · Importer et contrôler · Déclarations · Historique des exports/imports
   - **Contrôler** : Alertes · Écarts HT × taux · Règlements non rapprochés · Comptes de taxe sans tiers
   - **Suivre** : Journal · Audit · Pack de diagnostic
   - **Paramétrage** : Dossiers · Utilisateurs et droits · Options du dossier · Règles de contrôle · Mémoire du dossier · Centre de configuration
   Tous les écrans sont enregistrés dans **un modèle de menu unique** (équivalent de `appMenuModel.js`) qui alimente aussi la recherche de menu.
6. **Le Paramétrage est une zone à part du menu**, réservée par rôle (`Administrateur`, TASK-242) ; les écrans de la V1 gardent leur place tant que TASK-233 n'a pas migré.
7. Tous les écrans s'appuient sur la charte xGR (TASK-231) : tokens, boutons, tags de statut, dark mode.
8. Sont à réaliser **ici**, une seule fois, et à consommer ailleurs : `AdminConfigTable`, `AdminFormDrawer`, `ConfigField` (ligne libellé + ⓘ + valeur/champ), moteur de règles de statut, modèle de menu. Les TASKs 225, 234, 238, 239, 241, 242 ne recréent aucun de ces composants.

## Périmètre STRICT

- **Inclus** : composants d'administration, base de connaissance JSON + moteur de statuts, centre de configuration, modèle de menu V2 et sidebar restructurée, deux écrans pilotes complets (un par pattern : liste des dossiers = Type 1 ; options du dossier = Type 2) servant de référence, documentation du pattern (`DOCS/V2/PATTERNS_ADMIN_REACT.md`), mise à jour de `DOCS/UI_STANDARDS.md`.
- **Exclu** : la logique métier de chaque domaine (portée par les TASKs concernées), l'assistant IA de xGR (`ConfigAssistantDrawer`, hors projet V2 sans IA), la migration des écrans V1.

## Livrables

- Composants + base de connaissance + moteur de statuts + centre de configuration + menu + 2 écrans pilotes + documentation du pattern.

## Critères de validation

- Les deux écrans pilotes suivent le pattern xGR point par point (comparaison côte à côte avec `ModesReglementGroupe.vue` et `OptionsSociete.vue`, captures jointes).
- Chaque champ des deux écrans a son infobulle issue de la base de connaissance ; supprimer une entrée de la base de connaissance fait échouer un test (pas de champ sans aide).
- Le centre de configuration passe un dossier neuf de « bloquant » à « configuré » au fur et à mesure du paramétrage (scénario rejoué, captures).
- Le menu s'affiche selon le rôle : un `Lecteur` ne voit pas la zone Paramétrage (testé).
- `npm run lint` + `npm run build` 0 erreur ; **checklist UI** de `DOCS/UI_STANDARDS.md` ; preuve par critère datée dans le VERIFY ; validation PO de l'arborescence de menu avant code.

## Files

- `D:\_vibe\xGR\xGR\Necessaire\ADMIN_PATTERNS.md`, `Tresorerie.Vue/src/components/administration/AdminFormDrawer.vue`, `AdminConfigTable.vue`, `src/data/configPriorities.json`, `src/composables/useConfigDiagnostic.js`, `src/layout/appMenuModel.js`, `AppMenu.vue`, `AppMenuItem.vue`, `src/views` (écrans pilotes `ModesReglementGroupe.vue`, `OptionsSociete.vue`).
- `declaration-tva-web/src/App.tsx` (sidebar actuelle), `DOCS/UI_STANDARDS.md`.
