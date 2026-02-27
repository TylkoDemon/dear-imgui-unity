using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.LowLevel;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if USING_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace ImGuiNET.Unity
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class DearImGuiBootstrap
    {
#if UNITY_EDITOR
        static DearImGuiBootstrap()
        {
            Initialize();
        }
#endif

        // Dear Imgui script's callbacks are used to update/sync custom in-game debugdraw/gizmos feature
        // Injecting into player loop or Cinemachine event makes sure we have the latest data and no jittering when using in-game debugdraw/gizmos features
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if USING_CINEMACHINE
            CinemachineCore.CameraUpdatedEvent.RemoveListener(Heartbeat);
            CinemachineCore.CameraUpdatedEvent.AddListener(Heartbeat);
#else
            AddPlayerLoop(typeof(DearImGui), Heartbeat,
                "PostLateUpdate", "FinishFrameRendering", before: true
            );   
#endif
        }
        
#if USING_CINEMACHINE
        private static void Heartbeat(CinemachineBrain arg0)
        {
            var instance = DearImGui.Instance;
            if (instance != null)
                instance.SystemUpdate();
        }
#else
        private static void Heartbeat()
        {
            var instance = DearImGui.Instance;
            if (instance != null)
                instance.SystemUpdate();
        }
        public static void AddPlayerLoop(Type t, [NotNull] PlayerLoopSystem.UpdateFunction method,
            string categoryName, string systemName, int firstLast = 0, bool before = false)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            var pls = new PlayerLoopSystem
            {
                type = t,
                updateDelegate = method
            };
            AddPlayerLoop(t, pls, categoryName, systemName, firstLast, before);
        }
        
        public static void AddPlayerLoop(Type t, PlayerLoopSystem method,
            string categoryName, string systemName, int firstLast = 0, bool before = false)
        {
            if (method.type != t)
                throw new ArgumentException("Method type must be of type T");
            
            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            
            foreach (var subloop in playerLoop.subSystemList)
            {
                if (subloop.subSystemList != null && subloop.subSystemList.Any(x => x.type == t))
                    return;
            }
            
            AddPlayerLoop(method, ref playerLoop, categoryName, systemName, firstLast, before);
            PlayerLoop.SetPlayerLoop(playerLoop);
        }
        
        public static void AddPlayerLoop(PlayerLoopSystem method, ref PlayerLoopSystem playerLoop,
            string categoryName, string systemName, int firstLast = 0, bool before = false)
        {
            int sysIndex = Array.FindIndex(playerLoop.subSystemList, (s) => s.type.Name == categoryName);
            PlayerLoopSystem category = playerLoop.subSystemList[sysIndex];
            var systemList = new List<PlayerLoopSystem>(category.subSystemList);
            
            if (firstLast < 0)
            {
                systemList.Insert(0, method);
            }
            else if (firstLast > 0)
            {
                systemList.Add(method);
            }
            else
            {
                int index = systemList.FindIndex(h => h.type.Name.Contains(systemName));
                if (before)
                    systemList.Insert(index, method);
                else
                    systemList.Insert(index + 1, method);
            }
            
            category.subSystemList = systemList.ToArray();
            playerLoop.subSystemList[sysIndex] = category;
        }
#endif
    }
}