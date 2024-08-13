using Common.Mod;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;
using System.Diagnostics.Eventing.Reader;
using DockedVehicleStorageAccess.Patches;

namespace DockedVehicleStorageAccess
{


    public class MoonpoolTerminalController : MonoBehaviour
    {

        private static Vector3[] Positions = {
            new Vector3(4.96f, 1.4f, 3.23f),
            new Vector3(-4.96f, 1.4f, 3.23f),
            new Vector3(-4.96f, 1.4f, -3.23f),
            new Vector3(4.96f, 1.4f, -3.23f)
        };

        private static float[] Angles = {
            42.5f,
            -42.5f,
            180 + 42.5f,
            180 - 42.5f,
        };


        public static int positionIndex;
        private bool initialized;



        public void Awake()
        {
            Debug.Log("MoonpoolTerminalController Awake() called.");

            // Attempt to retrieve the Canvas component from the children
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("Canvas component not found in children.");
                return; // Exit early to prevent further execution
            }

            // Set the position directly
            SetPosition(Mod.config.Postions);
        }

        public void Start()
        {
            initialized= true;
        }

        private void SetPosition(int index)
        {
            Debug.Log($"SetPosition called with index: {index}");

            if (index >= 0 && index < Positions.Length)
            {
                positionIndex = index;

                Debug.Log($"positionIndex set to: {positionIndex}");

                gameObject.transform.localPosition = Positions[index];
                gameObject.transform.localEulerAngles = new Vector3(0, Angles[index], 0);
            }








        }
    }
}




