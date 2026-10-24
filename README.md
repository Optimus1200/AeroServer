# About

This is a mock server that allows you to play OPERATION ETERNAL LIBERATION.

# Cheat Engine

Right now, this server does not implement save-data backup, so you should not be using Cheat Engine with this server, as you risk corrupting save data.

# Supported Platforms
- Windows

# Requirements
- [ASP.NET Core Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (Hosting Bundle recommended for Windows)

# Setup
1. In Releases, download and extract `AeroServer-X.X-(OS).zip`.

2. Create a custom RPCS3 configuration for the game if you have not done so. Open RPCS3, 
right-click on the game in your library, and select `Create custom configuration...`.

3. Open the custom configuration settings and navigate to the `Network` tab.\
Set `Network Status` to `Connected`.\
Set `PSN Status` to `RPCN`.\
Set `IP/Hosts switches` to `dev-wind.siliconstudio.co.jp=127.0.0.1&&aci.vs765.nbgi-amnet.jp=127.0.0.1`.\
Click `Apply` and `Save custom configuration`.

4. Sign in to RPCN. In RPCS3, click the icon labelled `RPCN` and enter your credentials.

5. (Windows) Open the `AeroServer-X.X-(OS)` folder and launch `AeroServer-X.X-(OS).exe`.  
(Linux) Navigate to `AeroServer-X.X-(OS)` and run `sudo ./AeroServer-X.X-(OS)`.

6. Launch the game and reclaim the skies.

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

Many thanks to Killer0byte, JumpSuit and Volcano Water for contributing to the development of the server.

See the main project: https://github.com/The-OPERATIONS-Team/OPERATION-ETERNAL-LIBERATION

# Legal Disclaimer

This project is an independent, community-driven revival project and is **not** affiliated with, endorsed by, or otherwise connected to Bandai Namco Entertainment Inc. or any of the original rights holders of *Ace Combat Infinity*.

This project is developed strictly for **non-commercial, hobby, and preservation purposes**. It is, and will always remain, **freely available** via its official GitHub repository. Any distribution of this content in exchange for payment or other compensation is entirely unauthorized and is not endorsed by the -OPERATIONS- team. If you encounter such activity, we strongly encourage you to report it.

***

## Intellectual Property Notices

- **Ace Combat™** and **Ace Combat Infinity™** are intellectual properties of **Bandai Namco Entertainment Inc.** All rights reserved.
- All trademarks, copyrights, aircraft designations, manufacturer names, trade names, brand names, and visual imagery depicted in the original game remain the exclusive property of their respective owners and Bandai Namco Entertainment Inc.
- **PlayStation®** is a registered trademark of **Sony Interactive Entertainment Inc.**

> This project makes no claim of ownership over any of the above intellectual properties. All original assets, names, and likenesses are used solely for interoperability and non-commercial fan preservation purposes.
