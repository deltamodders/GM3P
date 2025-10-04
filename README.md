# GameMaker Mass Mod Patcher

GameMaker Mass Mod Patcher (abbreviated to GM3P) is a tool used to merge multiple XDelta mods for GameMaker games and thus be able to play multiple mods at once.<br /><br />
Used in the backend of [Deltamod](https://gamebanana.com/tools/20575) for mod merging.

## License
Licensed under GNU GPL 3.0

## How to build
1. Make sure you have .NET 8.0 or later and Git installed<br />
2. Open the .sln up in VS or Jetbrains and build the project "GM3P". (IDK about other IDEs like VSCode)<br />
3. Open the .sln up of [UTMT-For-GM3P](https://github.com/deltamodders/UTMT-For-GM3P) and build the project "UnderTaleModCli"<br />
4. Copy and paste the UTMT build into the GM3P build under a folder named "UTMTCLI" <br />
5. Copy and paste the contents of the "UTMT Scripts" folder under a folder named "Scripts" under "UTMTCLI" <br />
6. Download [xDelta3 v3.0.11 64-bit](https://github.com/jmacd/xdelta-gpl/releases/download/v3.0.11/xdelta3-3.0.11-x86_64.exe.zip) and paste it under the GM3P build.
7. Happy patching

## Credits
[Zorkats](https://gamebanana.com/members/3914910) - Main Programmer<br />
[techy804](https://gamebanana.com/members/4548254) - Creator <br />
