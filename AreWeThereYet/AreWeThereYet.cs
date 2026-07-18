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
        private int selectedSourceIndex;
        private List<Vessel> sourceVessels;
        private List<ProtoVessel> sourceProtoVessels;
        private string[] sourceNames;
        private bool showSourceDropdown;
        private Rect sourceDropdownButtonRect;
        private Dictionary<VesselType, Texture2D> vesselIcons;
        private GUIStyle buttonIconStyle;
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
                    try
                    {
                        string name = node.GetValue("name");
                        bodyColorConfig[name] = new Color(
                            float.Parse(node.GetValue("colorR")), 
                            float.Parse(node.GetValue("colorG")),
                            float.Parse(node.GetValue("colorB")), 
                            1f
                        );
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AreWeThereYet] failed to load BODY_COLOR: {ex.Message}");
                    }
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
            GameEvents.onVesselPartCountChanged.Add(OnVesselPartCountChanged);
            GameEvents.onVesselChange.Add(OnVesselChange);

            if (ApplicationLauncher.Ready && appButton == null)
                OnAppLauncherReady();

            BuildFilterOptions();

            vesselIcons = new Dictionary<VesselType, Texture2D>();
            string[] vesselTypeNames = Enum.GetNames(typeof(VesselType));
            for (int i = 0; i < vesselTypeNames.Length; i++)
            {
                Texture2D tex = GameDatabase.Instance.GetTexture("AreWeThereYet/Textures/VT" + vesselTypeNames[i], false);
                if (tex != null)
                    vesselIcons[(VesselType)Enum.Parse(typeof(VesselType), vesselTypeNames[i])] = tex;
            }

            BuildSourceOptions();
            positionRestored = false;
        }

        void OnDestroy()
        {
            SaveWindowPosition();
            ClearSubscriptions();
            GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnAppLauncherDestroyed);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneLoadRequested);
            GameEvents.onVesselPartCountChanged.Remove(OnVesselPartCountChanged);
            GameEvents.onVesselChange.Remove(OnVesselChange);
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
            if (vesselIcons != null)
                vesselIcons.Clear();
        }

        private void OnAppLauncherReady()
        {
            if (appButton != null)
                return;

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
            if (appButton == null)
                return;

            ApplicationLauncher.Instance.RemoveModApplication(appButton);
            appButton = null;
        }

        private void OnToggleTrue()
        {
            BuildSourceOptions();
            UpdateTouristData();
            isWindowOpen = true;

            // Debug.Log("[AreWeThereYet] === Vessel dump ===");
            // for (int i = 0; i < FlightGlobals.Vessels.Count; i++)
            // {
            //     Vessel v = FlightGlobals.Vessels[i];
            //     string crewList = "[]";
            //     if (v.parts != null)
            //     {
            //         crewList = "";
            //         for (int j = 0; j < v.parts.Count; j++)
            //         {
            //             Part p = v.parts[j];
            //             if (p == null || p.protoModuleCrew == null)
            //                 continue;

            //             for (int k = 0; k < p.protoModuleCrew.Count; k++)
            //             {
            //                 ProtoCrewMember c = p.protoModuleCrew[k];
            //                 if (c == null)
            //                     continue;

            //                 if (crewList.Length > 0)
            //                     crewList += ",";

            //                 crewList += c.name + "(" + c.type + ")";
            //             }
            //         }
            //         if (crewList.Length == 0)
            //             crewList = "[]";
            //     }
            //     Debug.Log("[AreWeThereYet]   Vessel: '" + v.vesselName
            //         + "' loaded=" + v.loaded
            //         + " parts=" + (v.parts?.Count ?? -1)
            //         + " crew=[" + crewList + "]"
            //     );
            // }
            // if (HighLogic.CurrentGame?.flightState?.protoVessels != null)
            // {
            //     for (int i = 0; i < HighLogic.CurrentGame.flightState.protoVessels.Count; i++)
            //     {
            //         ProtoVessel pv = HighLogic.CurrentGame.flightState.protoVessels[i];
            //         bool inVessels = false;
            //         for (int j = 0; j < FlightGlobals.Vessels.Count; j++)
            //         {
            //             if (FlightGlobals.Vessels[j].vesselName == pv.vesselName)
            //             { 
            //                 inVessels = true; 
            //                 break; 
            //             }
            //         }

            //         string crewList = "[]";
            //         if (pv.protoPartSnapshots != null)
            //         {
            //             crewList = "";
            //             for (int j = 0; j < pv.protoPartSnapshots.Count; j++)
            //             {
            //                 ProtoPartSnapshot pps = pv.protoPartSnapshots[j];
            //                 if (pps == null)
            //                     continue;

            //                 if (pps.protoModuleCrew != null)
            //                 {
            //                     for (int k = 0; k < pps.protoModuleCrew.Count; k++)
            //                     {
            //                         ProtoCrewMember c = pps.protoModuleCrew[k];
            //                         if (c == null)
            //                             continue;

            //                         if (crewList.Length > 0)
            //                             crewList += ",";
                                        
            //                         crewList += c.name + "(" + c.type + ")";
            //                     }
            //                 }
            //                 else if (pps.protoCrewNames != null)
            //                 {
            //                     for (int k = 0; k < pps.protoCrewNames.Count; k++)
            //                     {
            //                         if (crewList.Length > 0)
            //                             crewList += ",";

            //                         crewList += pps.protoCrewNames[k] + "(name)";
            //                     }
            //                 }
            //             }
            //             if (crewList.Length == 0)
            //                 crewList = "[]";
            //         }
            //         Debug.Log("[AreWeThereYet]   ProtoVessel: '" + pv.vesselName
            //             + "' inVessels=" + inVessels
            //             + " parts=" + (pv.protoPartSnapshots?.Count ?? -1)
            //             + " crew=[" + crewList + "]"
            //         );
            //     }
            // }
            // Debug.Log("[AreWeThereYet] === End vessel dump ===");
        }

        private void OnToggleFalse() => isWindowOpen = false;

        private void UpdateTouristData()
        {
            ClearSubscriptions();
            touristTasks.Clear();

            int kscIdx = HighLogic.LoadedSceneIsFlight ? 1 : 0;

            if (HighLogic.LoadedSceneIsFlight && selectedSourceIndex == 0)
            {
                Vessel vessel = FlightGlobals.ActiveVessel;
                if (vessel != null)
                    CollectTouristsFromVessel(vessel);
            }
            else if (selectedSourceIndex == kscIdx)
            {
                foreach (ProtoCrewMember tourist in HighLogic.CurrentGame.CrewRoster.Kerbals(
                    ProtoCrewMember.KerbalType.Tourist, ProtoCrewMember.RosterStatus.Available
                ))
                {
                    List<TouristTask> tasks = new List<TouristTask>();
                    FindTasksForTourist(tourist.name, tasks);
                    if (tasks.Count > 0)
                        touristTasks[tourist.name] = tasks;
                }
            }
            else if (sourceVessels != null && selectedSourceIndex >= 0 && selectedSourceIndex < sourceVessels.Count)
            {
                Vessel vessel = sourceVessels[selectedSourceIndex];
                bool vesselUsable = vessel != null && vessel.loaded && vessel.parts != null && vessel.parts.Count > 0;
                if (vesselUsable)
                {
                    CollectTouristsFromVessel(vessel);
                }
                else if (sourceProtoVessels != null && selectedSourceIndex < sourceProtoVessels.Count)
                {
                    ProtoVessel pv = sourceProtoVessels[selectedSourceIndex];
                    if (pv != null)
                        CollectTouristsFromProtoVessel(pv);
                }
            }
        }

        private void CollectTouristsFromVessel(Vessel vessel)
        {
            if (vessel.parts == null)
                return;

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part == null || part.protoModuleCrew == null)
                    continue;

                for (int j = 0; j < part.protoModuleCrew.Count; j++)
                {
                    ProtoCrewMember crewMember = part.protoModuleCrew[j];
                    if (crewMember == null)
                        continue;

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

        private void CollectTouristsFromProtoVessel(ProtoVessel pv)
        {
            if (pv.protoPartSnapshots == null)
                return;

            for (int i = 0; i < pv.protoPartSnapshots.Count; i++)
            {
                CollectTouristsFromProtoPartSnapshot(pv.protoPartSnapshots[i]);
            }
        }

        private void CollectTouristsFromProtoPartSnapshot(ProtoPartSnapshot pps)
        {
            if (pps == null)
                return;

            if (pps.protoModuleCrew != null)
            {
                for (int j = 0; j < pps.protoModuleCrew.Count; j++)
                {
                    ProtoCrewMember crewMember = pps.protoModuleCrew[j];
                    if (crewMember == null || crewMember.type != ProtoCrewMember.KerbalType.Tourist)
                        continue;

                    List<TouristTask> tasks = new List<TouristTask>();
                    FindTasksForTourist(crewMember.name, tasks);
                    if (tasks.Count > 0)
                        touristTasks[crewMember.name] = tasks;
                }
            }
            else if (pps.protoCrewNames != null)
            {
                for (int j = 0; j < pps.protoCrewNames.Count; j++)
                {
                    ProtoCrewMember crewMember = FindCrewByName(pps.protoCrewNames[j]);
                    if (crewMember == null || crewMember.type != ProtoCrewMember.KerbalType.Tourist)
                        continue;

                    List<TouristTask> tasks = new List<TouristTask>();
                    FindTasksForTourist(crewMember.name, tasks);
                    if (tasks.Count > 0)
                        touristTasks[crewMember.name] = tasks;
                }
            }
        }

        private void BuildSourceOptions()
        {
            List<Vessel> vessels = new List<Vessel>();
            List<string> names = new List<string>();

            if (HighLogic.LoadedSceneIsFlight)
            {
                vessels.Add(null);
                names.Add("Active Vessel");
            }

            vessels.Add(null);
            names.Add("KSC");

            List<string> sortNames = new List<string>();
            List<Vessel> sortVessels = new List<Vessel>();
            List<ProtoVessel> sortProtoVessels = new List<ProtoVessel>();

            if (HighLogic.LoadedSceneIsFlight)
            {
                for (int i = 0; i < FlightGlobals.Vessels.Count; i++)
                {
                    try
                    {
                        Vessel v = FlightGlobals.Vessels[i];
                        if (v == FlightGlobals.ActiveVessel || !HasTouristWithMatchingTask(v))
                            continue;

                        sortVessels.Add(v);
                        sortNames.Add(v.vesselName);
                        sortProtoVessels.Add(null);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AreWeThereYet] BuildSourceOptions vessel error: {ex.Message}");
                    }
                }
            }

            if (HighLogic.CurrentGame?.flightState?.protoVessels != null)
            {
                for (int i = 0; i < HighLogic.CurrentGame.flightState.protoVessels.Count; i++)
                {
                    try
                    {
                        ProtoVessel pv = HighLogic.CurrentGame.flightState.protoVessels[i];
                        if (HighLogic.LoadedSceneIsFlight && pv.vesselName == FlightGlobals.ActiveVessel?.vesselName)
                            continue;

                        if (sortNames.Contains(pv.vesselName))
                            continue;

                        bool hasTourist = HasTouristOnProtoVessel(pv);
                        if (!hasTourist)
                            continue;

                        Vessel match = null;
                        for (int j = 0; j < FlightGlobals.Vessels.Count; j++)
                        {
                            if (FlightGlobals.Vessels[j].vesselName == pv.vesselName)
                            {
                                match = FlightGlobals.Vessels[j];
                                break;
                            }
                        }
                        sortVessels.Add(match);
                        sortNames.Add(pv.vesselName);
                        sortProtoVessels.Add(pv);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[AreWeThereYet] BuildSourceOptions protoVessel error: " + ex.Message);
                    }
                }
            }

            for (int i = 0; i < sortNames.Count; i++)
            {
                for (int j = i + 1; j < sortNames.Count; j++)
                {
                    if (string.Compare(sortNames[i], sortNames[j], StringComparison.Ordinal) > 0)
                    {
                        string tmpN = sortNames[i];
                        sortNames[i] = sortNames[j];
                        sortNames[j] = tmpN;
                        Vessel tmpV = sortVessels[i];
                        sortVessels[i] = sortVessels[j];
                        sortVessels[j] = tmpV;
                        ProtoVessel tmpP = sortProtoVessels[i];
                        sortProtoVessels[i] = sortProtoVessels[j];
                        sortProtoVessels[j] = tmpP;
                    }
                }
            }

            for (int i = 0; i < sortNames.Count; i++)
            {
                vessels.Add(sortVessels[i]);
                names.Add(sortNames[i]);
            }

            int kscIdx = HighLogic.LoadedSceneIsFlight ? 1 : 0;
            sourceProtoVessels = new List<ProtoVessel>(vessels.Count);
            for (int i = 0; i <= kscIdx; i++)
                sourceProtoVessels.Add(null);
            for (int i = 0; i < sortNames.Count; i++)
                sourceProtoVessels.Add(sortProtoVessels[i]);

            sourceVessels = vessels;
            sourceNames = names.ToArray();

            if (selectedSourceIndex >= sourceNames.Length)
                selectedSourceIndex = kscIdx;
        }

        private bool HasTouristOnProtoVessel(ProtoVessel pv)
        {
            if (pv.protoPartSnapshots == null)
                return false;

            for (int i = 0; i < pv.protoPartSnapshots.Count; i++)
            {
                ProtoPartSnapshot pps = pv.protoPartSnapshots[i];
                if (pps == null)
                    continue;

                if (pps.protoModuleCrew != null)
                {
                    for (int j = 0; j < pps.protoModuleCrew.Count; j++)
                    {
                        ProtoCrewMember crew = pps.protoModuleCrew[j];
                        if (crew != null && crew.type == ProtoCrewMember.KerbalType.Tourist)
                            return true;
                    }
                }
                else if (pps.protoCrewNames != null)
                {
                    for (int j = 0; j < pps.protoCrewNames.Count; j++)
                    {
                        ProtoCrewMember crew = FindCrewByName(pps.protoCrewNames[j]);
                        if (crew != null && crew.type == ProtoCrewMember.KerbalType.Tourist)
                            return true;
                    }
                }
            }

            return false;
        }

        private ProtoCrewMember FindCrewByName(string name)
        {
            var roster = HighLogic.CurrentGame?.CrewRoster;
            if (roster == null)
                return null;

            foreach (ProtoCrewMember c in roster.Crew)
            {
                if (c.name == name)
                    return c;
            }

            return null;
        }

        private bool HasTouristWithMatchingTask(Vessel v)
        {
            if (v.parts == null)
                return false;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part part = v.parts[i];
                if (part == null || part.protoModuleCrew == null)
                    continue;

                for (int j = 0; j < part.protoModuleCrew.Count; j++)
                {
                    ProtoCrewMember crew = part.protoModuleCrew[j];
                    if (crew != null && crew.type == ProtoCrewMember.KerbalType.Tourist)
                        return true;
                }
            }

            return false;
        }

        private void FindTasksForTourist(string name, List<TouristTask> tasks)
        {
            if (ContractSystem.Instance == null)
                return;

            foreach (Contract contract in ContractSystem.Instance.Contracts)
            {
                if (contract.ContractState != Contract.State.Active)
                    continue;

                foreach (ContractParameter param in contract.AllParameters)
                {
                    KerbalDestinationParameter dest = param as KerbalDestinationParameter;
                    if (dest == null || dest.kerbalName != name)
                        continue;
                    if (!showComplete && dest.State != ParameterState.Incomplete)
                        continue;
                    if (showComplete && dest.State != ParameterState.Incomplete && dest.State != ParameterState.Complete)
                        continue;

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
            if (selectedFilterIndex <= 0)
                return true;
            CelestialBody filterBody = filterBodies[selectedFilterIndex];
            if (filterBody == null)
                return true;
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

            bool newShowComplete = GUILayout.Toggle(showComplete, "Show completed");
            if (newShowComplete != showComplete)
            {
                showComplete = newShowComplete;
                UpdateTouristData();
            }

            if (buttonIconStyle == null)
            {
                buttonIconStyle = new GUIStyle(GUI.skin.button);
                buttonIconStyle.imagePosition = ImagePosition.ImageLeft;
                buttonIconStyle.richText = true;
                buttonIconStyle.fixedHeight = 24;
                buttonIconStyle.padding.top = 2;
                buttonIconStyle.padding.bottom = 2;
            }

            GUILayout.BeginHorizontal();
            {
                string srcLabel = selectedSourceIndex < sourceNames.Length
                    ? sourceNames[selectedSourceIndex] : "KSC";

                Texture2D srcIcon = null;
                if (HighLogic.LoadedSceneIsFlight && selectedSourceIndex == 0)
                {
                    if (FlightGlobals.ActiveVessel != null)
                        vesselIcons.TryGetValue(FlightGlobals.ActiveVessel.vesselType, out srcIcon);
                }
                else if (sourceVessels != null && selectedSourceIndex < sourceVessels.Count)
                {
                    Vessel v = sourceVessels[selectedSourceIndex];
                    if (v != null)
                        vesselIcons.TryGetValue(v.vesselType, out srcIcon);
                }

                string soiCircles = GetSoiIndicatorString(selectedSourceIndex);
                string btnLabel = srcLabel + " ▼";
                if (!string.IsNullOrEmpty(soiCircles))
                    btnLabel = soiCircles + " " + btnLabel;

                GUIContent btnContent = srcIcon != null
                    ? new GUIContent(btnLabel, srcIcon)
                    : new GUIContent(btnLabel);

                if (GUILayout.Button(btnContent, buttonIconStyle, GUILayout.Width(200)))
                {
                    showSourceDropdown = !showSourceDropdown;
                    if (showFilterDropdown)
                        showFilterDropdown = false;
                }

                if (Event.current.type == EventType.Repaint)
                    sourceDropdownButtonRect = GUILayoutUtility.GetLastRect();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(filterDisplayNamesClean[selectedFilterIndex] + " ▼", buttonIconStyle, GUILayout.Width(130)))
            {
                showFilterDropdown = !showFilterDropdown;
                if (showSourceDropdown)
                    showSourceDropdown = false;
            }
            
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
                    if (!hasVisibleTasks)
                        continue;

                    var rowStyle = touristIndex % 2 == 0 ? rowEvenStyle : rowOddStyle;
                    bool allComplete = true;
                    for (int i = 0; i < tourist.Value.Count; i++)
                    {
                        if (!tourist.Value[i].IsComplete)
                        {
                            allComplete = false;
                            break;
                        }
                    }

                    GUILayout.BeginVertical(rowStyle);

                    int taskIndex = 0;
                    for (int i = 0; i < tourist.Value.Count; i++)
                    {
                        TouristTask task = tourist.Value[i];
                        if (!ShouldShowTask(task))
                            continue;

                        GUILayout.BeginHorizontal();

                        if (taskIndex == 0)
                        {
                            string nameTag = allComplete
                                ? $"<color=#44FF44><b>{tourist.Key}</b></color>"
                                : $"<b>{tourist.Key}</b>";
                            GUILayout.Label(nameTag, GUILayout.Width(120));
                        }
                        else
                            GUILayout.Label("", GUILayout.Width(120));

                        if (HighLogic.CurrentGame?.Parameters?.CustomParams<AWTYSettings>()?.showBodyIndicators ?? true)
                        {
                            string circles = BuildBodyCircles(task.Body);
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

            if (showSourceDropdown)
                DrawSourceDropdown();

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
                if (filterTreeLevel[i] != 1)
                    continue;
                int planetIdx = i;
                int firstMoon = -1;
                int lastMoon = -1;
                for (int j = i + 1; j < n && filterTreeLevel[j] == 2; j++)
                {
                    if (firstMoon < 0)
                        firstMoon = j;
                    lastMoon = j;
                }
                if (firstMoon < 0)
                    continue;

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
                }
            }

            if (Event.current.type == EventType.MouseDown)
            {
                if (areaRect.Contains(Event.current.mousePosition))
                {
                    if (newIndex != selectedFilterIndex)
                    {
                        selectedFilterIndex = newIndex;
                        dirty = true;
                    }
                    showFilterDropdown = false;
                    Event.current.Use();
                }
                else
                {
                    showFilterDropdown = false;
                }
            }
        }

        private string BuildBodyCircles(CelestialBody body)
        {
            if (body == null || body.flightGlobalsIndex < 0)
                return "";

            Color bodyCol = bodyColors[body.flightGlobalsIndex];
            if (bodyCol.a <= 0)
                return "";

            string circles = "";
            CelestialBody refBody = body.referenceBody;
            if (refBody != null && !refBody.isStar && refBody.flightGlobalsIndex >= 0)
            {
                Color refCol = bodyColors[refBody.flightGlobalsIndex];
                if (refCol.a > 0)
                    circles = $"<color=#{ColorUtility.ToHtmlStringRGB(refCol)}>●</color> ";
            }
            circles += $"<color=#{ColorUtility.ToHtmlStringRGB(bodyCol)}>●</color>";

            return circles;
        }

        private string GetSoiIndicatorString(int sourceIndex)
        {
            CelestialBody body = null;

            if (HighLogic.LoadedSceneIsFlight && sourceIndex == 0)
            {
                if (FlightGlobals.ActiveVessel != null)
                    body = FlightGlobals.ActiveVessel.mainBody;
            }
            else if (sourceVessels != null && sourceIndex < sourceVessels.Count)
            {
                Vessel v = sourceVessels[sourceIndex];
                if (v != null)
                {
                    body = v.mainBody;
                }
                else if (sourceProtoVessels != null && sourceIndex < sourceProtoVessels.Count)
                {
                    ProtoVessel pv = sourceProtoVessels[sourceIndex];
                    if (pv != null && pv.orbitSnapShot != null)
                    {
                        int refBodyIdx = pv.orbitSnapShot.ReferenceBodyIndex;
                        if (refBodyIdx >= 0 && refBodyIdx < FlightGlobals.Bodies.Count)
                            body = FlightGlobals.Bodies[refBodyIdx];
                    }
                }
            }

            string circles = BuildBodyCircles(body);
            if (string.IsNullOrEmpty(circles))
                return "";

            return " " + circles;
        }

        private void DrawSourceDropdown()
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
                filterItemStyle.richText = true;
                filterItemSelectedStyle.richText = true;
            }

            if (dropdownBgStyle == null)
            {
                dropdownBgStyle = new GUIStyle(GUI.skin.box);
                dropdownBgStyle.normal.background = dropdownBgTex;
            }

            float x = sourceDropdownButtonRect.x;
            float y = sourceDropdownButtonRect.y + sourceDropdownButtonRect.height;
            float w = sourceDropdownButtonRect.width;
            float lh = 18;
            float padV = 2;
            float iconSize = 20;
            int n = sourceNames.Length;
            float h = n * lh + padV * 2f;
            Rect areaRect = new Rect(x, y, w, h);

            GUI.Box(areaRect, "", dropdownBgStyle);

            int newIndex = selectedSourceIndex;

            for (int i = 0; i < n; i++)
            {
                float itemY = areaRect.y + padV + i * lh;
                float iconX = areaRect.x + 6;
                float circlesX = iconX + iconSize + 4;
                float circlesW = 32;
                float textX = circlesX + circlesW + 4;
                float itemW = areaRect.x + w - padV - textX;
                Rect textRect = new Rect(textX, itemY, itemW, lh);
                Rect fullRect = new Rect(areaRect.x + padV, itemY, w - padV * 2, lh);
                bool hovered = fullRect.Contains(Event.current.mousePosition);

                if (hovered)
                    GUI.DrawTexture(fullRect, filterHoverTex);

                Vessel v = sourceVessels[i];
                if (v != null)
                {
                    Texture2D icon;
                    if (vesselIcons.TryGetValue(v.vesselType, out icon))
                        GUI.DrawTexture(new Rect(iconX, itemY + (lh - iconSize) / 2f, iconSize, iconSize), icon);
                }

                if (HighLogic.CurrentGame?.Parameters?.CustomParams<AWTYSettings>()?.showBodyIndicators ?? true)
                {
                    string soiCircles = GetSoiIndicatorString(i);
                    if (!string.IsNullOrEmpty(soiCircles))
                        GUI.Label(new Rect(circlesX, itemY, circlesW, lh), soiCircles, filterItemStyle);
                }

                var style = (i == selectedSourceIndex) ? filterItemSelectedStyle : filterItemStyle;
                GUI.Label(textRect, sourceNames[i], style);

                if (hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    newIndex = i;
                }
            }

            if (Event.current.type == EventType.MouseDown)
            {
                // Debug.Log("[AreWeThereYet] DrawSourceDropdown: MouseDown event"
                //     + " mousePos=" + Event.current.mousePosition
                //     + " areaRect=" + areaRect
                //     + " contains=" + areaRect.Contains(Event.current.mousePosition)
                //     + " newIndex=" + newIndex
                //     + " selectedSourceIndex=" + selectedSourceIndex
                //     + " showSourceDropdown=" + showSourceDropdown);
                if (areaRect.Contains(Event.current.mousePosition))
                {
                    if (newIndex != selectedSourceIndex)
                    {
                        selectedSourceIndex = newIndex;
                        dirty = true;
                        // Debug.Log("[AreWeThereYet] DrawSourceDropdown: SET selectedSourceIndex=" + selectedSourceIndex);
                    }
                    showSourceDropdown = false;
                    Event.current.Use();
                }
                else
                {
                    showSourceDropdown = false;
                    // Debug.Log("[AreWeThereYet] DrawSourceDropdown: click outside, closing");
                }
            }
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
            if (AWTYSaveData.Instance == null || string.IsNullOrEmpty(sceneKey))
                return;
            Vector2 pos = AWTYSaveData.GetPosition(sceneKey, windowRect.width, windowRect.height);
            windowRect.x = pos.x;
            windowRect.y = pos.y;
        }

        private void SaveWindowPosition()
        {
            if (AWTYSaveData.Instance == null)
                return;
            AWTYSaveData.SetPosition(sceneKey, new Vector2(windowRect.x, windowRect.y));
        }

        private void OnSceneLoadRequested(GameScenes scene)
        {
            SaveWindowPosition();
            positionRestored = false;
        }

        private void OnVesselPartCountChanged(Vessel v)
        {
            dirty = true;
        }

        private void OnVesselChange(Vessel v)
        {
            dirty = true;
        }

        void Update()
        {
            if (!positionRestored)
            {
                if (AWTYSaveData.Instance == null || !AWTYSaveData.Instance.dataReady)
                    return;
                RestoreWindowPosition();
                positionRestored = true;
            }

            if (dirty && isWindowOpen)
            {
                dirty = false;
                BuildSourceOptions();
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
            {
                subscribedParams[i].OnStateChange.Remove(OnParameterStateChange);
            }
            subscribedParams.Clear();
        }
    }
}
