using Axiom.Managers;
using ExitGames.Client.Photon;
using GorillaLocomotion;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using Seralyth;
using Seralyth.Classes;
using Seralyth.Classes.Menu;
using Seralyth.Mods;
using Seralyth.Utilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using static Seralyth.Utilities.AssetUtilities;
using static Seralyth.Utilities.FileUtilities;
using static Seralyth.Utilities.RigUtilities;

namespace Axiom.Managers
{
    public class NetworkedIconManager : MonoBehaviour
    {
        // One indicator GameObject per rig, reused rather than recreated every frame
        private static Dictionary<VRRig, GameObject> iconPool = new Dictionary<VRRig, GameObject>();

        private static Material menuUserMaterial;
        private static Material superUserMaterial;
        private static Material blacklistMaterial;

        // Local mirror of the "HideSelfIcon" Photon custom property - kept as a real field
        // (not just read-on-demand) so UI toggles can check current state cheaply. The setter
        // is what actually broadcasts it; setting this field directly elsewhere won't propagate.
        public static bool hideSelfIcon;

        public Texture2D menuUserTexture;
        public Texture2D superUserTexture;
        public Texture2D blacklistTexture;
        public Texture2D developerTexture;

        public void Awake()
        {
            menuUserTexture = SafeLoadResource($"{PluginInfo.ClientResourcePath}.icon.png");
            superUserTexture = SafeLoadURL($"{PluginInfo.ServerResourcePath}/Images/Mods/Visuals/stick.png", "stick.png");
            blacklistTexture = SafeLoadURL($"{PluginInfo.ServerResourcePath}/Images/Mods/Visuals/warning.png", "warning.png");
            developerTexture = SafeLoadURL($"{ServerData.AssetURL}/crown.png", "crown.png");

            // Custom properties are per-room in Photon, so re-broadcast on every join rather
            // than once at startup - same pattern FriendManager already uses for its own check.
            NetworkSystem.Instance.OnJoinedRoomEvent += BroadcastSelfState;
        }

        // Call this from your "Hide My Badge" button instead of setting hideSelfIcon directly -
        // this is what actually syncs the value to everyone else's client.
        public static void SetHideSelfIcon(bool value)
        {
            hideSelfIcon = value;
            if (PhotonNetwork.InRoom)
                BroadcastSelfState();
        }

        private static void BroadcastSelfState()
        {
            var props = new Hashtable
            {
                { "HasAxiom", true },
                { "HideSelfIcon", hideSelfIcon }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        private static Texture2D SafeLoadResource(string resourcePath)
        {
            try
            {
                return LoadTextureFromResource(resourcePath);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[NetworkedIconManager] Failed to load resource texture '{resourcePath}': {e}");
                return null;
            }
        }

        private static Texture2D SafeLoadURL(string url, string fileName)
        {
            try
            {
                return LoadTextureFromURL(url, fileName);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[NetworkedIconManager] Failed to load '{fileName}' from {url}: {e}");
                return null;
            }
        }

        public void Update()
        {
            if (!PhotonNetwork.InRoom)
            {
                if (iconPool.Count > 0)
                {
                    foreach (KeyValuePair<VRRig, GameObject> icon in iconPool)
                        Destroy(icon.Value);
                    iconPool.Clear();
                }
                return;
            }

            try
            {
                // Prune anything stale: rig despawned, or no longer qualifies for any badge
                List<VRRig> toRemove = (from pair in iconPool
                                        let rig = pair.Key
                                        let player = rig?.Creator?.GetPlayerRef()
                                        where rig == null
                                           || !VRRigCache.ActiveRigs.Contains(rig)
                                           || player == null
                                           || GetBadgeState(player) == null
                                        select rig).ToList();

                foreach (VRRig rig in toRemove)
                {
                    if (iconPool.TryGetValue(rig, out GameObject go))
                        Destroy(go);
                    iconPool.Remove(rig);
                }

                foreach (Player player in PhotonNetwork.PlayerListOthers)
                {
                    Material badgeMaterial = GetBadgeState(player);
                    if (badgeMaterial == null)
                        continue;

                    VRRig playerRig = GetVRRigFromPlayer(player);
                    if (playerRig == null)
                        continue;

                    if (!iconPool.TryGetValue(playerRig, out GameObject iconObject))
                    {
                        iconObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Destroy(iconObject.GetComponent<Collider>());
                        iconPool.Add(playerRig, iconObject);
                    }

                    Renderer rend = iconObject.GetComponent<Renderer>();
                    rend.material = badgeMaterial;

                    iconObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.01f) * playerRig.scaleFactor;
                    iconObject.transform.position = playerRig.headMesh.transform.position + playerRig.headMesh.transform.up * (ConsoleStub.GetIndicatorDistance(playerRig) * playerRig.scaleFactor);
                    iconObject.transform.LookAt(GorillaTagger.Instance.headCollider.transform.position);
                }
            }
            catch { }
        }

        // Returns the material to use for this player's badge, or null if they get no badge at all.
        // Blacklist takes priority over role badges - a blacklisted dev/mod is still shown as
        // blacklisted even if they've also toggled HideSelfIcon.
        private Material GetBadgeState(Player player)
        {
            if (BlacklistManager.TryGetEntry(player.UserId, out _, out _))
            {
                EnsureMaterial(ref blacklistMaterial, blacklistTexture);
                return blacklistMaterial;
            }

            bool theyHideTheirIcon = player.CustomProperties.TryGetValue("HideSelfIcon", out object hideVal) && hideVal is bool hidden && hidden;
            if (theyHideTheirIcon)
                return null;

            RoleTier tier = RoleManager.GetRoleTier(player.UserId);

            // Self-reported, unlike Moderator/Developer which only ever come from the
            // trusted SuperUsers.json - don't let this override a real trusted tier.
            if (tier == RoleTier.None && player.CustomProperties.TryGetValue("HasAxiom", out object hasAxiomVal) && hasAxiomVal is bool hasAxiom && hasAxiom)
                tier = RoleTier.MenuUser;

            switch (tier)
            {
                case RoleTier.Developer:
                    EnsureMaterial(ref superUserMaterial, developerTexture);
                    return superUserMaterial;
                case RoleTier.Moderator:
                    EnsureMaterial(ref superUserMaterial, superUserTexture);
                    return superUserMaterial;
                case RoleTier.MenuUser:
                    EnsureMaterial(ref menuUserMaterial, menuUserTexture);
                    return menuUserMaterial;
                default:
                    return null;
            }
        }

        private static void EnsureMaterial(ref Material mat, Texture2D texture)
        {
            if (mat != null)
                return;

            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                mainTexture = texture
            };

            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}