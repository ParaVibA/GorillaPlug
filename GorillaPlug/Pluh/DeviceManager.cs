using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buttplug.Client;
using Buttplug.Core;
using UnityEngine;

namespace GorillaPlug.Pluh
{
    public class DeviceManager
    {
        private List<ButtplugClientDevice> ConnectedDevices { get; set; }
        private ButtplugClient ButtplugClient { get; set; }
        
        private int _pulseCount = 0;
        private bool _isConstantVibrating = false;
        public bool IsVibrating => _pulseCount > 0 || _isConstantVibrating;

        public DeviceManager(string clientName)
        {
            ConnectedDevices = new List<ButtplugClientDevice>();
            ButtplugClient = new ButtplugClient(clientName);
            Plugin.Mls.LogInfo("BP client created for " + clientName);
            ButtplugClient.DeviceAdded += HandleDeviceAdded;
            ButtplugClient.DeviceRemoved += HandleDeviceRemoved;
        }

        public bool IsConnected() => ButtplugClient != null && ButtplugClient.Connected;

        public async void ConnectDevices()
        {
            if (ButtplugClient.Connected) { return; }

            try
            {
                Plugin.Mls.LogInfo($"Attempting to connect to Intiface server at {PluhConfig.ServerUri.Value}");
                await ButtplugClient.ConnectAsync(new ButtplugWebsocketConnector(new Uri(PluhConfig.ServerUri.Value)));
                Plugin.Mls.LogInfo("Connection successful. Beginning scan for devices");
                await ButtplugClient.StartScanningAsync();
            }
            catch (ButtplugException exception)
            {
                Plugin.Mls.LogError($"Attempt to connect to devices failed. Ensure Intiface is running.");
                Plugin.Mls.LogDebug($"ButtplugIO error occured while connecting devices: {exception}");
            }
        }

        public void VibrateConnectedDevicesWithDuration(double intensity, float time)
        {
            intensity += PluhConfig.VibrateAmplifier.Value;
            if (Plugin.Instance != null)
            {
                Plugin.Instance.StartCoroutine(PulseRoutine(intensity, time));
            }
            else
            {
                Plugin.Mls.LogError("Cannot vibrate: Plugin Instance is null.");
            }
        }

        private IEnumerator PulseRoutine(double intensity, float time)
        {
            _pulseCount++;
            
            SendVibrationToAll(intensity);
            
            yield return new WaitForSeconds(time);
            
            SendVibrationToAll(0.0);

            _pulseCount--;
        }

        public void VibrateConnectedDevices(double intensity)
        {
            intensity += PluhConfig.VibrateAmplifier.Value;
            _isConstantVibrating = intensity > 0;
            SendVibrationToAll(intensity);
        }

        private void SendVibrationToAll(double intensity)
        {
            float clampedIntensity = Mathf.Clamp((float)intensity, 0f, 1.0f);
            
            foreach (ButtplugClientDevice device in ConnectedDevices)
            {
                device.VibrateAsync(clampedIntensity).ContinueWith(task => 
                {
                    if (task.IsFaulted)
                    {
                        Plugin.Mls.LogWarning($"Failed to vibrate device {device.Name}: {task.Exception?.InnerException?.Message}");
                    }
                });
            }
        }

        public void StopConnectedDevices()
        {
            _isConstantVibrating = false;
            foreach (var device in ConnectedDevices)
            {
                device.Stop().ContinueWith(task => 
                {
                    if (task.IsFaulted) Plugin.Mls.LogWarning($"Failed to stop device {device.Name}");
                });
            }
        }

        internal void CleanUp()
        {
            StopConnectedDevices();
        }

        private void HandleDeviceAdded(object sender, DeviceAddedEventArgs args)
        {
            if (!IsVibratableDevice(args.Device))
            {
                Plugin.Mls.LogInfo($"{args.Device.Name} was detected but ignored due to it not being vibratable.");
                return;
            }

            Plugin.Mls.LogInfo($"{args.Device.Name} connected to client {ButtplugClient.Name}");
            ConnectedDevices.Add(args.Device);
        }

        private void HandleDeviceRemoved(object sender, DeviceRemovedEventArgs args)
        {
            if (!IsVibratableDevice(args.Device)) { return; }

            Plugin.Mls.LogInfo($"{args.Device.Name} disconnected from client {ButtplugClient.Name}");
            ConnectedDevices.Remove(args.Device);
        }

        private bool IsVibratableDevice(ButtplugClientDevice device)
        {
            return device.VibrateAttributes.Count > 0;
        }
    }
}