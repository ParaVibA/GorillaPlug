using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using GorillaPlug.Pluh;
using UnityEngine.InputSystem;
using Photon.Pun;
using ExitGames.Client.Photon;
using GorillaNetworking;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace GorillaPlug
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        private static GorillaTagManager _gtagManager;
        private static DeviceManager DeviceManager { get; set; }
        internal static ManualLogSource Mls { get; private set; }
        
        private bool _showUI = true;
        private bool _showOverlay = false;
        private Rect _windowRect = new Rect(20, 20, 350, 450);
        
        private Texture2D _statusTexture;
        private Texture2D _vibStatusTexture;
        
        public bool wasAlreadyTagged = false;

        // Networking Variables (Matching GorillaShirts pattern)
        private readonly Dictionary<string, object> _networkProperties = new Dictionary<string, object>();
        private bool _propertiesReady;
        private float _propertySetTimer;
        private const float NetworkRaiseInterval = 1.0f; // Interval to prevent network spam
        
        private static Assembly LoadEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            string resourceName = "GorillaPlug.Libs.Buttplug.dll";

            var assemblyName = new AssemblyName(args.Name);
            if (assemblyName.Name != "Buttplug") return null;

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    var resources = string.Join(", ", Assembly.GetExecutingAssembly().GetManifestResourceNames());
                    Mls?.LogError($"Could not find embedded resource '{resourceName}'. Available: {resources}");
                    return null;
                }

                byte[] assemblyData = new byte[stream.Length];
                stream.Read(assemblyData, 0, assemblyData.Length);
                return Assembly.Load(assemblyData);
            }
        }

        void Awake()
        {
            Instance = this;
            Mls = Logger;
            
            AppDomain.CurrentDomain.AssemblyResolve += LoadEmbeddedAssembly;
            
            InitializeDeviceManager();
            
            Logger.LogInfo($"Plugin {PluginInfo.Name} ({PluginInfo.Version}) is loaded!");
        }
        
        private void InitializeDeviceManager()
        {
            try 
            {
                DeviceManager = new DeviceManager("GorillaPlug");
                DeviceManager.ConnectDevices();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to init DeviceManager: {ex}");
            }
        }

        public void OnPlayerTagged()
        {
            if (DeviceManager != null && DeviceManager.IsConnected())
            {
                float duration = PluhConfig.VibrateOnTagDuration.Value;
                float strength = PluhConfig.VibrateOnTagStrength.Value;
                
                DeviceManager.VibrateConnectedDevicesWithDuration(strength, duration);
            }
        }
        
        void Update()
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                _showUI = !_showUI;
            }
            
            ScanForManagerUpdate();
            UpdateNetworkLogic();
        }
        
        private void UpdateNetworkLogic()
        {
            _propertySetTimer = Mathf.Max(_propertySetTimer - Time.unscaledDeltaTime, 0f);
            
            if (DeviceManager != null)
            {
                bool isVibrating = DeviceManager.IsVibrating;
                string value = isVibrating ? "currently vibing: true" : "currently vibing: false";
                
                SetProperty("Gorilla ButtPlug", value);
            }
            
            if (_propertiesReady && _propertySetTimer <= 0)
            {
                if (PhotonNetwork.InRoom)
                {
                    _propertiesReady = false;
                    _propertySetTimer = NetworkRaiseInterval;
                    
                    Hashtable hash = new Hashtable();
                    foreach(var kvp in _networkProperties)
                    {
                        hash.Add(kvp.Key, kvp.Value);
                    }

                    PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
                }
            }
        }

        public void SetProperty(string key, object value)
        {
            bool setProperties = false;

            if (_networkProperties.ContainsKey(key))
            {
                if (!_networkProperties[key].Equals(value))
                {
                    setProperties = true;
                    _networkProperties[key] = value;
                }
            }
            else
            {
                setProperties = true;
                _networkProperties.Add(key, value);
            }

            // Mark as dirty if something changed
            _propertiesReady = _propertiesReady || setProperties;
        }

        private static void ScanForManagerUpdate()
        {
            if (NetworkSystem.Instance == null || !NetworkSystem.Instance.InRoom)
            {
                _gtagManager = null;
                return;
            }

            if (_gtagManager != null) return;
            
            if (GorillaGameManager.instance != null && GorillaGameManager.instance is GorillaTagManager tagManager)
            {
                _gtagManager = tagManager;
            }
        }

        private void FixedUpdate()
        {
            CheckForLocalTag();
        }

        private void CheckForLocalTag()
        {
            if (NetworkSystem.Instance == null || !NetworkSystem.Instance.InRoom) return;
            if (_gtagManager == null) return;

            bool isTaggedCurrently = _gtagManager.LocalIsTagged(NetworkSystem.Instance.LocalPlayer);
            
            if (!wasAlreadyTagged && isTaggedCurrently)
            {
                OnPlayerTagged();
            }

            wasAlreadyTagged = isTaggedCurrently;
        }

        void OnGUI()
        {
            if (_showOverlay)
            {
                DrawOverlay();
            }
            
            if (_showUI)
            {
                _windowRect = GUI.Window(101, _windowRect, DrawSettingsWindow, "GorillaPlug Settings [F1]");
            }
        }

        void DrawOverlay()
        {
            bool isConnected = DeviceManager != null && DeviceManager.IsConnected();
            bool isVibrating = DeviceManager != null && DeviceManager.IsVibrating;
            
            if (_statusTexture == null) _statusTexture = new Texture2D(1, 1);
            _statusTexture.SetPixel(0, 0, isConnected ? Color.green : Color.red);
            _statusTexture.Apply();

            if (_vibStatusTexture == null) _vibStatusTexture = new Texture2D(1, 1);
            _vibStatusTexture.SetPixel(0, 0, isVibrating ? Color.cyan : Color.gray);
            _vibStatusTexture.Apply();

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = _statusTexture,
                    textColor = Color.black
                },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.Box(new Rect(Screen.width - 110, 10, 100, 30), isConnected ? "ACTIVE" : "INACTIVE", boxStyle);
            
            var vibBoxStyle = new GUIStyle(boxStyle)
            {
                normal =
                {
                    background = _vibStatusTexture
                }
            };
            GUI.Box(new Rect(Screen.width - 110, 50, 100, 30), isVibrating ? "VIBING" : "IDLE", vibBoxStyle);
        }

        void DrawSettingsWindow(int windowID)
        {
            GUILayout.BeginVertical();
            
            GUILayout.Label($"<b>Connection</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reconnect Devices"))
            {
                DeviceManager?.ConnectDevices();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label("<b>Vibration Settings</b>");
            
            float currentStrength = PluhConfig.VibrateOnTagStrength.Value;
            GUILayout.Label($"Intensity: {currentStrength:P0}");
            float newStrength = GUILayout.HorizontalSlider(currentStrength, 0.0f, 1.0f);
            if (Math.Abs(newStrength - currentStrength) > 0.001f)
            {
                PluhConfig.VibrateOnTagStrength.Value = newStrength;
            }
            
            float currentDuration = PluhConfig.VibrateOnTagDuration.Value;
            GUILayout.Label($"Duration: {currentDuration:F1}s");
            float newDuration = GUILayout.HorizontalSlider(currentDuration, 0.1f, 5.0f);
            if (Math.Abs(newDuration - currentDuration) > 0.001f)
            {
                PluhConfig.VibrateOnTagDuration.Value = newDuration;
            }

            GUILayout.Space(10);
            
            GUILayout.Label("<b>Tests</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Low Vibe")) DeviceManager?.VibrateConnectedDevicesWithDuration(0.3, 0.5f);
            if (GUILayout.Button("High Vibe")) DeviceManager?.VibrateConnectedDevicesWithDuration(1.0, 0.5f);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Simulate Tag"))
            {
                OnPlayerTagged();
            }

            GUILayout.Space(10);
            
            GUILayout.Label("<b>UI Options</b>");
            if (GUILayout.Button(_showOverlay ? "Hide Overlay Boxes" : "Show Overlay Boxes"))
            {
                _showOverlay = !_showOverlay;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void OnApplicationQuit()
        {
            DeviceManager?.CleanUp();
        }
    }
}