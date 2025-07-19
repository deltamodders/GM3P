README

Section 1: Operation Instructions
	
	Part 1.1: Console
		1.1.1 First Enter in the path of the vanilla data.win of the file. If you just want to dump and combine already prepared modded data.win files, enter "skip" into this field. Hit Enter. 
		1.1.2 Then enter in the amount of mods you want to operate on as an integer (e.g. "2" for 2 mods).
		1.1.3 One at a time, enter in the path of your xDelta patches, hitting enter in between each one.
		1.1.4 Wait for xDelta3 to finish patching the data.win files
		1.1.5 If you just wanted to make a ton of single-mod data.win copies of your game, you may exit the terminal now
		1.1.6 Hit Enter, unless you want to dump and import manually or you have your own version of UTMT CLI that you prefer. If you want to use your own version of UTMT CLI, enter in the path for that. If you want to dump and import manually, enter "skip".
		1.1.7 Once it is done dumping, hit enter
		1.1.8 wait for the app to compare the modded files to the vanilla files, hit enter once it is finished, it will then import the difference
		1.1.9 It will ask you what you would like to name your pack, enter anything you please.
		1.1.10 Your completed pack is located at \output\result\*pack name*\
		1.1.11 Once you hit enter again, the program will delete output\xDeltaCombiner and close. You may close via Alt+F4 or hitting the "X" on the window if you don't want to delete them, but you will need to manually delete the folder on next use. 
	Part 1.2: Available CLI commands
		console - Enters console app
		help - gives command satanax and an example, or displays available commands.
		clear - clears output\xDeltaCombiner for future use
		massPatch - patches a ton of identical data.win files with a single mod each (currently supports .csx, .win, and .xdelta mod formats)
		compare - Compares and combine GM objects. Dumping and importing optional, but recommended. Can Only be successfully called if massPatch was called before or the user manually set things up in output\xDeltaCombiner.
		result - saves a copy of the result to output\result\

Section 2: Technical Information
	Part 2.1: System Requirements
		2.1.2: System Requirements
			OS: Windows 10 (Unix systems, including Android, Mac, Linux, and ChromeOS, are not supported atm)
			CPU: x86_64
			Storage: 256MB
			RAM: 192MB
			Software: .NET 8.0 runtime
		2.1.3: System Recommended
			OS: Windows 11
			Storage: 2GB
			RAM: 512MB	
	Part 2.2: output directory structure
		xDeltaCombiner\0: Vanilla Folder
		xDeltaCombiner\1: Finished Product Folder
		xDeltaCombiner\(2+): Single Mod Folder
		xDeltaCombiner\#\Objects: GameMaker Objects location
		result\: resulting merges
		Cache\vanilla: currently unused, will be used for storing vanilla data.win files
		Cache\Logs: stores logs, goes by YYMMDDHHmm-TZ
		Cache\modNumbersCache.txt: used to pass off a variable value from GM3P to UTMTCLI during runtime. 
	Part 2.3: Known Issues and limitations
		Issue: Sprites that are not in the vanilla game may be out of order, except for the last mod applied.
		Issue: Backported mods don't compare correctly
		Issue: Sprites may not export or import at the right size (specifically observed with the Running Animations mod for Deltarune)
		Issue: Dumping and Importing fails if a custom output folder is specified.
		Issue: Logging doesn't save user input
		Limitation: Can only apply patches meant for the same version of the same game. Can't mix and match
		Limitation: If 2 mods modify the same object, only the changes for the last affect mod entered applies.
		Haven't implemented yet: Error Handling
		Haven't implemented yet: a way for the user to turn off verbosity
		Found a bug not written here? Use the Issues tab on our GameBanana or GitHub page to report it.
	Part 2.4: Tools used
		xDelta3 CLI for applying mods
		A custom version of UndertaleModTool for dumping and importing GameMaker Objects
		VS2022 to make and build this program
		This program was poorly written in C# .NET
		SHA1 Hashing was used for comparing files

Section 3: Installation and run Instructions
	Part 3.1: Common for both UIs
		3.1.1 Make sure you have the .NET runtime v8.0 or later installed
		3.1.2 Once downloaded the .zip folder, extract it to it's own folder
		3.1.3 To update your data from an older version, simply copy the "output" folder from the old version into folder of the new version. 
	Part 3.2: Running the console app:
		To Run the console app via command line/terminal:
			3.2.1 use the "ls" and "cd" commands to navigate to the extracted folder
			3.2.2 type ".\GM3P.exe" to run the program
		To Run the console app via File Explorer:
			3.2.3 navigate to the extracted folder
			3.2.4 double-click "GM3P.exe"
	Part 3.3: Running CLI commands:
		To Run CLI commands via command line/terminal:
			3.3.1 use the "ls" and "cd" commands to navigate to the extracted folder
			3.3.2 type ".\GM3P.exe *command* *args*" to run the program with your specified command
		To Run CLI commands via File Explorer:
			3.2.3 navigate to the extracted folder
			3.2.4 right-click "GM3P.exe" and hit "Create Shortcut"
			3.2.5 Right-click the newly created shortcut and hit "Properties"
			3.2.6 Under "target", encase the path in double quotes (") and enter your command after the path.
			3.2.7 Hit "Apply" then "Ok"
			3.2.8 Double-click the shortcut, it will open a terminal window that'll close once the command is completed