using System;
using System.IO;
using OpenGarrison.Client;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

using var game = new Game1(GameStartupMode.ServerLauncher);
game.Run();
