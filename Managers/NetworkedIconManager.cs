using System.Collections.Generic;
using System.Linq;
using Axiom.Managers;
using GorillaLocomotion;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Rendering;
using Seralyth;
using Seralyth.Utilities;
using Seralyth.Classes.Menu;
using static Seralyth.Utilities.AssetUtilities;
using static Seralyth.Utilities.FileUtilities;
using static Seralyth.Utilities.RigUtilities;
using Seralyth.Mods;

namespace Axiom.Managers
{
	public class NetworkedIconManager : MonoBehaviour
	{
		// One indicator GameObject per rig, reused rather than recreated every frame
		private static Dictionary<VRRig, GameObject> iconPool = new Dictionary<VRRig, GameObject>();

		private static Material menuUserMaterial;
		private static Material superUserMaterial;
		private static Material blacklistMaterial;

		public Texture2D menuUserTexture = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.icon.png");
		public Texture2D superUserTexture = LoadTextureFromURL($"{PluginInfo.ServerResourcePath}/Images/Mods/stick.png", "stick.png");
		public Texture2D blacklistTexture = LoadTextureFromURL($"{PluginInfo.ServerResourcePath}/Images/Mods/warning.png", "warning.png");
		public Texture2D developerTexture = LoadTextureFromURL($"{ServerData.AssetURL}/crown.png", "crown.png");

		// How far above the nametag the icon floats, scaled by the target's own rig scale
		private const float IndicatorDistance = 0.35f;

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

					Transform nameTag = Visuals.GetNameTagTransform(playerRig);
					iconObject.transform.localScale = new Vector3(0.4f, 0.4f, 0.01f) * playerRig.scaleFactor;
					iconObject.transform.position = nameTag.position + nameTag.up * (IndicatorDistance * playerRig.scaleFactor);
					iconObject.transform.LookAt(GorillaTagger.Instance.headCollider.transform.position);
				}
			}
			catch {	}
		}

		// Returns the material to use for this player's badge, or null if they get no badge at all.
		// Blacklist takes priority over role badges - a blacklisted dev/mod is still shown as blacklisted.
		private Material GetBadgeState(Player player)
		{
			if (BlacklistManager.TryGetEntry(player.UserId, out _, out _))
			{
				EnsureMaterial(ref blacklistMaterial, blacklistTexture);
				return blacklistMaterial;
			}

			RoleTier tier = RoleManager.GetRoleTier(player.UserId);
			switch (tier)
			{
				case RoleTier.Developer:
					EnsureMaterial(ref superUserMaterial, developerTexture);
					return superUserMaterial;
				case RoleTier.SuperUser:
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