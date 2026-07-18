using System.Collections.Generic;
using UnityEngine;

namespace AreWeThereYet
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] {
        GameScenes.FLIGHT, GameScenes.EDITOR, GameScenes.SPACECENTER })]
    public class AWTYSaveData : ScenarioModule
    {
        public static AWTYSaveData Instance;

        public bool dataReady;

        private Dictionary<string, Vector2> windowPositions = new Dictionary<string, Vector2>();

        private const string KeyX = "x";
        private const string KeyY = "y";

        private static readonly Vector2 DefaultPos = new Vector2(300, 150);

        public override void OnAwake()
        {
            Instance = this;
            dataReady = false;
        }

        public override void OnSave(ConfigNode node)
        {
            foreach (KeyValuePair<string, Vector2> kv in windowPositions)
            {
                ConfigNode sub = node.AddNode(kv.Key);
                sub.AddValue(KeyX, Mathf.RoundToInt(kv.Value.x));
                sub.AddValue(KeyY, Mathf.RoundToInt(kv.Value.y));
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            windowPositions.Clear();
            foreach (ConfigNode sub in node.nodes)
            {
                string key = sub.name;
                float x = float.Parse(sub.GetValue(KeyX));
                float y = float.Parse(sub.GetValue(KeyY));
                windowPositions[key] = new Vector2(x, y);
            }
            dataReady = true;
        }

        public static Vector2 GetPosition(string sceneKey, float windowWidth, float windowHeight)
        {
            if (Instance == null || string.IsNullOrEmpty(sceneKey) || !Instance.windowPositions.ContainsKey(sceneKey))
                return DefaultPos;

            Vector2 pos = Instance.windowPositions[sceneKey];
            pos.x = Mathf.Clamp(pos.x, 0, Screen.width - windowWidth);
            pos.y = Mathf.Clamp(pos.y, 0, Screen.height - windowHeight);
            return pos;
        }

        public static void SetPosition(string sceneKey, Vector2 pos)
        {
            if (Instance == null || string.IsNullOrEmpty(sceneKey))
                return;
            Instance.windowPositions[sceneKey] = pos;
        }
    }
}
