# mild-client
Mild is a very lightweight, very shitty multiplayer server framework via NodeJS and WebSockets. This is the Unity C# client code used to connect to a Mild server (https://github.com/radiatoryang/mild-server)

- technically works in WebGL, but kind of poorly, in my experience
- for simplicity, there is no authoritative server model, every client owns their own object -- and every client can technically send messages about every object -- there can, and will be, some disagreement between clients
- easily hackable, easy to send fake requests, easy to cheat and break, if any of your players ever care to do so
- ***do not use this for large commercial-scale releases***... this is intended more for small prototypes, small games, and trusted player communities

### Mild client, Unity C# integration tutorial
1. Clone or download this repo's project folder from GitHub. (NOTE: it already contains this Simple WebSockets package from Unity https://www.assetstore.unity3d.com/en/#!/content/38367 with an additional .jslib fix)
2. Copy the "Assets/Mild" folder into your Unity project folder, or just use this project folder as a base for your game.
3. Put Mild.cs in your scene somewhere, and edit the web address to point to your web server. (For a Heroku deployment, the address will look like "ws://your-app-name.herokuapp.com")
4. Define a "player prefab" for each player, assign the reference to the Mild script. These prefabs will get tracked and instantiated by Mild.cs -- for an example script, see MildPlayer.cs
5. Test and play. If you need help, see the included example scene. For testing, I recommend building a Win/OSX player, and running multiple windowed instances to test.

- Make sure "Run in Background" is enabled in your Player Settings.
- Edit the "tick rate" in Mild.cs to edit how often to broadcast updates. Higher tick rates mean more accuracy but more data being sent, and lower tick rates mean less frequent updates and less data being sent. 30 per second is fairly standard for games.
