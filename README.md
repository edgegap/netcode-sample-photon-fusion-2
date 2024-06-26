# Photon Fusion 2 sample project

This guide will help you create a headless server on Edgegap for a Unity project using [Photon Fusion 2]( https://doc.photonengine.com/en-us/fusion/current/getting-started/fusion-intro) as its networking solution. You will need an account with both Edgegap and Photon for this.

We will use the sample project [Asteroid Simple (Host)](https://doc.photonengine.com/fusion/current/game-samples/fusion-asteroids) from Photon Fusion as an example.

## Tutorial

To test this sample:

- Clone the repo via the `git clone [URL]` command;
- In the Unity editor, set these values with your own:
    - In `PhotonAppSettings.asset`: `App Id Fusion`;
    - In `EdgegapConfig.asset`: `App Name`, `Version`, and `Api Token`. Keep the "token" keyword;
    - In `StartMenu.cs`: `serverPort` *(OPTIONAL, default value: 5050)*
- Make sure both scenes under `Assets/Asteroids-Host-Simple/Scenes` are included in the build settings.
- Create a new app version with the Edgegap plugin. Make sure to use the same app name, version tag and server port value from the previous steps. The protocol type is `UDP`. Click on `Build and Push`;
- Launch the game in the editor, enter a room name then click on `Start Edgegap`.

The game will search for an available room with the specified name. If none are found, an instance of the server application will be deployed automatically. After a short moment, the client will successfully connect to the server.

You can see the full documentation [here](https://docs.edgegap.com/docs/sample-projects/photon-fusion-2-on-edgegap).
