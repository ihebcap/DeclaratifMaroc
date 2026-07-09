# Diagnostic Défaut 1
Sortie de GET /api/declarations/{id}/lignes?domaine=Rapprochement&numeroRapprochement=RC26060104
TotalCount = 1082

Sortie de GET /api/declarations/{id}/lignes?domaine=Rapprochement
TotalCount = 1082

Réponses:
1. Le domaine Rapprochement renvoie-t-il des lignes tout court ? Oui (1082 lignes).
2. Le champ qui porte le numéro s'appelle-t-il bien numeroRapprochement, et sa valeur est-elle exactement RC26060104 ? Oui, le champ est numeroRapprochement et la valeur existe.
3. Le back accepte-t-il le filtre en liste ou attend-il un autre nom/format ? Le back ignore les paramètres en query string direct. Il attend un paramètre ilter contenant un JSON stringifié, ex: ?filter={"numeroRapprochement":"RC26060104"}.

# Diagnostic Défaut 3
Sortie de GET /api/declarations/{id}/lignes?statutLigne=Intégrée&statutLigne=Proposée
TotalCount = 1082 (le filtre est ignoré car passé en query string plat).

Réponses:
1. Le back filtre-t-il réellement sur statutLigne ? Non, le back ignore le paramètre plat et attend ilter={"etat":...}.
2. Le nom du champ/valeurs côté API sont-ils exactement Intégrée/Proposée/Exclue/Reportée/Écartée ? Le champ s'appelle tat et non statutLigne. Les valeurs acceptées par l'API dans le JSON sont bien les chaînes accentuées ("Proposée", "Intégrée", "Exclue", "Reportée", "Écartée") qui sont ensuite traduites en entiers dans Repository.
