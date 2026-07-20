# TASK-153 — URL morte de l'installateur .NET Framework 4.8 → crash non géré du Setup

Status: 🆕 à faire
Priority: HIGH (bloque l'installation complète sur toute machine sans .NET 4.8 déjà présent)
Risk: LOW (Setup uniquement, aucun impact sur l'API/worker/front)
Module: Declaration.Setup

> **Origine :** capture d'écran PO (20/07/2026) pendant l'installation/mise à jour de « Déclaratif
> Maroc » — boîte de dialogue Windows Forms générique d'exception non gérée :
> `System.Net.Http.HttpRequestException: Response status code does not indicate success: 404
> (Not Found)`, pile d'appel `Declaration.Setup.Services.NetFrameworkInstaller.DownloadAndInstall...`
> appelé depuis `Declaration.Setup.SetupForm.ValidatePrerequisitesStepAsync()`. Le PO a orienté le
> diagnostic : « je pense que c'est à l'installation de 4.8 ».

## Constat (preuve de code + vérification externe)

- [Declaration.Setup/Services/NetFrameworkInstaller.cs:13-14](../Declaration.Setup/Services/NetFrameworkInstaller.cs:13) :
  URL en dur `https://download.microsoft.com/download/2/1/3/213ac857-2e08-4586-95b1-2ec1cd688094/NDP48-x86-x64-AllOS-ENU.exe`,
  commentée « URL officielle et stable » — **vérifiée en direct : renvoie HTTP 404**. Microsoft a
  retiré/déplacé ce lien direct (GUID de download spécifique), ce n'est plus un point stable.
- [Declaration.Setup/Services/NetFrameworkInstaller.cs:22-24](../Declaration.Setup/Services/NetFrameworkInstaller.cs:22) :
  `response.EnsureSuccessStatusCode()` lève donc systématiquement `HttpRequestException` dès que
  cette étape est atteinte (poste sans .NET 4.8 déjà installé).
- [Declaration.Setup/SetupForm.cs:516-530](../Declaration.Setup/SetupForm.cs:516) : l'appel à
  `NetFrameworkInstaller.DownloadAndInstallSilentlyAsync()` est entouré d'un `try { ... } finally {
  Enabled = true; }` — **sans `catch`**. L'exception remonte donc non gérée jusqu'au runtime, d'où
  la boîte de dialogue générique « exception non gérée » vue par le PO (au lieu d'un message
  d'erreur propre côté Setup).

Double défaut : (1) URL morte côté Microsoft, (2) absence de gestion d'erreur qui transforme tout
échec réseau (URL morte, coupure réseau, proxy, etc.) en crash complet du Setup plutôt qu'en message
utilisateur maîtrisé — cf. `MessageBox.Show(... "L'installation ... a échoué ...")` déjà prévu
ligne 521 mais jamais atteint car l'exception court-circuite ce chemin.

## Objectif

1. Remplacer l'URL morte par une URL Microsoft valide et stable pour le redistribuable offline
   .NET Framework 4.8 (vérifier avant de coder — le PO/Gemini doivent confirmer qu'un lien officiel
   direct existe encore, sinon envisager le lien vers la page officielle "dotnet.microsoft.com" et
   ouvrir le navigateur en fallback plutôt qu'un téléchargement silencieux non fiabilisable).
2. Ajouter un `catch` explicite autour de l'appel dans `ValidatePrerequisitesStepAsync()` (ou dans
   `DownloadAndInstallSilentlyAsync` lui-même) pour que tout échec (réseau, URL invalide, droits
   admin refusés) affiche le `MessageBox` d'échec déjà écrit ligne 521 au lieu de crasher le
   processus — cohérent avec le comportement déjà prévu par le code existant pour `ok == false`.

## Garde-fous

- Ne pas transformer ceci en refonte de `NetFrameworkInstaller` : correction ciblée (URL + gestion
  d'erreur), pas de nouvelle dépendance, pas de retry automatique non demandé.
- Ne pas supprimer l'avertissement explicite du redémarrage Windows requis (déjà en place ligne 525).
- Le message d'échec doit rester explicite et diriger vers une installation manuelle (déjà le cas
  ligne 521) — ne pas masquer l'échec silencieusement.

## Files

- `Declaration.Setup/Services/NetFrameworkInstaller.cs` (l.13-14, l.16-27).
- `Declaration.Setup/SetupForm.cs` (l.516-530).

## Validation

- [ ] Build Setup OK.
- [ ] Nouvelle URL vérifiée manuellement (HTTP 200, exécutable valide) avant merge.
- [ ] Rejeu sur poste sans .NET 4.8 : plus de crash — soit installation silencieuse réussie, soit
      `MessageBox` d'échec propre si le téléchargement échoue (couper le réseau pour simuler).
- [ ] Non-régression : poste avec .NET 4.8 déjà présent → étape prérequis ignorée comme avant
      (`PrerequisiteChecker.CheckDotNetFramework48()` inchangé).

## Dépendances / risques

- Indépendante des autres chantiers Setup (TASK-115 en cours) — correctif isolé et non bloquant
  pour la suite du wizard.
