# Relic Loadout Journal

Open **Relic Journal** on the Super Fantasy Kingdom title menu to revisit the kingdom, hero, final day and held relics of each completed run. Browse runs and relics using the menu controls or the mouse. Names, descriptions and icons come from the installed game. A removed relic remains visible by ID.

The mod records a run when the player accepts its final result. Each save profile has its own history under `RelicLoadoutJournal/profile-N` in the game's save directory. Each run ID has one JSON file. The journal begins recording when installed.

## Development

Build on Windows with the .NET SDK, Python 3, a local game installation and a BepInEx 5 profile. The project defaults to the Steam library on D: and a Thunderstore profile named `Relic Loadout Journal Dev`. Override either with MSBuild properties `GameDir` and `ProfileDir`.

```powershell
dotnet build -c Release
python package.py
python tests/run.py --save "path/to/copied/1_human_game.data"
```

The ZIP is written to `dist/RelicLoadoutJournal-0.1.0.zip`. The check runner accepts `--game` and `--profile` for other installations. It loads the packaged DLL into embedded Mono, checks the installed game's save and completion methods, and exercises the journal, focus navigation and layout using the supplied save and temporary storage.

For gameplay review, use a copied save in an isolated game data location. A BepInEx mod profile selects plugins, while Unity chooses the save directory separately. Check a real save/continue through accepted completion, known relic icons and descriptions, menu focus and prompts, and readability at 1× and 1.5×.

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution and commit conventions.

## License

Original mod code is licensed under the [MIT License](LICENSE). Super Fantasy Kingdom and its assets belong to their respective owners.
