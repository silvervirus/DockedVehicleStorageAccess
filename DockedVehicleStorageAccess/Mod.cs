using System;
using System.Reflection;
using Common.Mod;
using HarmonyLib;
using Debug = UnityEngine.Debug;
using DockedVehicleStorageAccess.Buildable;
using System.IO;



namespace DockedVehicleStorageAccess
{ 
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.HardDependency)]
    internal  class Mod : BaseUnityPlugin
    {
        public const String PLUGIN_GUID = "DockedVehicleStorageAccessSML";
        public const String PLUGIN_NAME = "DockedVehicleStorageAccessSML";
        public const String PLUGIN_VERSION = "1.0.0";
        public static Config config;
      
        private static string modDirectory;

		public void Start()
		{
			Debug.Log("Starting patching");

           
            LoadConfig();

            AddBuildables();


            new Harmony("com.DockedVehicleStorageAccessSML.mod").PatchAll(Assembly.GetExecutingAssembly()); 
			

			Debug.Log("Patched");
		}
        private static string GetLaunchDirectory()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }
        public static void AddBuildables()
        {
           VehicleStorageAccess.VehicleStorageAccessBuildable.Patch();
        }

        public static string GetModPath()
        {
            return GetLaunchDirectory();
        }


        public static string GetAssetPath(string filename)
        {
            return Path.Combine(GetModPath(), "Assets", filename + ".png");
        }
        public enum AutosortLockersSML
        {
            Enabled,
            Disabled
        }
        private static void LoadConfig()
        {
            string configFilePath = Path.Combine(GetModPath(), "config.json");
            config = ModUtils.LoadConfig<Config>(configFilePath);
           

                Debug.Log("Running in standalone mode.");
           
        }
    }
}