# About

This is a mock server that aims to replicate the functionality of the Wind API to make the game work. Included in the Release is a custom build of RPCS3 by Killer0byte that allows the game to properly read .tss files, as well as a built-in patch to unlock all DLC and infinite fuel.

RPCS3 Fork Source: https://github.com/The-OPERATIONS-Team/OPERATION-ETERNAL-LIBERATION

# Cheat Engine

Right now, this server does not implement save-data backup, so you should not be using Cheat Engine with this server, as you risk corrupting save data.

# Supported Platforms
- Windows

# Requirements
- [ASP.NET Core Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (Hosting Bundle recommended for Windows)
- [PS3 Firmware](https://www.playstation.com/en-us/support/hardware/ps3/system-software/)
- A legitimate copy of Ace Combat: Infinity v2.11 and your .RAP license file.

# Setup
1. In Releases, download `AeroServer-X.X-(OS).zip` and `RPCS3-Title-Small-Storage-support-7e250c5-2.zip`.

2. Extract the .zip files.
NOTE: The game patch and custom configuration have already been applied in the RPCS3 build in advance. It is not recommended to change your custom game configuration's network settings unless necessary.

3. Open the `RPCS3-Title-Small-Storage-support-7e250c5-2` folder and launch `rpcs3.exe`.

4. Install PS3 Firmware: Select `File` -> `Install Firmware` -> select your `PS3UPDAT.PUP` file.

5. Install Game: Select `File` -> `Add Games` -> select your game folder.

6. Install License: Select `File` -> `Install Packages/Raps/Edats` -> select your .RAP file.

7. Click the icon labelled `RPCN` and sign in to RPCN.

8. (Windows) Open the `AeroServer-X.X-(OS)` folder and launch `AeroServer-X.X-(OS).exe`.  
(Linux) Navigate to `AeroServer-X.X-(OS)` and run `sudo ./AeroServer-X.X-(OS)`.

9. Upon launching the server for the first time, if you see alternating yellow and green lines, then that is a good sign. The server is successfully downloading the missing files it needs to feed the game.

10. Launch the game and reclaim the skies.

# Build

## Windows
```
dotnet build
dotnet run
```

## Linux
```
dotnet build
sudo dotnet run
```

# Credits

Many thanks to Killer0byte for providing the custom RPCS3 build, and many thanks to and JumpSuit and Volcano Water for contributing to the development of the server.

# Legal Disclaimer

This project is an independent, community-driven revival project and is **not** affiliated with, endorsed by, or otherwise connected to Bandai Namco Entertainment Inc. or any of the original rights holders of *Ace Combat Infinity*.

This project is developed strictly for **non-commercial, hobby, and preservation purposes**. It is, and will always remain, **freely available** via its official GitHub repository. Any distribution of this content in exchange for payment or other compensation is entirely unauthorized and is not endorsed by the -OPERATIONS- team. If you encounter such activity, we strongly encourage you to report it.

***

## Intellectual Property Notices

- **Ace Combat™** and **Ace Combat Infinity™** are intellectual properties of **Bandai Namco Entertainment Inc.** All rights reserved.
- All trademarks, copyrights, aircraft designations, manufacturer names, trade names, brand names, and visual imagery depicted in the original game remain the exclusive property of their respective owners and Bandai Namco Entertainment Inc.
- **PlayStation®** is a registered trademark of **Sony Interactive Entertainment Inc.**

> This project makes no claim of ownership over any of the above intellectual properties. All original assets, names, and likenesses are used solely for interoperability and non-commercial fan preservation purposes.
