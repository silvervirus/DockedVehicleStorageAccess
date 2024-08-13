using Common.Mod;
using System.IO;
using UnityEngine;

namespace DockedVehicleStorageAccess
{
    public static class MoonpoolTerminalSaveManager
    {
        public static void CreateStorageContainer()
        {
            var userStorage = PlatformUtils.main.GetUserStorage();
            var slotPath = Path.Combine(SaveLoadManager.main.GetCurrentSlot(), "DockedVehicleStorageAccess");
            userStorage.CreateContainerAsync(slotPath);
        }

        public static void SaveDataToFile(string terminalId, int positionIndex)
        {
            var saveData = CreateSaveData(positionIndex);
            var saveFilePath = GetSaveDataPath(terminalId);

            // Save the data to file
            ModUtils.Save(saveData, saveFilePath);
        }

        public static void SavePosition(int positionIndex, string terminalId)
        {
            MoonpoolTerminalSaveData saveData = new MoonpoolTerminalSaveData();
            saveData.Position = positionIndex;

            string saveFile = GetSaveDataPath(terminalId);
            ModUtils.Save(saveData, saveFile);
        }

        public static int LoadPosition(string terminalId)
        {
            string saveFile = GetSaveDataPath(terminalId);
            int position = 0; // Initialize position variable

            ModUtils.LoadSaveData<MoonpoolTerminalSaveData>(saveFile, (saveData) =>
            {
                position = saveData != null ? saveData.Position : 0;
            });

            return position; // Return the loaded position
        }

        private static MoonpoolTerminalSaveData CreateSaveData(int positionIndex)
        {
            // Create the save data object
            MoonpoolTerminalSaveData saveData = new MoonpoolTerminalSaveData();

            // Set the position index
            saveData.Position = positionIndex;

            return saveData;
        }

        private static string GetSaveDataPath(string terminalId)
        {
            string saveFile = Path.Combine("DockedVehicleStorageAccess", terminalId + ".json");
            return saveFile;
        }
    }
}
