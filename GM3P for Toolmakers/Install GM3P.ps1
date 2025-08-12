winget install Microsoft.DotNet.SDK.8
winget install CosimoMatteini.DRA
dra download --output ".\" --tag "v0.5.1" --select "GM3P.v0.5.1.zip" techy804/MassModPatcher
Expand-Archive ".\GM3P.v0.5.1.zip" -DestinationPath ".\" -Force
del ".\GM3P.v0.5.1.zip"