using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if DEBUG
using KramaxReloadExtensions;
#endif

namespace AutoAsparagus
{
    // Be sure to target .NET 3.5 or you'll get some bogus error on startup!

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class InstallChecker : MonoBehaviour
    {
        protected void Start()
        {
            const string modName = "GravityTurn";
            const string expectedPath = "GravityTurn/Plugins";
            // Search for this mod's DLL existing in the wrong location. This will also detect duplicate copies because only one can be in the right place.
            var assemblies = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name).Where(a => a.url != expectedPath);

            if (assemblies.Any())
            {
                var badPaths = assemblies.Select(a => a.path).Select(
                    p => Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString().Replace('/', Path.DirectorySeparatorChar))
                );
                PopupDialog.SpawnPopupDialog("Incorrect " + modName + " Installation",
                    modName + " has been installed incorrectly and will not function properly. All files should be located in KSP/GameData/" +
                    expectedPath + ". Do not move any files from inside that folder.\n\nIncorrect path(s):\n" +
                    String.Join("\n", badPaths.ToArray()),
                    "OK", false, HighLogic.Skin);
            }
            if (Versioning.version_major < 1 || !Versioning.VersionString.StartsWith("1.0.5"))
            {
                PopupDialog.SpawnPopupDialog("GravityTurn Compatibility", "GravityTurn is optimized for version 1.0.5\n" +
                    "Please either update your game, or expect some problems.  Found your game version " + Versioning.VersionString, "OK", false, HighLogic.Skin);
            }
        }
    }
}
