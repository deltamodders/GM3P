README

If you just want to know how to start it up, go to \GameMakerxDeltaCombiner\bin\Debug\Net8.0\GameMakerxDeltaCombiner.exe

Section 1: Operation Instructions
	
	Part 1.1: Common
		1.1.1 First Enter in the path of the vanilla data.win of the file. If you just want to dump and combine already prepared modded data.win files, enter "skip" into this field. Hit Enter. 
		1.1.2 Then enter in the amount of mods you want to operate on as an integer (e.g. "2" for 2 mods).
	Part 1.2: Mass Apply Patches
		1.2.1 One at a time, enter in the path of your xDelta patches, hitting enter in between each one.
		1.2.2 NOTE: There is a current known issue of the program only accepting patches in the C: drive.
		1.2.3 Wait for xDelta3 to finish patching the data.win files
		1.2.4 If you just wanted to make a ton of single-mod data.win copies of your game, you may exit the terminal now
	Part 1.3: Dump and Combine
		1.3.1 Because of the current version of UTMT CLI as of the time of writing (0.8.2.0) is broken, the program can't auto-dump the files from the data.win, and it must be done manually.
		1.3.2 If you have never opened UTMT before, open it once in order to set .win files to it, then close it.
		1.3.3 Open up each of the data.win files (with the exception of C:\xDeltaCombiner\1\data.win), then hit "Scripts" on the top left. Run "ExportAllCode.csx" and "ExportAllTilesets.csx" (only assets types that were tested)
		1.3.4 When asked where to save them save it in the Objects subfolder of the directory that data.win is in.
		1.3.5 Close out of all UTMT windows, if it ask you to save, hit "no"
		1.3.6 Back on this app, hit enter
		1.3.7 wait for the app to compare, it will either exit when finished, or show "Hit any button to exit..."
		1.3.8 Open up C:\xDeltaCombiner\1\data.win in UTMT, and run the scripts "ImportGML.csx" and "ImportAllTileSets.csx", choose C:\xDeltaCombiner\1\Objects as the selected folder
		1.3.9 Hit File -> Save and then hit enter. It will ask if you want to replace it, hit "yes"
		1.3.10 Your complete data.win is located at C:\xDeltaCombiner\1\data.win
		1.3.11 Note there is no garbage collection, so you will have to delete the files in C:\xDeltaCombiner\#\Objects to run the program successfully again

Section 2: Technical Information
	Part 2.1: System Requirements
		2.1.2: System Requirements
			OS: Windows 10 2004 or Later
			OS Components: WSL/Windows Subsystem for Linux
			CPU: virtualization support
			Storage: 64MB
			RAM: 32MB
			Software: UTMT/UndertaleModTool
			Software (cont.): the Ubuntu version of xDelta3 CLI
		2.1.3: System Recommended
			OS: Windows 11
			Storage: 2GB in the C: drive
			RAM: 64MB	
	Part 2.2: C:\xDeltaCombiner directory structure
		xDeltaCombiner\0: Vanilla Folder
		xDeltaCombiner\1: Finished Product Folder
		xDeltaCombiner\(2+): Single Mod Folder
		xDeltaCombiner\#\Objects: GameMaker Objects location
	Part 2.3: Known Issues and limitations
		There is a current known issue of the program only accepting patches in the C: drive.
		There is a current known issue of the program crashing when trying to compare objects in the subfolders of #\Objects\
		Limitation: Can only apply patches meant for the same version of the same game. Can't mix and match
		Limitation: If 2 mods modify the same object, only the changes for the last affect mod entered applies.
	Part 2.4: Tools used
		xDelta3 CLI for applying mods
		UndertaleModTool for dumping and importing GameMaker Objects
		VS2022 to make and build this program
		This program was poorly written in C#
		SHA1 Hashing was used for comparing files