using HarmonyLib;
using StationeersMods.Interface;


namespace KelvinConsole
{
    class KelvinConsole : ModBehaviour
    {
        public override void OnLoaded(ContentHandler contentHandler)
        {
            Harmony harmony = new Harmony("KelvinConsole");
            harmony.PatchAll();
            UnityEngine.Debug.Log("KelvinConsole Loaded!");
        }
    }
}