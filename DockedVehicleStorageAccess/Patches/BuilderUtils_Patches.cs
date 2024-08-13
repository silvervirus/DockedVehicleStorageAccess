using Common.Mod;
using HarmonyLib;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UWE;
using Task = System.Threading.Tasks.Task;

namespace DockedVehicleStorageAccess.Patches
{
    [HarmonyPatch(typeof(CraftData))]
    [HarmonyPatch("PreparePrefabIDCache")]
    class CraftData_PreparePrefabIDCache_Patch
    {
        private static bool initialized = false;

        private static void Postfix()
        {
            if (initialized)
                return;

            initialized = true;
            BuilderUtils.OnTechMappingInitialized();
        }
    }

    [HarmonyPatch(typeof(CraftData))]
    [HarmonyPatch("Get")]
    public class CraftData_Get_Patch
    {
        private static bool Prefix(ref ITechData __result, ref TechType techType, ref bool skipWarnings)
        {
            ITechData result = BuilderUtils.GetTechData(techType);
            if (result != null)
            {
                __result = result;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(CraftData))]
    [HarmonyPatch("GetPrefabForTechTypeAsync")]
    class CraftData_GetPrefabForTechType_Patch
    {
        private static Dictionary<TechType, GameObject> prefabCache = new Dictionary<TechType, GameObject>();

        private static async Task<GameObject> GetPrefabAsync(TechType techType)
        {
            if (prefabCache.TryGetValue(techType, out GameObject cachedPrefab))
                return cachedPrefab;

            GameObject prefab = await Task.Run(() => BuilderUtils.GetPrefab(techType));

            if (prefab != null)
                prefabCache[techType] = prefab;

            return prefab;
        }

        private static async Task<bool> PrefixAsync(Task<GameObject> __result, TechType techType, bool verbose)
        {
            GameObject prefab = await GetPrefabAsync(techType);

            if (prefab != null)
            {
                // Modify the task result to be the prefab
                __result = Task.FromResult(prefab);

                // Return false to indicate that the original method should not be executed
                return false;
            }

            // Return true to indicate that the original method should be executed
            return true;
        }





    }




    [HarmonyPatch(typeof(KnownTech))]
	[HarmonyPatch("Initialize")]
	class KnownTech_Initialize_Patch
	{
		private static bool initialized = false;

		private static void Postfix()
		{
			if (initialized)
			{
				return;
			}
			initialized = true;

			BuilderUtils.OnKnownTechInitialized();
		}
	}

	[HarmonyPatch(typeof(Language))]
	[HarmonyPatch("LoadLanguageFile")]
	class Language_LoadLanguageFile_Patch
	{
		private static void Postfix()
		{
			BuilderUtils.OnLanguageStringsInitialized();
		}
	}

	[HarmonyPatch(typeof(PrefabDatabase))]
	[HarmonyPatch("GetPrefabForFilename")]
	class PrefabDatabase_GetPrefabForFilename_Patch
	{
		private static bool Prefix(ref GameObject __result, string filename)
		{
			var prefab = BuilderUtils.GetPrefab(filename);
			if (prefab != null)
			{
				__result = prefab;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PrefabDatabase))]
	[HarmonyPatch("GetPrefabAsync")]
	class PrefabDatabase_GetPrefabAsync_Patch
	{
		private static bool Prefix(ref IPrefabRequest __result, string classId)
		{
			var prefab = BuilderUtils.GetPrefab(classId);
			if (prefab != null)
			{
				__result = new LoadedPrefabRequest(prefab);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PrefabDatabase))]
	[HarmonyPatch("LoadPrefabDatabase")]
	class PrefabDatabase_LoadPrefabDatabase_Patch
	{
		private static void Postfix()
		{
			BuilderUtils.OnPrefabDatabaseInitialized(GameObject);
		}
	}

	[HarmonyPatch(typeof(SpriteManager))]
	[HarmonyPatch("Get")]
	[HarmonyPatch(new Type[] { typeof(TechType) })]
	class SpriteManager_Get_Patch
	{
		private static bool Prefix(ref Atlas.Sprite __result, TechType techType)
		{
			CustomTechInfo techInfo = BuilderUtils.GetCustomTechData(techType);
			if (techInfo != null)
			{
				__result = techInfo.sprite;
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(TechTypeExtensions))]
	[HarmonyPatch("Contains")]
	class TechTypeExtensions_Contains_Patch
	{
		private static bool Prefix(ref bool __result, TechType techType)
		{
			if (BuilderUtils.HasCustomTechData(techType))
			{
				__result = true;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TechTypeExtensions))]
	[HarmonyPatch("TryGet")]
	class TechTypeExtensions_TryGet_Patch
	{
		private static bool Prefix(ref bool __result, TechType techType, ref string result)
		{
			var techData = BuilderUtils.GetCustomTechData(techType);
			if (techData != null)
			{
				result = techData.displayString;
				__result = true;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TechTypeExtensions))]
	[HarmonyPatch("Get")]
	class TechTypeExtensions_Get_Patch
	{
		private static bool Prefix(ref string __result, TechType techType)
		{
			var techData = BuilderUtils.GetCustomTechData(techType);
			if (techData != null)
			{
				__result = techData.displayString;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TechTypeExtensions))]
	[HarmonyPatch("GetOrFallback")]
	[HarmonyPatch(new Type[] { typeof(Language), typeof(TechType), typeof(TechType) })]
	class TechTypeExtensions_GetOrFallback1_Patch
	{
		private static bool Prefix(ref string __result, TechType techType)
		{
			var techData = BuilderUtils.GetCustomTechData(techType);
			if (techData != null)
			{
				__result = techData.displayString;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TechTypeExtensions))]
	[HarmonyPatch("GetOrFallback")]
	[HarmonyPatch(new Type[] { typeof(Language), typeof(string), typeof(TechType) })]
	class TechTypeExtensions_GetOrFallback2_Patch
	{
		private static bool Prefix(ref string __result, string key)
		{
			var techData = BuilderUtils.GetCustomTechDataByKey(key);
			if (techData != null)
			{
				__result = techData.displayString;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TechTypeExtensions))]
	[HarmonyPatch("GetOrFallback")]
	[HarmonyPatch(new Type[] { typeof(Language), typeof(TechType), typeof(string) })]
	class TechTypeExtensions_GetOrFallback3_Patch
	{
		private static bool Prefix(ref string __result, TechType techType)
		{
			var techData = BuilderUtils.GetCustomTechData(techType);
			if (techData != null)
			{
				__result = techData.displayString;
				return false;
			}
			return true;
		}
	}
}
