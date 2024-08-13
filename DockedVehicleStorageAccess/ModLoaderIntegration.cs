using BepInEx;
using System;

namespace DockedVehicleStorageAccess
{
   

    public  class QPatch : BaseUnityPlugin
    {
        public const String PLUGIN_GUID = "DockedVehicleStorageAccessSML";
        public const String PLUGIN_NAME = "DockedVehicleStorageAccessSML";
        public const String PLUGIN_VERSION = "1.0.0";
        public void Start()
		{
			Mod.Patch("BepInEx/plugins/DockedVehicleStorageAccessSML");
            
           

        }
	}
}