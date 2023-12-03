using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing.MiniJSON;
using ZeepSDK.Racing;
using static Rewired.Demos.PressStartToJoinExample_Assigner;

namespace Zeepkist.Rumble
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("ZeepSDK")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;

        public static ConfigEntry<bool> Enable { get; private set; }

        public static bool hasRewired { get; set; }
        const string REWIRED_ASSEMBLY_NAME = "Rewired_Core";

        static New_ControlCar playerCar = null;

        // States
        static bool isOnTwoWheels { get; set; }
        static bool isFirstPerson { get; set; }
        static bool isDead { get; set; }

        private void Awake()
        {
            harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            // Plugin startup logic
            Debug.Log($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            Enable = Config.Bind<bool>("Mod", "Enable", true);

            var rewiredAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name.Equals(REWIRED_ASSEMBLY_NAME, StringComparison.OrdinalIgnoreCase));
            hasRewired = rewiredAssembly != null;
            Debug.Log($"Found rewired assembly {REWIRED_ASSEMBLY_NAME} == {hasRewired}");

            RacingApi.Crashed += RacingApi_Crashed;
            RacingApi.PlayerSpawned += RacingApi_PlayerSpawned;
            RacingApi.PassedCheckpoint += RacingApi_PassedCheckpoint;
            RacingApi.WheelBroken += RacingApi_WheelBroken;

            RacingApi.EnteredFirstPerson += RacingApi_EnteredFirstPerson;
            RacingApi.EnteredThirdPerson += RacingApi_EnteredThirdPerson;
        }

        private void RacingApi_EnteredThirdPerson()
        {
            Debug.Log("Detected third person, setting isFirstPerson = false");
            isFirstPerson = false;
        }

        private void RacingApi_EnteredFirstPerson()
        {
            Debug.Log("Detected first person, setting isFirstPerson = true");
            isFirstPerson = true;
        }

        public void OnGUI()
        {
            if (playerCar == null || Enable.Value == false || hasRewired == false || isDead == true)
            {
                return;
            }

            // Detect when coming off 2 wheels
            if (isOnTwoWheels == true && playerCar.IsCarOnTwoWheels() == false) {
                Debug.Log("Detected coming off 2 wheels, rumbling");
                Rumble(0.25f, 0.1f);
            }
            isOnTwoWheels = playerCar.IsCarOnTwoWheels();

            // Detect when hitting ground hard
            if (playerCar.localGForce.y > 2)
            {
                float strength = playerCar.localGForce.y / 10;
                if (strength > 1)
                {
                    strength = 1.0f;
                }
                Debug.Log($"Detected strong Y Gforce of {playerCar.localGForce.y} = {strength}");
                Rumble(strength, 0.2f);
            }

            // Detect when hitting something hard
            if (playerCar.localGForce.x > 2)
            {
                float strength = playerCar.localGForce.x / 10;
                if (strength > 1)
                {
                    strength = 1.0f;
                }
                Debug.Log($"Detected strong X Gforce of {playerCar.localGForce.x} = {strength}");
                Rumble(strength, 0.2f);
            }

            // Only run the following in 3rd person so 1st person doesnt get advantage
            if (isFirstPerson == false)
            {
                // Detect when wheels are slipping
                bool isAnyWheelSlippingOrLocked = playerCar.wheels.Any(x => x.isSlipping || x.isWheelLock);
                if (isAnyWheelSlippingOrLocked)
                {
                    Debug.Log($"Detected a wheel slipping or locked");
                    Rumble(0.2f, 0.1f);
                }

                // Detect difference in surfaces and only going 50+ speed
                // Multiple by 3.6 to get real speed
                if (playerCar.GetLocalVelocity().magnitude * 3.6f > 50)
                {
                    var surfaces = playerCar.wheels.Select(x => x.GetCurrentSurface().physics.name).Distinct();
                    if (surfaces.Count() > 1)
                    {
                        Debug.Log($"Detected two or more different surfaces --> {string.Join(',', surfaces)}");
                        Rumble(0.1f, 0.1f);
                    }
                }

            }
        }
            

        private void Rumble(float intensity, float length)
        {
            if (Enable.Value == false)
            {
                Debug.Log("Mod disabled, not rumbling!");
                return;
            }

            Debug.Log($"Rumbling with intensity = {intensity} and length = {length}");
            foreach (var joystick in Rewired.ReInput.players.AllPlayers.First().controllers.Joysticks)
            {
                if (!joystick.enabled) continue;
                if (joystick.vibrationMotorCount > 0) { joystick.SetVibration(0, intensity, length); }
                if (joystick.vibrationMotorCount > 1) { joystick.SetVibration(1, intensity, length); }
            }
        }

        private void RacingApi_WheelBroken()
        {
            Debug.Log($"Detected a broken wheel");
            Rumble(1f, .5f);
        }

        private void RacingApi_PassedCheckpoint(float time)
        {
            Debug.Log($"Detected passing a checkpoint");
            Rumble(.5f, .1f);
        }

        private void RacingApi_PlayerSpawned()
        {
            playerCar = PlayerManager.Instance.currentMaster.carSetups.First().cc;
            isDead = false;
            isFirstPerson = false;

            Debug.Log($"Detected a player spawn, setting isDead = false and isFirstPerson = false");
            Rumble(.5f, .2f);
        }

        private void RacingApi_Crashed(CrashReason reason)
        {
            isDead = true;
            Debug.Log($"Detected a crash");
            Rumble(1f, 1f);
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
        }
    }
}