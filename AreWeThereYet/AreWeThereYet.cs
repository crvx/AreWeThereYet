using UnityEngine;
using Contracts;
using System.Collections.Generic;
using KSP.UI.Screens;
using FinePrint.Contracts.Parameters;
using System;

namespace AreWeThereYet
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    public class AreWeThereYetUI : MonoBehaviour
    {
        private class TouristTask
        {
            public string Description;
            public bool IsComplete;
            public CelestialBody Body;
        }

        private ApplicationLauncherButton appButton;

        private bool isWindowOpen = false;
        private bool showComplete = false;
        private int selectedFilterIndex = 0;
        private bool showFilterDropdown = false;
        private Rect windowRect = new Rect(300, 150, 380, 580);
        private int windowID;

        private Dictionary<string, List<TouristTask>> touristTasks = new Dictionary<string, List<TouristTask>>();
        private Vector2 scrollPosition = Vector2.zero;
        private Rect dropdownButtonRect;
        private string sceneKey;
        private bool positionRestored;
        private float lastSaveTime;
        private GUIStyle rowEvenStyle;
        private GUIStyle rowOddStyle;
        private GUIStyle filterItemStyle;
        private GUIStyle filterItemSelectedStyle;
        private Texture2D filterHoverTex;
        private string[] filterDisplayNames;
        private string[] filterDisplayNamesClean;
        private CelestialBody[] filterBodies;
        private bool[] filterIsPlanet;
        private int[] filterTreeLevel;
        private int[] filterParentIndex;
        private Texture2D treeLineTex;
        private Texture2D dropdownBgTex;
        private GUIStyle dropdownBgStyle;
        private Color[] bodyColors;
        private bool dirty;
        private List<KerbalDestinationParameter> subscribedParams = new List<KerbalDestinationParameter>();
        void Start()
        {
            windowID = UnityEngine.Random.Range(1000, 200000);

            var texEven = new Texture2D(1, 1);
            texEven.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.1f));
            texEven.Apply();

            var texOdd = new Texture2D(1, 1);
            texOdd.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.1f));
            texOdd.Apply();

            rowEvenStyle = new GUIStyle { normal = { background = texEven }, padding = new RectOffset(0, 0, 0, 0) };
            rowOddStyle = new GUIStyle { normal = { background = texOdd }, padding = new RectOffset(0, 0, 0, 0) };

            filterHoverTex = new Texture2D(1, 1);
            filterHoverTex.SetPixel(0, 0, new Color(0.2f, 0.4f, 0.2f, 0.3f));
            filterHoverTex.Apply();

            treeLineTex = new Texture2D(1, 1);
            treeLineTex.SetPixel(0, 0, new Color(0.5f, 1f, 1f, 0.5f));
            treeLineTex.Apply();

            dropdownBgTex = new Texture2D(1, 1);
            dropdownBgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
            dropdownBgTex.Apply();

            Dictionary<string, Color> bodyColorConfig = new Dictionary<string, Color>();
            foreach (UrlDir.UrlConfig urlConfig in GameDatabase.Instance.GetConfigs("ARE_WE_THERE_YET"))
            {
                foreach (ConfigNode node in urlConfig.config.GetNodes("BODY_COLOR"))
                {
                    string name = node.GetValue("body");
                    string[] parts = node.GetValue("color").Split(',');
                    bodyColorConfig[name] = new Color(
                        float.Parse(parts[0]), float.Parse(parts[1]),
                        float.Parse(parts[2]), float.Parse(parts[3]));
                }
            }

            int nBodies = FlightGlobals.Bodies.Count;
            bodyColors = new Color[nBodies];
            for (int i = 0; i < nBodies; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];
                Color c = Color.clear;
                if (bodyColorConfig.TryGetValue(body.bodyName, out Color cfgColor))
                    c = cfgColor;
                bodyColors[i] = c;
            }

            sceneKey = GetSceneKey();

            GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnAppLauncherDestroyed);
            GameEvents.onGameSceneLoadRequested.Add(OnSceneLoadRequested);

            if (ApplicationLauncher.Ready && appButton == null)
                OnAppLauncherReady();

            BuildFilterOptions();
            positionRestored = false;
        }

        void OnDestroy()
        {
            SaveWindowPosition();
            ClearSubscriptions();
            GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnAppLauncherDestroyed);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneLoadRequested);
            OnAppLauncherDestroyed();

            if (rowEvenStyle != null && rowEvenStyle.normal.background != null)
                Destroy(rowEvenStyle.normal.background);
            if (rowOddStyle != null && rowOddStyle.normal.background != null)
                Destroy(rowOddStyle.normal.background);
            if (filterHoverTex != null)
                Destroy(filterHoverTex);
            if (treeLineTex != null)
                Destroy(treeLineTex);
            if (dropdownBgTex != null)
                Destroy(dropdownBgTex);
        }

        private void OnAppLauncherReady()
        {
            if (appButton != null) return;

            appButton = ApplicationLauncher.Instance.AddModApplication(
                OnToggleTrue,
                OnToggleFalse,
                null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW |
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH |
                ApplicationLauncher.AppScenes.SPACECENTER,
                GameDatabase.Instance.GetTexture("AreWeThereYet/Textures/AreWeThereYet", false)
            );
        }

        private void OnAppLauncherDestroyed()
        {
            if (appButton == null) return;
            ApplicationLauncher.Instance.RemoveModApplication(appButton);
            appButton = null;
        }

        private void OnToggleTrue()
        {
            UpdateTouristData();
            isWindowOpen = true;
        }

        private void OnToggleFalse() => isWindowOpen = false;

        private void UpdateTouristData()
        {
            ClearSubscriptions();
            touristTasks.Clear();

            if (HighLogic.LoadedSceneIsFlight)
            {
                Vessel vessel = FlightGlobals.ActiveVessel;
                if (vessel == null) return;

                for (int i = 0; i < vessel.parts.Count; i++)
                {
                    Part part = vessel.parts[i];
                    if (part.protoModuleCrew == null) continue;
                    for (int j = 0; j < part.protoModuleCrew.Count; j++)
                    {
                        ProtoCrewMember crewMember = part.protoModuleCrew[j];
                        if (crewMember == null) continue;
                        if (crewMember.type == ProtoCrewMember.KerbalType.Tourist)
                        {
                            List<TouristTask> tasks = new List<TouristTask>();
                            FindTasksForTourist(crewMember.name, tasks);
                            if (tasks.Count > 0)
                                touristTasks[crewMember.name] = tasks;
                        }
                    }
                }
            }
            else
            {
                foreach (ProtoCrewMember tourist in HighLogic.CurrentGame.CrewRoster.Kerbals(
                    ProtoCrewMember.KerbalType.Tourist, ProtoCrewMember.RosterStatus.Available))
                {
                    List<TouristTask> tasks = new List<TouristTask>();
                    FindTasksForTourist(tourist.name, tasks);
                    if (tasks.Count > 0)
                        touristTasks[tourist.name] = tasks;
                }
            }
        }

        private void FindTasksForTourist(string name, List<TouristTask> tasks)
        {
            if (ContractSystem.Instance == null) return;

            foreach (Contract contract in ContractSystem.Instance.Contracts)
            {
                if (contract.ContractState != Contract.State.Active) continue;

                foreach (ContractParameter param in contract.AllParameters)
                {
                    KerbalDestinationParameter dest = param as KerbalDestinationParameter;
                    if (dest == null) continue;
                    if (dest.kerbalName != name) continue;
                    if (!showComplete && dest.State != ParameterState.Incomplete) continue;
                    if (showComplete && dest.State != ParameterState.Incomplete && dest.State != ParameterState.Complete) continue;

                    string bodyName = dest.targetBody.displayName.Replace("^N", "");
                    tasks.Add(new TouristTask
                    {
                        Description = FormatTask(dest.targetType, bodyName),
                        IsComplete = dest.State == ParameterState.Complete,
                        Body = dest.targetBody
                    });

                    if (dest.State == ParameterState.Incomplete)
                    {
                        dest.OnStateChange.Add(OnParameterStateChange);
                        subscribedParams.Add(dest);
                    }
                }
            }
        }

        private string FormatTask(FlightLog.EntryType type, string body)
        {
            switch (type)
            {
                case FlightLog.EntryType.Land:     return "Land on " + body;
                case FlightLog.EntryType.Flyby:    return "Fly by " + body;
                case FlightLog.EntryType.Orbit:    return "Orbit around " + body;
                case FlightLog.EntryType.Suborbit: return "Suborbital flight on " + body;
                default: return type.ToString() + " " + body;
            }
        }

        private void BuildFilterOptions()
        {
            var sun = FlightGlobals.Bodies[0];
            var options = new List<string>();
            var optionsClean = new List<string>();
            var bodies = new List<CelestialBody>();
            var isPlanet = new List<bool>();
            var levels = new List<int>();
            var parents = new List<int>();

            options.Add("All");
            optionsClean.Add("All");
            bodies.Add(null);
            isPlanet.Add(false);
            levels.Add(0);
            parents.Add(-1);

            var planets = new List<CelestialBody>();
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];
                if (!body.isStar && body.referenceBody == sun)
                    planets.Add(body);
            }
            planets.Sort((a, b) => a.orbit.semiMajorAxis.CompareTo(b.orbit.semiMajorAxis));

            for (int p = 0; p < planets.Count; p++)
            {
                CelestialBody planet = planets[p];
                string planetName = planet.displayName.Replace("^N", "");

                int planetIdx = options.Count;
                options.Add(planetName);
                optionsClean.Add(planetName);
                bodies.Add(planet);
                isPlanet.Add(true);
                levels.Add(1);
                parents.Add(-1);

                var moons = planet.orbitingBodies;
                if (moons.Count > 0)
                {
                    moons.Sort((a, b) => a.orbit.semiMajorAxis.CompareTo(b.orbit.semiMajorAxis));
                    for (int m = 0; m < moons.Count; m++)
                    {
                        string moonName = moons[m].displayName.Replace("^N", "");
                        options.Add(moonName);
                        optionsClean.Add(moonName);
                        bodies.Add(moons[m]);
                        isPlanet.Add(false);
                        levels.Add(2);
                        parents.Add(planetIdx);
                    }
                }
            }

            filterDisplayNames = options.ToArray();
            filterDisplayNamesClean = optionsClean.ToArray();
            filterBodies = bodies.ToArray();
            filterIsPlanet = isPlanet.ToArray();
            filterTreeLevel = levels.ToArray();
            filterParentIndex = parents.ToArray();
        }

        private bool ShouldShowTask(TouristTask task)
        {
            if (selectedFilterIndex <= 0) return true;
            CelestialBody filterBody = filterBodies[selectedFilterIndex];
            if (filterBody == null) return true;
            if (filterIsPlanet[selectedFilterIndex])
                return task.Body == filterBody || task.Body.referenceBody == filterBody;
            return task.Body == filterBody;
        }

        void OnGUI()
        {
            if (isWindowOpen)
            {
                // GUI.skin = HighLogic.Skin;
                Rect prev = windowRect;
                windowRect = GUI.Window(windowID, windowRect, DrawWindow, "Are We There Yet?");
                if ((prev.x != windowRect.x || prev.y != windowRect.y) && Time.time - lastSaveTime > 5f)
                {
                    SaveWindowPosition();
                    lastSaveTime = Time.time;
                }
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            bool newShowComplete = GUILayout.Toggle(showComplete, "Show completed");
            if (newShowComplete != showComplete)
            {
                showComplete = newShowComplete;
                UpdateTouristData();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(filterDisplayNamesClean[selectedFilterIndex] + " ▼", GUILayout.Width(130)))
                showFilterDropdown = !showFilterDropdown;
            if (Event.current.type == EventType.Repaint)
                dropdownButtonRect = GUILayoutUtility.GetLastRect();
            GUILayout.EndHorizontal();

            if (touristTasks.Count == 0)
            {
                GUILayout.Label("No tourists with active contracts.");
            }
            else
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);

                int touristIndex = 0;
                foreach (var tourist in touristTasks)
                {
                    bool hasVisibleTasks = false;
                    for (int i = 0; i < tourist.Value.Count; i++)
                    {
                        if (ShouldShowTask(tourist.Value[i]))
                        {
                            hasVisibleTasks = true;
                            break;
                        }
                    }
                    if (!hasVisibleTasks) continue;

                    var rowStyle = touristIndex % 2 == 0 ? rowEvenStyle : rowOddStyle;
                    GUILayout.BeginVertical(rowStyle);

                    int taskIndex = 0;
                    for (int i = 0; i < tourist.Value.Count; i++)
                    {
                        TouristTask task = tourist.Value[i];
                        if (!ShouldShowTask(task)) continue;

                        GUILayout.BeginHorizontal();

                        if (taskIndex == 0)
                            GUILayout.Label($"<b>{tourist.Key}</b>", GUILayout.Width(120));
                        else
                            GUILayout.Label("", GUILayout.Width(120));

                        if (HighLogic.CurrentGame?.Parameters?.CustomParams<AWTYSettings>()?.showBodyIndicators ?? true)
                        {
                            string circles = "";
                            if (task.Body != null && task.Body.flightGlobalsIndex >= 0)
                            {
                                Color bodyCol = bodyColors[task.Body.flightGlobalsIndex];
                                if (bodyCol.a > 0)
                                {
                                    CelestialBody refBody = task.Body.referenceBody;
                                    if (refBody != null && !refBody.isStar && refBody.flightGlobalsIndex >= 0)
                                    {
                                        Color refCol = bodyColors[refBody.flightGlobalsIndex];
                                        if (refCol.a > 0)
                                            circles = $"<color=#{ColorUtility.ToHtmlStringRGB(refCol)}>●</color> ";
                                    }
                                    circles += $"<color=#{ColorUtility.ToHtmlStringRGB(bodyCol)}>●</color>";
                                }
                            }
                            GUILayout.Label(circles, GUILayout.Width(24));
                        }

                        if (task.IsComplete)
                            GUILayout.Label("<color=#44FF44>✓</color>", GUILayout.Width(10));
                        else
                            GUILayout.Label("", GUILayout.Width(10));

                        GUILayout.Label(task.Description);

                        // string icon = task.IsComplete ? "✓" : "✗";
                        // string iconColor = task.IsComplete ? "#44FF44" : "red";
                        // GUILayout.Label($"<color={iconColor}>{icon}</color> {task.Description}");

                        GUILayout.EndHorizontal();
                        taskIndex++;
                    }

                    GUILayout.EndVertical();
                    touristIndex++;
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();

            if (showFilterDropdown)
                DrawFilterDropdown();

            GUI.DragWindow();
        }

        private void DrawFilterDropdown()
        {
            if (filterItemStyle == null)
            {
                filterItemStyle = new GUIStyle(GUI.skin.label);
                filterItemStyle.alignment = TextAnchor.MiddleLeft;
                filterItemStyle.padding = new RectOffset(0, 0, 0, 0);
                filterItemStyle.margin = new RectOffset(0, 0, 0, 0);
                filterItemSelectedStyle = new GUIStyle(GUI.skin.label);
                filterItemSelectedStyle.alignment = TextAnchor.MiddleLeft;
                filterItemSelectedStyle.padding = new RectOffset(0, 0, 0, 0);
                filterItemSelectedStyle.margin = new RectOffset(0, 0, 0, 0);
                filterItemSelectedStyle.normal.textColor = Color.yellow;
            }

            if (dropdownBgStyle == null)
            {
                dropdownBgStyle = new GUIStyle(GUI.skin.box);
                dropdownBgStyle.normal.background = dropdownBgTex;
            }

            float x = dropdownButtonRect.x;
            float y = dropdownButtonRect.y + dropdownButtonRect.height;
            float w = 130;
            float lh = 18;
            float padV = 2;
            int n = filterDisplayNames.Length;
            float h = n * lh + padV * 2f;
            Rect areaRect = new Rect(x, y, w, h);

            GUI.Box(areaRect, "", dropdownBgStyle);

            float treeX = areaRect.x + 6;
            float textIndent = treeX + 10;

            int newIndex = selectedFilterIndex;

            for (int i = 0; i < n; i++)
            {
                if (filterTreeLevel[i] != 1) continue;
                int planetIdx = i;
                int firstMoon = -1;
                int lastMoon = -1;
                for (int j = i + 1; j < n && filterTreeLevel[j] == 2; j++)
                {
                    if (firstMoon < 0) firstMoon = j;
                    lastMoon = j;
                }
                if (firstMoon < 0) continue;

                float topY = areaRect.y + padV + planetIdx * lh + lh;
                float botY = areaRect.y + padV + lastMoon * lh + lh / 2f;
                GUI.DrawTexture(new Rect(treeX, topY, 1, botY - topY), treeLineTex);
            }

            for (int i = 0; i < n; i++)
            {
                float itemY = areaRect.y + padV + i * lh;
                float textX = (filterTreeLevel[i] == 2) ? textIndent : areaRect.x + padV + 2;
                float itemW = areaRect.x + w - padV - textX;
                Rect textRect = new Rect(textX, itemY, itemW, lh);
                Rect fullRect = new Rect(areaRect.x + padV, itemY, w - padV * 2, lh);
                bool hovered = fullRect.Contains(Event.current.mousePosition);

                if (hovered)
                    GUI.DrawTexture(new Rect(areaRect.x + padV, itemY, w - padV * 2, lh), filterHoverTex);

                if (filterTreeLevel[i] == 2)
                {
                    float centerY = itemY + lh / 2f;
                    bool isLast = (i + 1 >= n || filterTreeLevel[i + 1] != 2 || filterParentIndex[i + 1] != filterParentIndex[i]);

                    // float branchTop = centerY;
                    // float branchBot = isLast ? centerY : itemY + lh;
                    // if (branchBot > branchTop)
                    //     GUI.DrawTexture(new Rect(treeX, branchTop, 1, branchBot - branchTop), treeLineTex);

                    GUI.DrawTexture(new Rect(treeX + 1, centerY, textIndent - treeX - 4, 1), treeLineTex);
                }

                var style = (i == selectedFilterIndex) ? filterItemSelectedStyle : filterItemStyle;
                GUI.Label(textRect, filterDisplayNames[i], style);

                if (hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    newIndex = i;
                    Event.current.Use();
                }
            }

            if (newIndex != selectedFilterIndex)
            {
                selectedFilterIndex = newIndex;
                showFilterDropdown = false;
            }

            if (Event.current.type == EventType.MouseDown && !areaRect.Contains(Event.current.mousePosition))
                showFilterDropdown = false;
        }

        private static string GetSceneKey()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return "EDITOR";
            if (HighLogic.LoadedSceneIsFlight)
                return "FLIGHT";
            return "SPACECENTER";
        }

        private void RestoreWindowPosition()
        {
            if (AWTYSaveData.Instance == null || string.IsNullOrEmpty(sceneKey)) return;
            Vector2 pos = AWTYSaveData.GetPosition(sceneKey, windowRect.width, windowRect.height);
            windowRect.x = pos.x;
            windowRect.y = pos.y;
        }

        private void SaveWindowPosition()
        {
            if (AWTYSaveData.Instance == null) return;
            AWTYSaveData.SetPosition(sceneKey, new Vector2(windowRect.x, windowRect.y));
        }

        private void OnSceneLoadRequested(GameScenes scene)
        {
            SaveWindowPosition();
            positionRestored = false;
        }

        void Update()
        {
            if (!positionRestored)
            {
                if (AWTYSaveData.Instance == null || !AWTYSaveData.Instance.dataReady) return;
                RestoreWindowPosition();
                positionRestored = true;
            }

            if (dirty && isWindowOpen)
            {
                dirty = false;
                UpdateTouristData();
            }
        }

        private void OnParameterStateChange(ContractParameter param, ParameterState state)
        {
            dirty = true;
        }

        private void ClearSubscriptions()
        {
            for (int i = 0; i < subscribedParams.Count; i++)
                subscribedParams[i].OnStateChange.Remove(OnParameterStateChange);
            subscribedParams.Clear();
        }
    }
}
