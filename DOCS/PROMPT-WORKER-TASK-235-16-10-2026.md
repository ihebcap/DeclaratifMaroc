# PROMPT WORKER — TASK-235 (à coller dans Gemini)

Tu es le WORKER du projet GRF (`D:\_vibe\GRF`). Tu implémentes la TASK `TASKS/TASK-235-v2-compta-analyse-donnees-3-bases-lotrex-ema-gocom.md`.

## Branche
Travaille **uniquement sur la branche `feature/v2-compta`** (`git switch feature/v2-compta`). Ne touche jamais `main` ni les fichiers modifiés non commités de la TASK-224.

## À lire d'abord (dans cet ordre)
1. `TASKS/TASK-235-v2-compta-analyse-donnees-3-bases-lotrex-ema-gocom.md` (ta mission, périmètre strict)
2. `DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md` (contexte, décisions PO, premier spike)
3. `TASKS/TASK-226` à `TASK-229` et `TASK-234` (pour savoir à quoi servent tes analyses)

## Règles absolues
- **Lecture seule** : uniquement des `SELECT` sur les bases Sage. Jamais d'écriture, de `ALTER`, de `EXEC` d'écriture.
- **Aucun fichier du code applicatif ne doit être modifié.** Tes seuls fichiers : `DOCS/ANALYSE_V2_DONNEES_2026-10-16.md` et `DOCS/analyse_v2/*.sql`, plus ton fichier VERIFY.
- Chaque chiffre du rapport = la requête exacte qui l'a produit. Pas de chiffre sans requête, pas d'extrapolation.
- Si une analyse est impossible, écris « impossible » et pourquoi. Ne devine pas.
- Ne pas improviser un contexte manquant : si quelque chose est ambigu, le noter dans le rapport comme question au PO.
- **Ton point d'arrêt est le dépôt de `VERIFY/TASK-235_verify.md`.** Tu ne déplaces pas la TASK vers `DONE_DETAIL/`, tu ne touches pas `DONE.md`, `TODO.md`, `CHANGELOG.md`, et tu ne commites aucun message contenant « approuve ». La clôture est faite par l'architecte.

## Connexion
- Instance : `.\sql2022` (authentification Windows). Exemple :
  `sqlcmd -S ".\sql2022" -E -C -W -d "NEW_EMA DISTRIBUTION" -Q "SELECT ..."`
- Bases : `LOTREX`, `NEW_EMA DISTRIBUTION` (le nom contient un espace), `GOCOM`.
- Tables Sage : `F_ECRITUREC`, `F_JOURNAUX`, `F_COMPTET`, `F_TAXE`.

## Faits déjà établis par l'architecte (à vérifier, pas à croire)
- Date d'opération d'une écriture = `DATEADD(day, EC_Jour - 1, JM_Date)`.
- Une écriture d'achat : tiers au crédit (TTC), charge au débit avec `TA_Code`, TVA au débit sur le compte `F_TAXE.CG_Num` de ce code.
- Une pièce de banque a une ligne tiers (lettrée) et une ligne de trésorerie qui porte `EC_TresoPiece`.
- `GOCOM` ne contient pas `F_ECRITUREC`.

## Livrable
Les 13 analyses de la TASK, dans l'ordre, avec tableaux par base, requêtes, constats, risques et questions au PO.
