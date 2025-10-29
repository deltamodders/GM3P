# GameMaker Mass Mod Patcher (GM3P)

**G**ame**M**aker **M**ass **M**od **P**atcher (abbreviated to **GM3P**) is a tool used to merge multiple xdelta mods for GameMaker games and thus be able to play multiple mods at once.<br />

_This tool is currently used in the backend of [Deltamod](https://gamebanana.com/tools/20575) for mod merging._

## How to build
1. Make sure you have .NET 8.0 or later and Git installed<br />
2. Open the .sln up in VS or Jetbrains and build the project "GM3P". (IDK about other IDEs like VSCode)<br />
3. Open the .sln up of [UTMT-For-GM3P](https://github.com/deltamodders/UTMT-For-GM3P) and build the project "UnderTaleModCli"<br />
4. Copy and paste the UTMT build into the GM3P build under a folder named "UTMTCLI" <br />
5. Copy and paste the contents of the "UTMT Scripts" folder under a folder named "Scripts" under "UTMTCLI" <br />
6. Download [xDelta3 v3.0.11 64-bit](https://github.com/jmacd/xdelta-gpl/releases/download/v3.0.11/xdelta3-3.0.11-x86_64.exe.zip) and paste it under the GM3P build.
7. Happy patching

## Credits
| | Name | Role |
|-|------|-------|
| ![Zorkats](./GitHubAssets/zorkats.jpeg) | Zorkats | Main programmer |
| ![Techy](./GitHubAssets/techy.png) | techy804 | Creator of GM3P |

## License
Licensed under GNU GPL 3.0