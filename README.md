<p align="center">
  <img src="src/LolSettings/icon/icon-256.png" width="112" alt="LolSettings">
</p>

<h1 align="center">LolSettings</h1>

<p align="center">Enregistre les réglages League of Legends de tes comptes et copie-les d'un compte à l'autre, sur Windows.</p>

---

## ⚠️ Avertissement — à lire avant d'utiliser

- **Je me désolidarise totalement de tout bannissement, sanction, perte de compte ou autre problème** qui pourrait survenir, que ce soit **sur ton compte Riot / League of Legends** ou **sur ton PC** (fichiers, configuration, système), suite à l'utilisation de ce logiciel. **Tu l'utilises entièrement à tes risques et périls.**
- **Je n'encourage pas le smurf, je le condamne.** Ce logiciel n'est pas fait pour faciliter le smurf ou le contournement des règles de Riot Games. Il sert à retrouver tes propres réglages sur tes propres comptes.
- Ce projet n'est **ni affilié, ni approuvé, ni soutenu par Riot Games**. *League of Legends* et *Riot Games* sont des marques de Riot Games, Inc.
- L'outil utilise l'API **locale et non officielle** du client League of Legends, comme le jeu lui-même, pour lire et écrire les réglages du compte connecté. Riot peut la modifier ou la bloquer à tout moment.
- Le logiciel est fourni **« tel quel », sans aucune garantie**. Version de test : des bugs sont possibles.

---

## Fonctionnalités

- **Comptes** : liste des comptes auxquels tu t'es connecté avec l'appli ouverte, avec surnoms.
- **Enregistrer** : choisis les réglages à garder, par catégorie ou un par un (souris & caméra, touches, interface, audio, graphismes).
- **Appliquer** un profil sur le compte connecté, catégorie par catégorie.
- **Exporter / importer** un profil en fichier `.lolprofile` ou en code à partager.
- **Sauvegarde automatique** du compte cible avant chaque application, supprimée au bout d'1 h.

## Utilisation

1. Ouvre le **client League of Legends**, connecte-toi au compte source → **+ Enregistrer**.
2. Change de compte (**pas de partie en cours**, client ouvert).
3. **Profils** → **Appliquer →** → choisis les catégories → **Appliquer**.

Les profils sont stockés dans `%APPDATA%\LolSettings\profiles`.

En cas de souci, `LolSettings.exe --check C:\temp\lol` écrit un diagnostic en lecture seule (à m'envoyer).

## Compiler

Prérequis : Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download), Microsoft Edge WebView2.

```powershell
dotnet publish src\LolSettings -c Release -o dist
```

L'exécutable est `dist\LolSettings.exe` (nécessite le runtime .NET 10 Desktop). Un workflow GitHub Actions (`.github/workflows/build.yml`) le compile aussi.

## Structure

- `src/LolSettings/*.cs` : application C# (WinForms + WebView2), API locale LCU, profils, catégories.
- `src/LolSettings/web/` : interface (HTML/CSS/JS) embarquée dans l'exe.
- `src/LolSettings/icon/` : icône.
