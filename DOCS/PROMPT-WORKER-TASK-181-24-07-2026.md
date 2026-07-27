Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md`, puis le
fichier TASK ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Contexte

Décision PO (24/07/2026) : le tag `<des>` (désignation) de l'export XML CDC DGI est aujourd'hui
toujours vide (`Designation = ""`, gap connu et documenté — hors périmètre ici). En attendant
l'arbitrage de la vraie source, le PO acte un placeholder en dur : **"Achat marchandise"**. Confirmé :
cet export XML ne porte que la TVA déductible (Décaissement, filtre déjà existant) — une seule valeur
fixe suffit, aucune conditionnalité par sens fiscal.

## Ta mission

Traiter **TASK-181-export-xml-designation-placeholder-achat-marchandise.md** (dossier
`D:\_vibe\GRF\TASKS\`) — seule task de ce lot.

Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les erreurs, puis
écris `VERIFY/TASK-181_verify.md` en suivant le même niveau de détail que `VERIFY/TASK-144_verify.md`
(sers-t'en de modèle) : section Périmètre livré, Fichiers modifiés, Checklist, **et une section
"Reste à valider" honnête si tout n'a pas pu être vérifié** — ne déclare jamais un point validé si tu
ne l'as pas réellement vérifié.

## Règles de travail (non négociables)

- **Portée exacte, un seul fichier de code** : `Declaration.Export.Xml/DeclarationXmlExporter.cs:65` —
  remplacer la valeur écrite dans `<des>` (actuellement `EscapeXml(ligne.Designation?.Trim())`) par le
  littéral fixe `"Achat marchandise"`. C'est un remplacement **total**, pas un fallback conditionné à
  une `Designation` vide — même si `Designation` portait un jour une valeur réelle, `<des>` doit rester
  fixe tant que cette task n'est pas explicitement révisée.
- **Ne touche à aucun autre fichier de production** :
  - Pas `DeclarationWorkflowService.cs::ConstruireModeleExportAsync` (`Designation = ""` reste
    inchangé — le gap DM_LGTVA/LigneCandidate reste ouvert tel quel).
  - Pas `Declaration.Export.Excel/Exporter.cs` — la colonne « Désignation » de l'export Excel n'est
    pas concernée par ce placeholder, qui est strictement XML.
  - Ne touche pas au filtre Décaissement/Encaissement existant
    (`DeclarationXmlExporter.cs:21-22`) — reste tel quel.
- Mets à jour les tests impactés dans `Declaration.Export.Xml.Tests/DeclarationXmlExporterTests.cs`
  (assertions actuelles sur `<des>` basées sur `ligne.Designation`, ~lignes 51/128/144/251/338) : ils
  doivent désormais attendre `<des>Achat marchandise</des>` quelle que soit la `Designation` fournie en
  entrée du test.
- Build back (`dotnet build DeclarationTVA.slnx`) ET `dotnet test Declaration.Export.Xml.Tests`
  doivent passer avant d'écrire un VERIFY. Rejoue aussi la suite complète pour t'assurer d'aucune
  régression ailleurs (aucun autre projet ne devrait être affecté par ce changement).
- Le VERIFY doit inclure un exemple de fichier `.xml` généré (ou un extrait suffisant) montrant
  `<des>Achat marchandise</des>` sur toutes les lignes.
- **Travail en parallèle** : TASK-180 est possiblement en cours par un autre worker au même moment.
  Aucun chevauchement de fichier (TASK-180 ne touche que `Model.cs`/`ConstructeurDeclaration.cs`/
  `DeclarationWorkflowService.cs::ConstruireModeleControleAsync`/`Exporter.cs`) — travaille normalement,
  mais committe **uniquement** les fichiers de TASK-181 dans ton commit, jamais un fichier modifié par
  l'autre task même si `git status` en affiche par ailleurs.
- Committe le correctif **en un seul commit** pour cette TASK, message clair, jamais `--no-verify`,
  **ne jamais pousser (`git push`)** — le commit reste local pour revue.
- Respecte les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors repository, pas de
  secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse — documente tout
  compromis dans le VERIFY).
- Si un point est réellement ambigu : **arrête-toi sur ce point précis, documente-le clairement dans
  le VERIFY** plutôt que d'inventer une réponse.

## À la fin

Laisse la TASK traitée dans `VERIFY/` (ne la déplace pas toi-même vers `DONE_DETAIL/` — c'est le rôle
de l'architecte de la reviewer et de faire la clôture). Si tu n'as pas eu le temps de la terminer,
indique dans un résumé final l'état exact d'avancement et ce qu'il reste à faire.
