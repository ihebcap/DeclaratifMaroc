Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\ARCHITECTURE_PROJECT.md`
(s'il existe), `D:\_vibe\GRF\TODO.md` (section « ROADMAP — V2 : source du détail TVA... », paragraphe
« Extension confirmée au module Délai de Paiement »), puis le fichier TASK ci-dessous en entier. Ne
suppose jamais un contexte manquant.

## Contexte

Dette assumée depuis le CDC (19/07/2026) et reprise telle quelle par TASK-133 : dans le XML de dépôt
Délai de Paiement, `<natureMarchandise>` est toujours vide et `<dateLivraisonMarchandise>` est en
réalité la date de facture. Le CDC documente que le mapping existe déjà, configurable par société dans
`P_SOCIETE` (`SO_ColValueNatureMarchandise` / `SO_ColValueDateLivraisonMarchandise`, colonnes de
`F_DOCENTETE`). Décision PO (28/07/2026) : câbler réellement ces 2 champs, en répliquant le pattern déjà
en place pour `SO_ColValueNumRegistreCommerceFournisseur` (whitelist, tolérance à l'absence de config).

## Ta mission

Traiter **TASK-191**
(`D:\_vibe\GRF\TASKS\TASK-191-cablage-nature-date-livraison-marchandise-ddp.md`).

Vérifie d'abord que les champs Objectif/Périmètre/Étapes/Livrables/Critères de validation sont bien
remplis (ils le sont). Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige
les erreurs, puis écris `VERIFY/TASK-191_verify.md` en suivant le même niveau de détail que
`DONE_DETAIL/TASK-190_verify.md` (sers-t'en de modèle) : section Périmètre livré, Fichiers modifiés,
Tests ajoutés, Checklist, et une section « Reste à valider » honnête si tout n'a pas pu être vérifié.

## Règles de travail (non négociables)

- **Portée exacte** (§Périmètre STRICT de la TASK) :
  1. Étendre la lecture société (méthode dédiée ou extension de l'existant) pour lire
     `SO_ColValueNatureMarchandise` / `SO_ColValueDateLivraisonMarchandise` (`P_SOCIETE`), même whitelist
     que `ValiderNomColonneOptionnelle` (`DeclarationDelaiPaiementRepository.cs:499-512`), tolérance
     explicite à l'absence de config (retourne vide, jamais une erreur) — reproduis le contrat de
     `GetIdentitesFiscalesTiersAsync` (même fichier, l.434-490) à l'identique.
  2. **Avant tout code** : identifie/confirme la clé de jointure facture → `F_DOCENTETE` (piste :
     `DO_Numero`/`DO_Reference` déjà projetés par `SelectionDelaiPaiementRepository.cs` autour de
     l.94-98, TASK-187). Lecture Sage strictement en lecture seule, par lot (même principe que
     `GetIdentitesFiscalesTiersAsync`), jamais un aller-retour par ligne.
  3. **Si aucune piste de jointure exploitable n'existe** sur le flux DDP actuel (`DO_Numero`/
     `DO_Reference` absent ou ambigu) : **arrête-toi**, documente précisément le blocage dans le VERIFY
     (section « Reste à valider » ou une section dédiée « Blocage »), et **n'improvise aucune jointure
     alternative non validée** (ex. appariement par date) — signale à l'architecte plutôt que deviner.
  4. Câble le résultat dans `DeclarationDelaiPaiementXmlModele.cs` (`Declaration.Core`) : remplace le
     hardcode vide de `NatureMarchandise` (l.210, commentaire `// dette assumée`) et le fallback
     `dateEmission` de `DateLivraisonMarchandise` par la valeur réelle quand disponible — **fallback
     identique à aujourd'hui si la config est absente ou la valeur introuvable sur `F_DOCENTETE`**.
     Vérifie aussi `Declaration.Export.Xml/DeclarationDelaiPaiementXmlExporter.cs:114-115` (lecture du
     modèle, pas de logique dupliquée à y ajouter).
  5. Tests unitaires (3 cas minimum, cf. Étapes de la TASK) : société sans colonne configurée
     (comportement actuel inchangé — non-régression stricte) ; société avec colonne configurée mais
     valeur absente sur le document (fallback identique à aujourd'hui, jamais d'exception) ; société
     avec valeur réelle présente (câblage effectif, XML reflète la vraie valeur).
- **Exclu** : toute modification de schéma (aucune nouvelle table/colonne, tout existe déjà dans
  `P_SOCIETE`/`F_DOCENTETE`) ; toute généralisation multi-source (grand livre comptable — mentionnée en
  Contexte de la TASK comme motivation long terme uniquement, pas à implémenter ici) ; tout écran de
  configuration (déjà couvert par l'écran WinForms legacy `UcParamDeclarationTvaEncaissement`, hors
  périmètre GRF — **ne touche à aucune table/colonne owned par apbs-gr_winform**, cf. contrainte
  permanente de ce projet).
- **Tu as un accès réel à la base de données** : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d
  GR_EMA_DISTRIBUTION -Q "..."` (identifiants complets dans `D:\_vibe\GRF\connections.json`). Utilise-le
  pour :
  - Confirmer le schéma réel de `P_SOCIETE` (présence effective des 2 colonnes
    `SO_ColValueNatureMarchandise`/`SO_ColValueDateLivraisonMarchandise`) et de `F_DOCENTETE` (colonnes
    disponibles) avant d'écrire le moindre code — ne suppose pas leur existence/nom sur la seule foi du
    CDC.
  - Vérifier sur au moins 1-2 cas réels (société configurée si une l'est déjà, sinon documente que
    aucune société réelle n'a la config renseignée à ce jour) que la jointure choisie vers
    `F_DOCENTETE` fonctionne et retourne la bonne ligne.
  - Lecture seule stricte sur Sage (`F_DOCENTETE`/`P_SOCIETE`) — aucune écriture nulle part dans cette
    TASK.
- Build back (`dotnet build DeclarationTVA.slnx` — vérifie qu'aucun process `Declaration.API.exe` ne
  verrouille le build, comme rencontré sur les TASK précédentes) ET tests (`Declaration.Core.Tests`,
  et toute suite couvrant `Declaration.Export.Xml`/`Declaration.Infrastructure` si elle existe) doivent
  passer avant d'écrire le VERIFY.
- Un seul commit pour TASK-191, message clair, jamais `--no-verify`, **ne jamais pousser (`git push`)**
  — le commit reste local pour revue architecte.
- Respecte les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors couche repository,
  pas de secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse) et la contrainte
  permanente de ce projet : **aucune modification de schéma sur une table owned par apbs-gr_winform**
  (aucune ici de toute façon — périmètre strictement lecture).
- Si un point est réellement ambigu (notamment l'absence de piste de jointure fiable, ou une colonne
  `P_SOCIETE`/`F_DOCENTETE` absente du schéma réel) : **arrête-toi sur ce point précis, documente-le
  clairement dans le VERIFY** plutôt que d'inventer une réponse ou de dégrader silencieusement le
  contrat (ex. jointure approximative par date — explicitement interdit par la TASK).

## À la fin

Laisse TASK-191 dans `VERIFY/` (ne la déplace pas toi-même vers `DONE_DETAIL/`). Termine par un résumé
clair incluant explicitement : la clé de jointure retenue (ou le blocage documenté si aucune n'est
fiable), le résultat des tests, et si une société réelle en base a pu servir de preuve de câblage
effectif (config présente) ou seulement de preuve de non-régression (aucune société configurée à ce
jour).
