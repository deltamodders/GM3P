README

Section 1: Operation Instructions
	
	Part 1.1: Console
		1.1.1 First Enter in the path of the vanilla data.win of the file. If you just want to dump and combine already prepared modded data.win files, enter "skip" into this field. Hit Enter. 
		1.1.2 Then enter in the amount of mods you want to operate on as an integer (e.g. "2" for 2 mods).
		1.1.3 One at a time, enter in the path of your patches, hitting enter in between each one.
		1.1.4 If you are patching multiple chapters, when it asks you to enter the patches, enter them in this order: ROOT, Chapter 1, Chapter 2, Chapter 3, etc. . If your mod doesn’t modify one or more of these, you can skip those when it’s their turn. Example: Let's say you want to patch a 2 chapter game with 3 mods. mod1 only modifies chapter 2, mod2 modifies the root and both chapters, and mod3 modifies chapters 1 and 2, but not root. You would enter them in this order: blank, mod2 root, blank, blank, mod2 ch1, mod3 ch1, mod1, mod2 ch2, mod3 ch2.
		1.1.5 Wait for GM3P to finish patching the data.win files
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
			OS: Windows 7 or later; Debian-based or Azure Linux
			CPU: 64-bit x86 processor with 2 or more cores
			Storage: 256MB
			RAM: 64MB
			Software: .NET 8.0 runtime (Windows and Linux); xDelta3 (Linux only)
		2.1.3: System Recommended
			OS: Windows 10 or 11
			CPU: Intel i5-2400 or later
			Storage: 2GB NvME SSD
			RAM: 2GB
	Part 2.2: output directory structure
		xDeltaCombiner\: The main folder, responsible for the patching and combining
		xDeltaCombiner\chapter#\0: Vanilla Folder
		xDeltaCombiner\chapter#\1: Finished Product Folder
		xDeltaCombiner\chapter#\(2+): Single Mod Folder
		xDeltaCombiner\chapter#\#\Objects: GameMaker Objects location
		result\: resulting modpacks and modsets
		Cache\: temp storage to help with various things
		Cache\vanilla: currently unused, will be used for storing vanilla data.win files
		Cache\Logs: stores logs, goes by YYMMDDHHmm-TZ
		Cache\running: passes off variable values from GM3P to UTMTCLI during runtime. 
	Part 2.3: Known Issues and limitations
		Issue: File size may increase due to texture page duplication
		Issue: Dumping and Importing fails if a custom output folder is specified.
		Issue: Logging doesn't save user input
		Limitation: Can only apply patches meant for the same version of the same game. Can't mix and match
		Limitation: If 2 mods modify the same object, only the changes for the last affect mod entered applies.
		Haven't implemented yet: a way for the user to turn off verbosity
		Found a bug not written here? Use the Issues tab on our GameBanana or GitHub page to report it.
	Part 2.4: Tools used
		xDelta3 CLI for applying mods
		A custom version of UndertaleModTool for dumping and importing GameMaker Objects
		VS2022 to make and build this program
		This program was poorly written in C#/.NET
		SHA1 Hashing was used for comparing files
		DRA used in installation script
	Part 2.5 Life Cycle Policy:
		When it comes to Alpha and Beta versions (v0.x.y), the support cycle is this:
		Stable versions will be supported until 48 hours after the next one comes out
		Pre-Release versions will be supported until 2 hours after the next one comes out, or 2 hours after the Stable version comes out.
		Dev versions are not supported
		There are 2 exceptions to this policy: v0.1.0 and the DeltaMOD Mod Manager
		For v0.1.0, support is limited to only those who needs a non-copyleft license. In addition bug fixes and other changes will never come to v0.1.0.
		For DeltaMOD, support is for whatever the latest version of GM3P that's compatible with whatever is the latest version of DeltaMOD. 

Section 3: Installation and run Instructions
	Part 3.1: Common for both UIs (Windows)
		3.1.1 Make sure you have the .NET runtime v8.0 or later installed
		3.1.2 Once downloaded the .zip folder, extract it to it's own folder
		3.1.3 To update your data from an older version, simply copy the "output" folder from the old version into folder of the new version. 
	Part 3.2: Running the console app (Windows):
		To Run the console app via command line/terminal:
			3.2.1 use the "ls" and "cd" commands to navigate to the extracted folder, or go to the extracted folder in File Explorer, right-click, and hit "Open in Terminal"
			3.2.2 type ".\GM3P.exe" to run the program
		To Run the console app via File Explorer:
			3.2.3 navigate to the extracted folder
			3.2.4 double-click "GM3P.exe"
	Part 3.3: Running CLI commands (Windows):
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
	Part 3.4: Running on Linux
			3.4.1 Extract the .zip file whereever you like
			3.4.1 Open up a new terminal window
			3.4.2 Make sure you have .NET runtime 8.0 or later installed. If you don't have it installed and you are on a distro with snap, you can install this using the command "sudo snap install dotnet"
			3.4.3 use the "ls" and "cd" commands to navigate to the extracted folder
			3.4.4 typing in the command "dotnet GM3P.dll" will launch the console app, for CLI, you can use "dotnet GM3P.dll *command* *args*"