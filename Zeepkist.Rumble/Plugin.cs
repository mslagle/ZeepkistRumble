using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using ZeepSDK.Racing;

namespace Zeepkist.Rumble
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("ZeepSDK")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;

        public static ConfigEntry<bool> EnableReason { get; private set; }
        public static ConfigEntry<bool> EnableCrash { get; private set; }
        public static ConfigEntry<bool> EnableWheel { get; private set; }
        public static ConfigEntry<bool> EnableCheckpoint { get; private set; }
        public static ConfigEntry<bool> EnableFinish { get; private set; }
        public static ConfigEntry<bool> EnableSpawn { get; private set; }
        public static ConfigEntry<bool> EnableTwoWheels { get; private set; }
        public static ConfigEntry<bool> EnableGForce { get; private set; }
        public static ConfigEntry<float> MinimumGForce { get; private set; }
        public static ConfigEntry<float> MaximumGForce { get; private set; }
        public static ConfigEntry<bool> EnableTireSmoke { get; private set; }

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

            EnableReason = Config.Bind<bool>("Mod", "Enable message for rumble reason", false);
            EnableCrash = Config.Bind<bool>("Mod", "Enable for crash", true);
            EnableWheel = Config.Bind<bool>("Mod", "Enable for wheel loss", true);
            EnableFinish = Config.Bind<bool>("Mod", "Enable for finish", true);
            EnableCheckpoint = Config.Bind<bool>("Mod", "Enable for checkpoint", true);
            EnableSpawn = Config.Bind<bool>("Mod", "Enable for spawn", true);
            EnableTwoWheels = Config.Bind<bool>("Mod", "Enable for two wheels", true);
            EnableTireSmoke = Config.Bind<bool>("Mod", "Enable for tiresmoke (disabled in 1st person)", true, "Disabled in 1st person");

            EnableGForce = Config.Bind<bool>("G Force", "Enable for high G force", true);
            MinimumGForce = Config.Bind<float>("G Force", "Minimum G Force", 3);
            MaximumGForce = Config.Bind<float>("G Force", "Maximum G Force", 10);

            EnableReason.SettingChanged += SettingChanged;
            EnableCrash.SettingChanged += SettingChanged;
            EnableWheel.SettingChanged += SettingChanged;
            EnableFinish.SettingChanged += SettingChanged;
            EnableCheckpoint.SettingChanged += SettingChanged;
            EnableSpawn.SettingChanged += SettingChanged;
            EnableTwoWheels.SettingChanged += SettingChanged;
            EnableTireSmoke.SettingChanged += SettingChanged;

            EnableGForce.SettingChanged += SettingChanged;
            MinimumGForce.SettingChanged += SettingChanged;
            MaximumGForce.SettingChanged += SettingChanged;

            var rewiredAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name.Equals(REWIRED_ASSEMBLY_NAME, StringComparison.OrdinalIgnoreCase));
            hasRewired = rewiredAssembly != null;
            Debug.Log($"Found rewired assembly {REWIRED_ASSEMBLY_NAME} == {hasRewired}");

            RacingApi.Crashed += RacingApi_Crashed;
            RacingApi.PlayerSpawned += RacingApi_PlayerSpawned;
            RacingApi.PassedCheckpoint += RacingApi_PassedCheckpoint;
            RacingApi.WheelBroken += RacingApi_WheelBroken;
            RacingApi.CrossedFinishLine += RacingApi_CrossedFinishLine;

            RacingApi.EnteredFirstPerson += RacingApi_EnteredFirstPerson;
            RacingApi.EnteredThirdPerson += RacingApi_EnteredThirdPerson;
        }

        private void SettingChanged(object sender, EventArgs e)
        {
            var configObject = sender as ConfigEntryBase;

            if (configObject != null)
            {
                Debug.Log($"Detected config change.  {configObject.Definition} = {configObject.BoxedValue}");
            }
        }

        private void RacingApi_CrossedFinishLine(float time)
        {
            isDead = true;
            Debug.Log($"Crossed Finish line {time}, setting isDead = true");

            if (EnableCrash.Value)
            {
                Rumble(1f, 1f, "Finish");
            }
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

        private void Update()
        {
            if (playerCar == null || hasRewired == false || isDead == true)
            {
                return;
            }

            // Detect when coming off 2 wheels
            if (EnableTwoWheels.Value && isOnTwoWheels == true && playerCar.IsCarOnTwoWheels() == false) {
                Debug.Log("Detected coming off 2 wheels, rumbling");
                Rumble(0.25f, 0.1f, "Two Wheels");
            }
            isOnTwoWheels = playerCar.IsCarOnTwoWheels();

            // Detect when hitting ground hard
            if (EnableGForce.Value && playerCar.localGForce.y > MinimumGForce.Value)
            {
                float strength = playerCar.localGForce.y / MaximumGForce.Value;
                if (strength > 1)
                {
                    strength = 1.0f;
                }
                Debug.Log($"Detected strong Y Gforce of {playerCar.localGForce.y} = {strength}");
                Rumble(strength, 0.2f, "G Force");
            }

            // Detect when hitting something hard
            if (EnableGForce.Value && playerCar.localGForce.x > MinimumGForce.Value)
            {
                float strength = playerCar.localGForce.x / MaximumGForce.Value;
                if (strength > 1)
                {
                    strength = 1.0f;
                }
                Debug.Log($"Detected strong X Gforce of {playerCar.localGForce.x} = {strength}");
                Rumble(strength, 0.2f, "G Force");
            }

            // Only run the following in 3rd person so 1st person doesnt get advantage
            if (isFirstPerson == false && EnableTireSmoke.Value)
            {
                // Detect when wheels are slipping
                var wheelLocked = playerCar.wheels.FirstOrDefault(x => x.IsGrounded() && x.IsSlipping());
                if (wheelLocked != null)
                {
                    Debug.Log($"Detected a wheel slipping on a hard surface {wheelLocked.name} on {wheelLocked.GetCurrentSurface().name} with {wheelLocked.GetCurrentSurface().physics.frictionFront}");
                    float rumbleIntensity = wheelLocked.GetCurrentSurface().physics.frictionFront / 1.5f;
                    Rumble(rumbleIntensity, 0.1f, $"Wheel smoke - {wheelLocked.GetCurrentSurface().physics.frictionFront.ToString("0.00")}");
                }
            }
        }
            

        private void Rumble(float intensity, float length, string reason)
        {
            if (EnableReason.Value)
            {
                PlayerManager.Instance.messenger.Log(reason, 1.0f);
            }
            
            //Debug.Log($"Rumbling with intensity = {intensity} and length = {length} due to {reason}");
            foreach (var joystick in Rewired.ReInput.players.AllPlayers.First().controllers.Joysticks)
            {
                if (!joystick.enabled) continue;
                if (joystick.vibrationMotorCount > 0) { joystick.SetVibration(0, intensity, length); }
                if (joystick.vibrationMotorCount > 1) { joystick.SetVibration(1, intensity, length); }
            }
        }

        private void RacingApi_WheelBroken()
        {
            if (EnableWheel.Value)
            {
                Debug.Log($"Detected a broken wheel");
                Rumble(1f, .5f, "Broken Wheel");
            }
        }

        private void RacingApi_PassedCheckpoint(float time)
        {
            if (EnableCheckpoint.Value)
            {
                Debug.Log($"Detected passing a checkpoint");
                Rumble(.5f, .1f, "Checkpoint");
            }
        }

        private void RacingApi_PlayerSpawned()
        {
            playerCar = PlayerManager.Instance.currentMaster.carSetups.First().cc;
            isDead = false;
            isFirstPerson = false;
            Debug.Log($"Detected a player spawn, setting isDead = false and isFirstPerson = false");

            if (EnableSpawn.Value)
            {
                Rumble(.5f, .2f, "Player Spawn");
            }

        }

        private void RacingApi_Crashed(CrashReason reason)
        {
            isDead = true;
            Debug.Log($"Detected a crash, setting isDead = true");

            if (EnableCrash.Value)
            {
                Rumble(1f, 1f, "Crashed");
            }
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
        }
    }
}