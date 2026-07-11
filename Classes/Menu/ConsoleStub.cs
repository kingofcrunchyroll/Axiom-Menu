using System.Collections.Generic;
using Seralyth;
using UnityEngine;

namespace Seralyth.Classes.Menu
{
    public class ConsoleStub
    {
        public static readonly string MenuName = "Axiom";
        public static readonly string MenuVersion = PluginInfo.Version;

        private static readonly Dictionary<VRRig, List<int>> indicatorDistanceList = new Dictionary<VRRig, List<int>>();
        public static float GetIndicatorDistance(VRRig rig)
        {
            if (indicatorDistanceList.ContainsKey(rig))
            {
                if (indicatorDistanceList[rig][0] == Time.frameCount)
                {
                    indicatorDistanceList[rig].Add(Time.frameCount);
                    return (0.3f + indicatorDistanceList[rig].Count * 0.5f);
                }

                indicatorDistanceList[rig].Clear();
                indicatorDistanceList[rig].Add(Time.frameCount);
                return (0.3f + indicatorDistanceList[rig].Count * 0.5f);
            }

            indicatorDistanceList.Add(rig, new List<int> { Time.frameCount });
            return 0.8f;
        }

        public static VRRig GetVRRigFromPlayer(NetPlayer p) =>
                GorillaGameManager.StaticFindRigForPlayer(p);
    }
}