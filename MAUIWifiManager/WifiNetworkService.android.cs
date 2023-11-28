﻿using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static Android.Provider.Settings;
using Context = Android.Content.Context;

namespace Plugin.MauiWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
    /// </summary>
    /// 
    public class WifiNetworkService : IWifiNetworkService
    {
        private static NetworkData? _networkData;
        private static Context? _context;
        private static NetworkCallback? _callback;
        private static ConnectivityManager? connectivityManager;
        private static bool _requested;
        readonly NetworkRequest request = new NetworkRequest.Builder().AddTransportType(transportType: Android.Net.TransportType.Wifi).Build();
        private static WifiManager wifiManager;
        public WifiManager wifiManager_2;
        private WifiScanReceiver wifiScanReceiver;
        public WifiNetworkService() 
        {
            
        }

        public static void Init(Context context)
        {
            CheckInit(context);
            _context = context;
            _networkData = new NetworkData();
            wifiManager = (WifiManager)(_context.GetSystemService(Context.WifiService));

            _callback = new NetworkCallback
            {
                NetworkAvailable = network =>
                {

                },
                NetworkUnavailable = () =>
                {

                }
            };

        }
        /// <summary>
        /// Connect Wi-Fi
        /// </summary>
        public async Task<NetworkData> ConnectWifi(string ssid, string password)
        {           
            if (Build.VERSION.SdkInt <= BuildVersionCodes.P)
            {               
                if (!wifiManager.IsWifiEnabled)
                {
                    wifiManager.SetWifiEnabled(true);
                }
                string wifiSsid = wifiManager.ConnectionInfo.SSID.ToString();
                if (wifiSsid != string.Format("\"{0}\"", ssid))
                {
                    WifiConfiguration wifiConfig = new WifiConfiguration();
                    wifiConfig.Ssid = string.Format("\"{0}\"", ssid);
                    wifiConfig.PreSharedKey = string.Format("\"{0}\"", password);
                    int netId = wifiManager.AddNetwork(wifiConfig);
                    wifiManager.Disconnect();
                    wifiManager.EnableNetwork(netId, true);
                    wifiManager.Reconnect();
                    _networkData.Ssid = wifiConfig.Ssid;

                }
                else
                {
                    Console.WriteLine("Cannot find valid SSID");
                }
            }
            else if (Build.VERSION.SdkInt == BuildVersionCodes.Q)
            {
                RequestNetwork(ssid, password);
            }
            else
                await AddWifi(ssid, password);
            return _networkData;
        }

        /// <summary>
        /// Disconnect Wi-Fi
        /// From Android Q (Android 10) you can't enable/disable wifi programmatically anymore. 
        /// So, use Settings Panel to toggle wifi connectivity
        /// </summary>
        public void DisconnectWifi(string? ssid)
        {
            CheckInit(_context);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                Intent panelIntent = new Intent(Panel.ActionWifi);
                _context.StartActivity(panelIntent);
            }               
            else
            {               
                wifiManager.SetWifiEnabled(false); // Disable wifi
                wifiManager.SetWifiEnabled(true); // Enable wifi
            }
        }

        /// <summary>
        /// Get Wi-Fi Network Info
        /// </summary>
        public async Task<NetworkData> GetNetworkInfo()
        {           
            int apiLevel = (int)Build.VERSION.SdkInt;
            if (apiLevel < 31)
            {               
                if (wifiManager.IsWifiEnabled)
                {
                    _networkData.Ssid = wifiManager.ConnectionInfo.SSID.Trim(new char[] { '"', '\"' });
                    _networkData.IpAddress = wifiManager.DhcpInfo.IpAddress;
                    _networkData.GatewayAddress = wifiManager.DhcpInfo.Gateway.ToString();
                    _networkData.NativeObject = wifiManager;
                }
                else
                {
                    Console.WriteLine("WI-Fi turned off");
                }
            }
            else
            {
                ConnectivityManager connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                NetworkInfo activeNetworkInfo = connectivityManager.ActiveNetworkInfo;
                if (activeNetworkInfo != null)
                {
                    NetworkCallbackFlags flagIncludeLocationInfo = NetworkCallbackFlags.IncludeLocationInfo;
                    NetworkCallback networkCallback = new NetworkCallback((int)flagIncludeLocationInfo);
                    connectivityManager.RequestNetwork(request, networkCallback);
                }
                else
                {
                    Console.WriteLine("Failed to get data");
                }
            }
            await Task.Delay(1000);
            return _networkData;
        }

        /// <summary>
        /// Open Wi-Fi Setting
        /// </summary>
        public Task<bool> OpenWifiSetting()
        {
            CheckInit(_context);
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Intent panelIntent;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                panelIntent = new Intent(Panel.ActionWifi);
            else
                panelIntent = new Intent(ActionWifiSettings);
            _context.StartActivity(panelIntent);
            taskCompletionSource.TrySetResult(true);
            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {

        }

        public static void CheckInit(Context context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(_context), "Please call WifiNetworkService.Init(this) inside the MainActivity's OnCreate function.");
        }

        private async Task AddWifi(string ssid, string psk)
        {
            await Task.Run(async () =>
            {
                var suggestions = new List<IParcelable>
                        {
                           new WifiNetworkSuggestion.Builder()
                            .SetSsid(ssid)
                            .SetWpa2Passphrase(psk)
                            .SetPriority(GetPriorityForSignalStrength(-60)) // Set the priority based on signal strength
                            .SetIsAppInteractionRequired(true)
                            .SetIsUserInteractionRequired(true)
                            .SetIsMetered(false)                         
                            .SetIsEnhancedOpen(false)
                            .SetIsHiddenSsid(false)
                            .Build()
                    };

                await OpenWifiSetting();
                var bundle = new Bundle();
                bundle.PutParcelableArrayList("android.provider.extra.WIFI_NETWORK_LIST", suggestions);
                var intent = new Intent("android.settings.WIFI_ADD_NETWORKS");
                intent.PutExtras(bundle);
                _context.StartActivity(intent);

            });
         }

        private int GetPriorityForSignalStrength(int signalStrength)
        {
            if (signalStrength >= -50)
            {
                return 1;
            }
            else if (signalStrength >= -60)
            {
                return 2;
            }
            else if (signalStrength >= -70)
            {
                return 3;
            }
            else if (signalStrength >= -80)
            {
                return 4;
            }
            else if (signalStrength >= -90)
            {
                return 5;
            }
            else
            {
                return 6;
            }
        }

        public void RequestNetwork(string ssid, string password) 
        {           
            if (!wifiManager.IsWifiEnabled) 
            {
                Console.WriteLine("Wi-Fi is turned off");
            }

            var specifier = new WifiNetworkSpecifier.Builder()
               .SetSsid(ssid)
               .SetWpa2Passphrase(password)
               .Build();

            var request = new NetworkRequest.Builder()?
                .AddTransportType(Android.Net.TransportType.Wifi)?
                .SetNetworkSpecifier(specifier)?
                .Build();

            UnregisterNetworkCallback(_callback);
            connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
            if (_requested)
            {
                connectivityManager?.UnregisterNetworkCallback(_callback);
            }
            connectivityManager?.RequestNetwork(request, _callback);
            _requested = true;
        }

        private void UnregisterNetworkCallback(NetworkCallback networkCallback)
        {
            if (networkCallback != null)
            {
                try
                {
                    connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                    connectivityManager.UnregisterNetworkCallback(networkCallback);

                }
                catch
                {
                    networkCallback = null;
                }
            }
        }

        /// <summary>
        /// Scan Wi-Fi Networks
        /// </summary>
        public async Task<List<NetworkData>> ScanWifiNetworks()
        {
            List<NetworkData> wifiNetworks = new List<NetworkData>();   
            if (wifiManager.IsWifiEnabled)
            {
                wifiManager.StartScan();
                var scanResults = wifiManager.ScanResults;
                foreach (var result in scanResults)
                {
                    wifiNetworks.Add(new NetworkData() { Bssid = result.Bssid,Ssid = result.Ssid,NativeObject = result });
                }
            }
            else
            {
                Console.WriteLine("WI-Fi turned off");
            }
            return wifiNetworks;
        }

        private class NetworkCallback : ConnectivityManager.NetworkCallback
        {
            public Action<Network>? NetworkAvailable { get; set; }
            public Action? NetworkUnavailable { get; set; }

            public NetworkCallback(int flags) 
            {
            }
            public NetworkCallback()
            {
            }
            public override void OnAvailable(Network network)
            {
                base.OnAvailable(network);
                NetworkAvailable?.Invoke(network);
                //connectivityManager.BindProcessToNetwork(network);
            }

            public override void OnUnavailable()
            {
                base.OnUnavailable();
                NetworkUnavailable?.Invoke();
            }
            public override void OnCapabilitiesChanged(Network network, NetworkCapabilities networkCapabilities)
            {
                base.OnCapabilitiesChanged(network, networkCapabilities);
                WifiInfo wifiInfo = (WifiInfo)networkCapabilities.TransportInfo;

                if (wifiInfo != null)
                {
                    _networkData.StausId = 1;
                    _networkData.Ssid = wifiInfo.SSID.Trim(new char[] { '"', '\"' });
                    _networkData.IpAddress = wifiInfo.IpAddress;
                    _networkData.NativeObject = wifiInfo;
                    _networkData.SignalStrength = wifiInfo.Rssi;                  
                }
            }
        }

        [Flags]
        public enum NetworkCallbackFlags
        {
            //
            // Summary:
            //     To be added.
            [IntDefinition(null, JniField = "")]
            None = 0x0,
            //
            // Summary:
            //     To be added.
            [IntDefinition("Android.Net.ConnectivityManager.NetworkCallback.FlagIncludeLocationInfo", JniField = "android/net/ConnectivityManager$NetworkCallback.FLAG_INCLUDE_LOCATION_INFO")]
            IncludeLocationInfo = 0x1
        }

        private class WifiScanReceiver : BroadcastReceiver
        {
            private WifiNetworkService wifiScanner;

            public List<ScanResult> ScanResults { get; private set; }

            public WifiScanReceiver(WifiNetworkService wifiScanner)
            {
                this.wifiScanner = wifiScanner;
                ScanResults = new List<ScanResult>();
            }

            public override void OnReceive(Context context, Intent intent)
            {
            }
        }
    }
}
