using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Common.Mod;
using Common.Utility;
using Nautilus.Assets;
using Nautilus.Assets.Gadgets;
using Nautilus.Crafting;
using Nautilus.Handlers;
using UnityEngine.UI;
using static CraftData;
using Debug = UnityEngine.Debug;
using ImageUtils = Common.Utility.ImageUtils;
using static HandReticle;
using PriorityQueueInternal;
using HarmonyLib;
using System.Runtime.InteropServices;
using System.Diagnostics.Eventing.Reader;
using Nautilus.Assets.PrefabTemplates;
using TMPro;


namespace DockedVehicleStorageAccess.Buildable
{
	[Serializable]
	public class VehicleStorageAccessSaveData
	{
		public bool Enabled = true;
		public bool Autosort = true;
	}

    public class VehicleStorageAccess : MonoBehaviour, IProtoEventListener
    {
       
        //public static bool Showlog => Mod.config.Showlogs;
        private static readonly Color PrimaryColor = new Color32(66, 134, 244, 255);
        private static readonly Color HiddenColor = new Color32(66, 134, 244, 20);
        private static readonly Type AutosortLockerType = Type.GetType("AutosortLockers.AutosortLocker, AutosortLockersSML", false, false);
        private static readonly FieldInfo AutosortLocker_container = AutosortLockerType?.GetField("container", BindingFlags.NonPublic | BindingFlags.Instance);

        private HashSet<TechType> excludedTechTypesSet = access.GetExcludedTechTypes();
        private List<TechType> excludedTechTypesList = access.excludedTechTypesSet.ToList();
        private bool initialized;
        private VehicleStorageAccessSaveData saveData;
        private bool extractingItems;
        private Constructable constructable;
        private StorageContainer container;
        private SubRoot subRoot;
        private VehicleDockingBay[] dockingBays = new VehicleDockingBay[0];
        private List<Vehicle> vehicles = new List<Vehicle>();

        private bool transferringToAutosorter;
        private List<Component> autosorters = new List<Component>();

        public static VehicleStorageAccess access;

        [SerializeField]
        private TextMeshProUGUI textPrefab;
        [SerializeField]
        private Image background;
        [SerializeField]
        private Image icon;
        [SerializeField]
        private TextMeshProUGUI text;
        [SerializeField]
        private Image seamothIcon;
        [SerializeField]
        private TextMeshProUGUI seamothCountText;
        [SerializeField]
        private Image exosuitIcon;
        [SerializeField]
        private TextMeshProUGUI exosuitCountText;
        [SerializeField]
        private CheckboxButton enableCheckbox;
        [SerializeField]
        private CheckboxButton autosortCheckbox;




        private void Awake()
        {
            constructable = GetComponent<Constructable>();
            container = GetComponent<StorageContainer>();
            subRoot = GetComponentInParent<SubRoot>();
            access = this;
        }

        private IEnumerator Start()
        {            
                while (true)
                {
                    if (initialized && constructable._constructed && enableCheckbox.toggled)
                    {
                        
                        GetDockingBays();
                     LoadExclusionTechTypes();
                    yield return TryExtractItems();

                        if (Mod.config.UseAutosortMod)
                        {
                            if (!extractingItems)
                            {
                                yield return new WaitForSeconds(Mod.config.AutosortTransferInterval);
                                yield return TryMoveToAutosorter();
                            }
                        }
                    }
                    yield return new WaitForSeconds(Mod.config.CheckVehiclesInterval);
                }
            
        }




        public HashSet<TechType> GetExcludedTechTypes()
        {
            // Ensure the variable name matches the actual list
            return ConvertToTechTypeSet(excludedTechTypesList);
        }

        public void LoadExclusionTechTypes()
        {
            string pluginFolderPath = Path.Combine(BepInEx.Paths.PluginPath, "DockedVehicleStorageAccessSML");
            string filePath = Path.Combine(pluginFolderPath, "exclusionTechTypes.json");

            if (File.Exists(filePath))
            {
                string jsonContent = File.ReadAllText(filePath);
                ExclusionTechTypesData exclusionData = JsonUtility.FromJson<ExclusionTechTypesData>(jsonContent);

                if (exclusionData != null)
                {
                    try
                    {
                        List<TechType> techTypes = new List<TechType>();

                        if (exclusionData.exclusionTechTypes != null)
                        {
                            foreach (var typeName in exclusionData.exclusionTechTypes)
                            {
                                // Debug log for each type name being processed
                                Debug.Log($"Attempting to parse: {typeName}");

                                if (Enum.TryParse(typeName, true, out TechType techType))
                                {
                                    techTypes.Add(techType);
                                }
                                else
                                {
                                    // Log warning for failed parse
                                    Debug.LogWarning($"Failed to parse TechType: {typeName}");
                                }
                            }

                            // Convert the list of TechType enums to a HashSet
                            excludedTechTypesSet = ConvertToTechTypeSet(techTypes);
                            Debug.Log("Exclusion tech types loaded: " + string.Join(", ", excludedTechTypesSet));
                        }
                    }
                    catch (Exception ex)
                    {
                        //if (Showlog) return;
                        Debug.LogError("Failed to parse exclusion tech types: " + ex.Message);
                    }
                }
                else
                {
                   // if (Showlog) return;
                    Debug.LogError("Failed to parse exclusion tech types JSON file.");
                }
            }
            else
            {
                //if (Showlog) return;
                Debug.LogError("Exclusion tech types JSON file not found at: " + filePath);
            }
        }









        public HashSet<TechType> ConvertToTechTypeSet(List<TechType> techTypes)
        {
            // Return an empty set if the input list is null or empty
            if (techTypes == null || techTypes.Count == 0)
            {
                return new HashSet<TechType>();
            }

            HashSet<TechType> exclusionTechTypes = new HashSet<TechType>();

            foreach (var techType in techTypes)
            {
                if (Enum.IsDefined(typeof(TechType), techType))
                {
                    exclusionTechTypes.Add(techType);
                }
            }

            return exclusionTechTypes;
        }
        private void OnDisable()
        {
           
                RemoveDockingBayListeners();
                StopAllCoroutines();
            
        }


        private void GetDockingBays()
        {
            RemoveDockingBayListeners();

            if (subRoot == null)
            {
                Debug.LogError("subRoot is null. Cannot get docking bays.");
                return;
            }

            dockingBays = subRoot.GetComponentsInChildren<VehicleDockingBay>();

            if (dockingBays == null || dockingBays.Length == 0)
            {
                Debug.LogError("No VehicleDockingBay components found.");
            }
            else
            {
                Debug.Log($"Found {dockingBays.Length} VehicleDockingBay components.");
            }

            AddDockingBayListeners();
            UpdateDockedVehicles();

            if (Mod.config.UseAutosortMod)
            {
                autosorters = subRoot.GetComponentsInChildren(AutosortLockerType).ToList();
                Debug.Log($"Found {autosorters.Count} autosorters.");
            }
        }



        private void AddDockingBayListeners()
        {
            foreach (var dockingBay in dockingBays)
            {
                dockingBay.onDockedChanged += OnDockedVehicleChanged;
            }
        }

        private void RemoveDockingBayListeners()
        {
            foreach (var dockingBay in dockingBays)
            {
                dockingBay.onDockedChanged -= OnDockedVehicleChanged;
            }
        }

        private void UpdateDockedVehicles()
        {
            vehicles.Clear();
            foreach (var dockingBay in dockingBays)
            {
                var vehicle = dockingBay.GetDockedVehicle();
                if (vehicle != null)
                {
                    vehicles.Add(vehicle);
                }
            }
        }


        private void OnDockedVehicleChanged()
        {
            UpdateDockedVehicles();
            StartCoroutine(TryExtractItems());
        }


        private IEnumerator TryExtractItems()
        {
            if (extractingItems || !enableCheckbox.toggled)
            {
                yield break;
            }

            extractingItems = true; // Prevent concurrent extraction
            bool extractedAnything = false;
            Dictionary<string, int> extractionResults = new Dictionary<string, int>();

            List<Vehicle> localVehicles = vehicles.ToList();
            foreach (var vehicle in localVehicles)
            {
                var vehicleName = vehicle.GetName();
                extractionResults[vehicleName] = 0;

                // Process StorageContainers
                foreach (var vehicleContainer in vehicle.gameObject.GetComponentsInChildren<StorageContainer>()
                    .Select(x => x.container).ToList())
                {
                    if (vehicleContainer == null || vehicleContainer.Count() == 0) continue;

                    foreach (var item in vehicleContainer.ToList())
                    {
                        if (item == null) continue;

                        var itemTechType = item.item.GetTechType();
                        if (excludedTechTypesSet.Contains(itemTechType)) continue;

                        if (container.container.HasRoomFor(itemTechType))
                        {
                            bool removeSuccess = vehicleContainer.RemoveItem(item.item);
                            if (removeSuccess)
                            {
                                bool addSuccess = container.container.AddItem(item.item) != null;

                                if (addSuccess)
                                {
                                    extractionResults[vehicleName]++;
                                    if (!extractedAnything)
                                    {
                                        // ErrorMessage.AddDebug("Extracting items from vehicle storage...");
                                    }
                                    extractedAnything = true;
                                    yield return new WaitForSeconds(Mod.config.ExtractInterval);
                                }
                                else
                                {
                                    // Debug.LogError($"Failed to add item: {item.item} to main container.");
                                }
                            }
                            else
                            {
                                // Debug.LogError($"Failed to remove item: {item.item} from vehicle container.");
                            }
                        }
                        else
                        {
                            // Debug.LogWarning($"Main container does not have room for item: {itemTechType}.");
                        }
                    }
                }

                // Process Seamoth Storage Modules
                foreach (var module in GetSeamothStorage(vehicle))
                {
                    var seamothStorageContainer = module.GetComponent<SeamothStorageContainer>();
                    if (seamothStorageContainer == null)
                    {
                        // Debug.LogWarning($"SeamothStorageContainer component not found on module: {module.name}");
                        continue;
                    }

                    foreach (var pickupable in seamothStorageContainer.container.ToList())
                    {
                        if (!enableCheckbox.toggled) break;

                        if (pickupable == null)
                        {
                            // Debug.LogWarning("Pickupable item is null.");
                            continue;
                        }

                        var pickupableTechType = pickupable.item.GetTechType();
                        if (excludedTechTypesSet.Contains(pickupableTechType)) continue;

                        if (container.container.HasRoomFor(pickupableTechType))
                        {
                            bool removeSuccess = seamothStorageContainer.container.RemoveItem(pickupable.item);
                            if (removeSuccess)
                            {
                                bool addSuccess = container.container.AddItem(pickupable.item) != null;

                                if (addSuccess)
                                {
                                    extractionResults[vehicleName]++;
                                    if (!extractedAnything)
                                    {
                                        // ErrorMessage.AddDebug("Extracting items from Seamoth storage...");
                                    }
                                    extractedAnything = true;
                                    yield return new WaitForSeconds(Mod.config.ExtractInterval);
                                }
                                else
                                {
                                    // Debug.LogError($"Failed to add Pickupable: {pickupable} to main container.");
                                }
                            }
                            else
                            {
                                // Debug.LogError($"Failed to remove Pickupable: {pickupable} from Seamoth container.");
                            }
                        }
                        else
                        {
                            // Debug.LogWarning($"Main container does not have room for Pickupable: {pickupableTechType}.");
                        }
                    }

                    if (!enableCheckbox.toggled) break;
                }

                if (extractedAnything)
                {
                    NotifyExtraction(extractionResults);
                }
            }

            extractingItems = false; // Reset the flag after operation

            // After extracting items, attempt to move them to the Autosorter if applicable
            StartCoroutine(TryMoveToAutosorter());
        }
        private List<GameObject> GetSeamothStorage(Vehicle vehicle)
        {
            var results = new List<GameObject>();

            if (vehicle is SeaMoth seaMoth)
            {
                //Debug.Log("Vehicle is a SeaMoth.");

                if (seaMoth.modules != null)
                {
                    var equipment = seaMoth.modules.GetEquipment();
                    while (equipment.MoveNext())
                    {
                        var module = equipment.Current.Value;
                        if (module?.item != null)
                        {
                            var storageRoot = module.item.transform.Find("StorageRoot");
                            if (storageRoot != null)
                            {
                                // Debug.Log($"Found StorageRoot in module: {module.item.name}");

                                // Only add the module if it has a SeamothStorageContainer component
                                var seamothStorageContainer = module.item.GetComponent<SeamothStorageContainer>();
                                if (seamothStorageContainer != null)
                                {
                                    // Debug.Log($"Module {module.item.name} has SeamothStorageContainer.");

                                    foreach (Transform child in storageRoot)
                                    {
                                        if (child.GetComponent<Pickupable>() != null)
                                        {
                                            results.Add(module.item.gameObject);
                                            //Debug.Log($"Added module {module.item.name} with StorageRoot containing Pickupable.");
                                            break; // No need to process more children if the module is already added
                                        }
                                    }
                                }
                                else
                                {
                                    //Debug.LogWarning($"SeamothStorageContainer component not found on module: {module.item.name}");
                                }
                            }
                            else
                            {
                                // Debug.LogWarning($"No StorageRoot found in module: {module.item.name}");
                            }
                        }
                        else
                        {
                            // Debug.LogWarning($"Module item is null in module: {module.item.name}");
                        }
                    }
                }
                else
                {
                    // Debug.LogWarning("SeaMoth modules are not properly initialized.");
                }
            }
            else
            {
                //Debug.LogWarning("Vehicle is not a SeaMoth.");
            }

            return results;
        }


        // Exclusive for Autosort integration
        private IEnumerator TryMoveToAutosorter()
        {
            if (autosorters.Count == 0)
            {
                yield break;
            }
            if (!autosortCheckbox.toggled || !enableCheckbox.toggled)
            {
                yield break;
            }
            if (transferringToAutosorter)
            {
                yield break;
            }

            var items = container.container.ToList();
            bool couldNotAdd = false;
            int itemsTransferred = 0;
            foreach (var item in items)
            {
                foreach (var autosorter in autosorters)
                {
                    if (!enableCheckbox.toggled || !autosortCheckbox.toggled)
                    {
                        break;
                    }

                    var aContainer = (StorageContainer)AutosortLocker_container.GetValue(autosorter);

                    if (aContainer.container.HasRoomFor(item.item))
                    {
                        var success = aContainer.container.AddItem(item.item);
                        if (success != null)
                        {
                            itemsTransferred++;
                            if (!transferringToAutosorter)
                            {
                                ErrorMessage.AddDebug("Transferring items to Autosorter...");
                            }
                            transferringToAutosorter = true;
                        }
                        else
                        {
                            couldNotAdd = true;
                            break;
                        }
                    }
                    else
                    {
                        couldNotAdd = true;
                        break;
                    }
                    yield return new WaitForSeconds(Mod.config.AutosortTransferInterval);
                }

                if (couldNotAdd || !enableCheckbox.toggled || !autosortCheckbox.toggled)
                {
                    break;
                }
            }

            if (itemsTransferred > 0)
            {
                ErrorMessage.AddDebug("Transfer complete");
            }
            transferringToAutosorter = false;
        }

        private void UpdateText()
        {
            if (text == null)
            {
                Debug.LogError("text is null in UpdateText.");
                return;
            }

            var dockingBayCount = dockingBays?.Length ?? 0;

            if (subRoot is BaseRoot)
            {
                text.text = dockingBayCount > 0 ? ("Moonpools: " + dockingBayCount) : "No Moonpools";
            }
            else
            {
                text.text = "Cyclops Docking Bay";
            }

            if (Mod.config.UseAutosortMod)
            {
                autosortCheckbox = autosortCheckbox ?? throw new NullReferenceException("autosortCheckbox is null.");
                autosortCheckbox.isEnabled = autosorters.Count > 0;
                text.text += autosorters.Count == 0 ? "\nNo Autosorters" : "";
            }

            int seamothCount = 0;
            int exosuitCount = 0;
            foreach (var vehicle in vehicles)
            {
                seamothCount += (vehicle is SeaMoth ? 1 : 0);
                exosuitCount += (vehicle is Exosuit ? 1 : 0);
            }

            seamothCountText = seamothCountText ?? throw new NullReferenceException("seamothCountText is null.");
            exosuitCountText = exosuitCountText ?? throw new NullReferenceException("exosuitCountText is null.");
            seamothIcon = seamothIcon ?? throw new NullReferenceException("seamothIcon is null.");
            exosuitIcon = exosuitIcon ?? throw new NullReferenceException("exosuitIcon is null.");

            seamothCountText.text = seamothCount > 1 ? "x" + seamothCount : "";
            exosuitCountText.text = exosuitCount > 1 ? "x" + exosuitCount : "";

            seamothIcon.color = seamothCount > 0 ? PrimaryColor : HiddenColor;
            exosuitIcon.color = exosuitCount > 0 ? PrimaryColor : HiddenColor;

            if (!enableCheckbox.toggled)
            {
                text.text += "\n\n<color=red>DISABLED</color>";
            }
            else if (extractingItems)
            {
                text.text += "\n\n<color=green>EXTRACTING...</color>";
            }
            else if (Mod.config.UseAutosortMod && autosorters.Count == 0 && transferringToAutosorter)
            {
                text.text += "\n\n<color=green>TRANSFERRING...</color>";
            }
            else
            {
                text.text += "\n\n<color=green>READY</color>";
            }
        }


        private void Update()
        {
            if (!initialized && constructable._constructed && transform.parent != null)
            {
                Initialize();
            }

            if (!initialized || !constructable._constructed)
            {
                return;
            }

           

            UpdateText();
        }







        private void NotifyExtraction(Dictionary<string, int> extractionResults)
		{
			List<string> messageEntries = new List<string>();

			foreach (var entry in extractionResults)
			{
				messageEntries.Add(entry.Key + " x" + entry.Value);
			}

			string message = string.Format("Extracted items from vehicle{0}: {1}", messageEntries.Count > 0 ? "s" : "", string.Join(", ", messageEntries.ToArray()));
			ErrorMessage.AddDebug(message);
		}

        // Exclusive for Autosort integration

        private void Initialize()
        {
            Debug.Log("Initializing...");

            background.gameObject.SetActive(true);
            icon.gameObject.SetActive(true);
            text.gameObject.SetActive(true);
            text.richText = true;

            var lockerScreenTexture = Utilities.GetTexture("LockerScreen");

            background.sprite = Common.Utility.ImageUtils.TextureToSprite(lockerScreenTexture, default(Vector2), 100f, SpriteMeshType.FullRect);
            icon.sprite = Common.Utility.ImageUtils.TextureToSprite(Utilities.GetTexture("Receptacle"), default(Vector2), 100f, SpriteMeshType.FullRect);
            seamothIcon.sprite = Common.Utility.ImageUtils.TextureToSprite(Utilities.GetTexture("Seamoth"), default(Vector2), 100f, SpriteMeshType.FullRect);
            exosuitIcon.sprite = Common.Utility.ImageUtils.TextureToSprite(Utilities.GetTexture("Exosuit"), default(Vector2), 100f, SpriteMeshType.FullRect);

            // Initialize enableCheckbox
            enableCheckbox.toggled = saveData == null || saveData.Enabled;
            enableCheckbox.transform.localPosition = new Vector3(0f, -104f);
            enableCheckbox.Initialize();
            if (Mod.config.UseAutosortMod)
            {
                autosortCheckbox.toggled = saveData != null ? saveData.Autosort : true;
                autosortCheckbox.transform.localPosition = new Vector3(0, -104 + 19);
                autosortCheckbox.Initialize();
            }

            subRoot = gameObject.GetComponentInParent<SubRoot>();

            initialized = true;

            GetDockingBays();
            UpdateDockedVehicles();
            UpdateText();
        }



        [System.Serializable]
        private class ExclusionTechTypesData
        {
            public List<string> exclusionTechTypes;
        }



        public void OnProtoSerialize(ProtobufSerializer serializer)
        {
            var userStorage = PlatformUtils.main.GetUserStorage();
            userStorage.CreateContainerAsync(Path.Combine(SaveLoadManager.main.GetCurrentSlot(), "DockedVehicleStorageAccess"));

            var saveDataFile = GetSaveDataPath();
            saveData = CreateSaveData();
            ModUtils.Save(saveData, saveDataFile);
        }

        private VehicleStorageAccessSaveData CreateSaveData()
        {
            var saveData = new VehicleStorageAccessSaveData
            {
                Enabled = enableCheckbox.toggled,
                Autosort = Mod.config.UseAutosortMod 
            };

            return saveData;
        }

        public void OnProtoDeserialize(ProtobufSerializer serializer)
        {
            var saveDataFile = GetSaveDataPath();
            ModUtils.LoadSaveData<VehicleStorageAccessSaveData>(saveDataFile, (data) =>
            {
                saveData = data;
                initialized = false;
            });
        }

        public string GetSaveDataPath()
        {
            var prefabIdentifier = GetComponent<PrefabIdentifier>();
            var id = prefabIdentifier.Id;

            var saveFile = Path.Combine("DockedVehicleStorageAccess", id + ".json");
            return saveFile;
        }

        internal class VehicleStorageAccessBuildable
        {
            public static PrefabInfo Info { get; private set; }
            public static TextMeshProUGUI textPrefab;

            public static void Patch()
            {
                //Debug.Log("Starting AutoIO Patch method.");

                Info = Utilities.CreatePrefabInfo(
                    "DockedVehicleStorageAccess",
                    "Docked Vehicle Storage Access",
                    "Wall locker that extracts items from any docked vehicle in the moonpool or cyclops.",
                    Common.Mod.Utilities.GetSprite("StorageAccess")
                );
                UWE.CoroutineHost.StartCoroutine(SetTextMeshProPrefab());

                var customPrefab = new CustomPrefab(Info);
                var clonePrefab = new CloneTemplate(Info, TechType.SmallLocker);

                clonePrefab.ModifyPrefab += obj =>
                {
                    VehicleStorageAccess vehicleStorageAccess = obj.AddComponent<VehicleStorageAccess>();
                    vehicleStorageAccess.textPrefab = UnityEngine.Object.Instantiate(obj.GetComponentInChildren<TextMeshProUGUI>());
                    Transform transform = obj.transform.Find("Label");

                    UnityEngine.Object.DestroyImmediate(transform.gameObject);

                    Transform transform2 = obj.transform.Find("TriggerCull");

                    UnityEngine.Object.DestroyImmediate(transform2.gameObject);

                    Nautilus.Utility.PrefabUtils.AddStorageContainer(obj, "Vehicle Storage Access", "VSA", Mod.config.LockerWidth, Mod.config.LockerHeight, true);
                    MeshRenderer[] componentsInChildren = obj.GetComponentsInChildren<MeshRenderer>();
                    MeshRenderer[] array = componentsInChildren;
                    foreach (MeshRenderer meshRenderer in array)
                    {
                        meshRenderer.material.color = Color.blue;
                    }
                    Canvas canvas = LockerPrefabShared.CreateCanvas(obj.transform);
                    vehicleStorageAccess.background = LockerPrefabShared.CreateBackground(canvas.transform);
                    vehicleStorageAccess.icon = LockerPrefabShared.CreateIcon(vehicleStorageAccess.background.transform, PrimaryColor, 15);
                    vehicleStorageAccess.text = LockerPrefabShared.CreateText(vehicleStorageAccess.background.transform, vehicleStorageAccess.textPrefab, PrimaryColor, -40, 10, "");
                    vehicleStorageAccess.seamothIcon = LockerPrefabShared.CreateIcon(vehicleStorageAccess.background.transform, PrimaryColor, 80);
                    vehicleStorageAccess.seamothCountText = LockerPrefabShared.CreateText(vehicleStorageAccess.background.transform, vehicleStorageAccess.textPrefab, PrimaryColor, 55, 10, "none");
                    vehicleStorageAccess.exosuitIcon = LockerPrefabShared.CreateIcon(vehicleStorageAccess.background.transform, PrimaryColor, 80);
                    vehicleStorageAccess.exosuitCountText = LockerPrefabShared.CreateText(vehicleStorageAccess.background.transform, vehicleStorageAccess.textPrefab, PrimaryColor, 55, 10, "none");
                    vehicleStorageAccess.seamothIcon.rectTransform.anchoredPosition += new Vector2(-23f, 0f);
                    vehicleStorageAccess.seamothCountText.rectTransform.anchoredPosition += new Vector2(-23f, 0f);
                    vehicleStorageAccess.exosuitIcon.rectTransform.anchoredPosition += new Vector2(23f, 0f);
                    vehicleStorageAccess.exosuitCountText.rectTransform.anchoredPosition += new Vector2(23f, 0f);
                    if (Mod.config.UseAutosortMod)
                    {
                        vehicleStorageAccess.autosortCheckbox = CheckboxButton.CreateCheckbox(vehicleStorageAccess.background.transform, PrimaryColor, vehicleStorageAccess.textPrefab, "Autosort");
                        vehicleStorageAccess.autosortCheckbox.transform.localPosition = new Vector3(0, -104 + 19);
                    }
                    vehicleStorageAccess.enableCheckbox = CheckboxButton.CreateCheckbox(vehicleStorageAccess.background.transform, PrimaryColor, vehicleStorageAccess.textPrefab, "Enabled");
                    vehicleStorageAccess.enableCheckbox.transform.localPosition = new Vector3(0f, -104f);

                    

                    // Debug.Log("h");
                };
                // Debug.Log("i");
                if (Mod.config.EasyBuild)
                {
                    customPrefab.SetRecipeFromJson(RamuneLib.Utils.JsonUtils.GetJsonRecipe("EZVehicleStorage"));
                }
                else
                {
                    customPrefab.SetRecipeFromJson(RamuneLib.Utils.JsonUtils.GetJsonRecipe("VehicleStorage"));
                }

                if (Mod.config.UnlockedAtStart)
                {
                    KnownTechHandler.UnlockOnStart(Info.TechType);
                }
                else
                {
                    customPrefab.SetUnlock(TechType.Workbench);
                }
                // Debug.Log("j");
                customPrefab.SetGameObject(clonePrefab);
                customPrefab.SetPdaGroupCategory(TechGroup.InteriorModules, TechCategory.InteriorModule);
                customPrefab.Register();
                Debug.Log("Finished DockedStorageAccesslocker Patch method.");
                // Debug.Log("k");
            }

            public static IEnumerator SetTextMeshProPrefab()
            {
                CoroutineTask<GameObject> task = CraftData.GetBuildPrefabAsync(TechType.SmallLocker);

                while (task.MoveNext())
                {
                    yield return task.Current;
                }

                GameObject prefab = task.GetResult();
                if (prefab != null)
                {
                    textPrefab = prefab.GetComponentInChildren<TextMeshProUGUI>();
                    if (textPrefab == null)
                    {
                        Debug.LogError("TextMeshProUGUI component not found in prefab.");
                    }
                }
                else
                {
                    Debug.LogError("Failed to get SmallLocker prefab.");
                }

            }

           
        }
       

    }
}
