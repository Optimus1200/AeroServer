# Requirements
- RPCS3 emulator
- A legitimate copy of Ace Combat: Infinity v2.11
- .NET 8.0 installed on your computer

# Getting Started
1. In Releases, download the .zip file and the .yml patch file.
2. Place `imported_patch.yml` in your RPCS3/patches folder.
3. If you haven't, create an RPCN account and sign in to RPCN.
4. Open RPCS3 and go to Manager -> Game Patches -> ACE COMBAT INFINITY and enable the patch.
5. In you RPCS3 game library, right-click the game and select `Create custom configuration`
6. Go to Settings -> Network -> paste `dev-wind.siliconstudio.co.jp=127.0.0.1` in IP/Hosts switches.
7. Make sure Network Status is set to `Connected` and PSN Status is `RPCN`.
8. Click `Apply` and `Save custom configuration`.
9. Extract the server .zip file you downloaded and run `LocalServer.exe`
10. Launch the game and keep pressing the X button past the title screen. The screen will go black for a second and then load to the main menu.

# Notes
- This server only listens and sends OK responses. It does not save game data nor send game data. It is not possible to save data with this server as it is.
- Sometimes the server may disconnect. For the majority of the time, it should keep you connected in the game.
