using HarmonyLib;
using Seralyth.Classes.Menu;
using GorillaLocomotion;
using Seralyth.Menu;
using System.Linq;

namespace Seralyth.Patches
{
    [HarmonyPatch(typeof(GTPlayer), nameof(GTPlayer.FixedUpdate))]
    public class FixedUpdatePatch
    {
        public static void Postfix()
        {
            foreach (ButtonInfo button in Buttons.buttons
                     .SelectMany(list => list)
                     .Where(button => (button.enabled || button.label) && (button.fixedMethod != null)))
            {
                button.fixedMethod?.Invoke();
            }
        }
    }
}