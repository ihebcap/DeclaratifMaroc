# TASK-139 — Badge « Écart détecté » global vs détail « ligne incohérente » filtré par onglet

Status: DONE — approuvée 19/07/2026 (voir DONE_DETAIL/TASK-139_verify.md, DONE.md, CHANGELOG.md)
Priority: HIGH
Risk: LOW
Module: declaration-tva-web / Declaration.API (checkup)

## OBJECTIF

Corriger le message trompeur « Écart détecté mais aucune ligne incohérente identifiée dans les
lignes déclarées » qui s'affiche à l'étape ② (Vérifier & Intégrer) lorsque la ligne réellement
incohérente (TTC ≠ HT+TVA) se trouve dans l'**autre** onglet (domaine) que celui actuellement
ouvert.

## BUSINESS VALUE

Confirmé par le PO sur la déclaration `TVA1-2026-02` : écart de -49 167,99 MAD affiché onglet
Décaissement (ou Encaissement) ouvert, avec « aucune ligne incohérente identifiée » — alors que la
ligne fautive était dans la partie **Collectée** (Encaissement). Le message actuel fait croire à
une anomalie non expliquée / non tracée, alors que le back-end sait probablement l'expliquer
globalement (`ecartExplique`). Ce faux signal fait perdre confiance dans le contrôle et peut pousser
à chercher une anomalie qui n'existe pas dans l'onglet affiché.

## CONTEXTE — cause racine identifiée (lecture de code)

- Le badge « Écart détecté : X MAD » (`equilibre.ecart`) est calculé **toutes lignes confondues**
  (Décaissement + Encaissement), `DeclarationsController.cs:239-277`.
- Le détail « ligne incohérente » affiché sous le badge est filtré **par onglet actif** :
  `VerifierIntegrerPanel.tsx:386` → `recapIncoherence.find(r => r.domaine === selectedTab && r.incoherente)`.
- Si la ligne incohérente responsable de l'écart est dans le domaine **non affiché**, cette
  recherche ne trouve rien → branche « aucune ligne incohérente identifiée »
  (`VerifierIntegrerPanel.tsx:1094-1101`) — alors que `checkup.equilibre.ecartExplique` (calculé
  globalement, `DeclarationsController.cs:304`) vaut très probablement `true` dans ce cas.
- C'est un angle mort laissé par TASK-112 (qui a scindé le détail par domaine pour éviter la
  tautologie « Source ») et TASK-108 (qui a corrigé le calcul global de l'écart) : personne n'a
  recroisé le fait que le badge reste global pendant que le détail est devenu local à l'onglet.

## CONTRAINTES

- Ne pas recalculer l'écart ni changer sa valeur — défaut de **restitution/affichage** uniquement.
- Respecter le principe déjà en place « aucune ligne silencieuse » (TASK-112) : si l'écart est
  expliqué mais dans l'autre onglet, le dire explicitement plutôt que de laisser croire qu'aucune
  cause n'est identifiée.
- Ne pas modifier le comportement quand la ligne incohérente est bien dans l'onglet affiché (cas
  déjà correct, TASK-112).
- Pas de changement sur `ecartExplique` lui-même (déjà correct, calcul global).

## FILES

- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` (lignes ~385-386 `ligneIncoherente`, ~1094-1101
  message de repli)
- `Declaration.API/Controllers/DeclarationsController.cs` (lignes ~279-304, `recapIncoherence` /
  `ecartExplique` — lecture seule attendue, sauf si l'arbitrage ci-dessous change le contrat JSON)

## VALIDATION

- [ ] Build OK
- [ ] Tests passés
- [ ] Sur une déclaration en écart dont la ligne incohérente est dans l'onglet **non affiché**, le
      message distingue clairement : « écart expliqué, situé dans l'onglet [Encaissement/Décaissement] »
      (ou équivalent), au lieu de « aucune ligne incohérente identifiée ».
- [ ] Reproduction réelle sur `TVA1-2026-02` (cas signalé par le PO, écart -49 167,99 MAD, ligne
      incohérente côté Collectée/Encaissement) : le nouveau message est correct depuis l'onglet
      Décaissement.
- [ ] Non-régression : quand la ligne incohérente est dans l'onglet affiché, comportement TASK-112
      inchangé (tableau `RecapSourceTable` + drill).
- [ ] Non-régression : cas où l'écart n'est réellement expliqué par **aucune** ligne d'aucun domaine
      (`ecartExplique === false` globalement) — le message doit rester « non expliqué », pas basculer
      à tort vers « regardez l'autre onglet ».

## ARCHITECTURE RULES APPLICABLES

- Principe « aucune ligne silencieuse » (TASK-112).
- Pas de dette technique silencieuse (ARCHITECTURE.md §5) : ne pas juste masquer le message, expliquer
  la localisation réelle.

## NOTES

**Arbitrage PO/architecte à trancher avant implémentation** (deux options, ne pas coder avant
réponse) :
1. Front seul : `VerifierIntegrerPanel.tsx` regarde aussi `recapIncoherence` **hors onglet actif**
   pour détecter ce cas et adapter le message (« la ligne incohérente est dans l'onglet X »,
   éventuellement avec lien pour basculer). Aucun changement back nécessaire (`recapIncoherence`
   contient déjà tous les domaines).
2. Ajouter au message un renvoi explicite au domaine concerné + éventuellement un raccourci pour
   changer d'onglet directement (meilleure UX, portée un peu plus large sur le front).

Recommandation architecte : option 1, périmètre strictement front, aucune donnée back à changer
(`recapIncoherence` a déjà tout ce qu'il faut, cf. `DeclarationsController.cs:289-302`).

Origine : question PO sur déclaration `TVA1-2026-02` (écart -49 167,99 MAD, 174 lignes candidates,
174 intégrées/proposées, 0 exclue/reportée/écartée) — confirmé par le PO : la ligne incohérente
était bien côté Collectée (Encaissement).
