using System;
using UnityEngine;

namespace CarRacer.Game.Platform
{
    /// <summary>
    /// Android filters out multicast and broadcast packets on Wi-Fi to save power unless the
    /// app holds a WifiManager.MulticastLock (requires CHANGE_WIFI_MULTICAST_STATE, added by
    /// AndroidManifestPostProcessor). Hold it only while discovery is active. No-op elsewhere.
    /// </summary>
    public sealed class AndroidMulticastLock : IDisposable
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _lock;
#endif

        public bool IsHeld { get; private set; }

        public void Acquire()
        {
            if (IsHeld)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi");
                _lock = wifi.Call<AndroidJavaObject>("createMulticastLock", "car-racer-discovery");
                _lock.Call("setReferenceCounted", false);
                _lock.Call("acquire");
                IsHeld = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not acquire multicast lock; LAN discovery may not work: {e.Message}");
            }
#else
            IsHeld = true;
#endif
        }

        public void Release()
        {
            if (!IsHeld)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _lock?.Call("release");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not release multicast lock: {e.Message}");
            }

            _lock?.Dispose();
            _lock = null;
#endif
            IsHeld = false;
        }

        public void Dispose() => Release();
    }
}
