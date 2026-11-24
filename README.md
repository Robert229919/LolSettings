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

## Télécharger

**[⬇ LolSettings (Windows 10/11, 64 bits)](https://github.com/Robert229919/LolSettings/releases/latest)**. Un seul `.exe` (nécessite le runtime .NET 10 Desktop). Il n'est pas signé : si SmartScreen s'affiche, clique sur « Informations complémentaires » puis « Exécuter quand même ».

## Utilisation

1. Ouvre le **launcher League of Legends** avec le compte qui a les bonnes touches, puis clique sur **+ Enregistrer**.
2. **Ferme le launcher** (et le jeu). Laisse LolSettings ouvert.
3. **Rouvre le launcher avec le nouveau compte** (pas de partie en cours).
4. **Profils** → **Appliquer →** → choisis les catégories → **Appliquer**.

Ce guide est aussi affiché dans l'appli (onglet Comptes → « Comment ça marche »).

**Mises à jour** : au démarrage, LolSettings regarde sur GitHub si une nouvelle version est sortie et te demande si tu veux la télécharger. Seule une requête vers l'API publique de GitHub est faite, aucune donnée perso n'est envoyée.

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
