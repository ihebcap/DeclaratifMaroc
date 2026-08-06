<#
.SYNOPSIS
    TASK-115 : recupere le binaire officiel WinSW-x64.exe (licence MIT, projet winsw/winsw)
    dans le dossier courant, plutot que de le committer en dur dans le depot (cf. reserve
    licence notee dans TASK-115).

.DESCRIPTION
    A executer manuellement (ou depuis le futur publish.ps1 de TASK-044, non livre a ce jour)
    avant de packager deploy\. Le binaire telecharge est renomme WinSW.exe : Declaration.Setup
    le recopie ensuite sous le nom du service choisi (ex. DeclaratifMaroc.exe) a cote de
    Declaration.API.exe, avec le gabarit XML DeclaratifMaroc.winsw.xml.template de ce dossier.

.PARAMETER Version
    Tag de release WinSW a recuperer (cf. https://github.com/winsw/winsw/releases).
    Defaut : derniere version stable connue au moment de l'ecriture de ce script.
#>
param(
    [string]$Version = "v3.0.0-alpha.11",
    [string]$OutputPath = (Join-Path $PSScriptRoot "WinSW.exe")
)

$ErrorActionPreference = "Stop"

$url = "https://github.com/winsw/winsw/releases/download/$Version/WinSW-x64.exe"

Write-Host "Telechargement de WinSW ($Version) depuis $url ..."
Invoke-WebRequest -Uri $url -OutFile $OutputPath -UseBasicParsing

if (-not (Test-Path $OutputPath)) {
    throw "Echec du telechargement : $OutputPath absent apres Invoke-WebRequest."
}

Write-Host "WinSW recupere : $OutputPath"
Write-Host "Rappel licence : WinSW est distribue sous licence MIT (voir https://github.com/winsw/winsw/blob/master/LICENSE.txt) — a rappeler si redistribue au client."
