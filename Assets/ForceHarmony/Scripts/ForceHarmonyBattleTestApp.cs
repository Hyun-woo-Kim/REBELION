using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ForceHarmony
{
    public sealed class ForceHarmonyBattleTestApp : MonoBehaviour
    {
        private ForceHarmonyDatabase database;
        private ForceHarmonyBattleSimulator simulator;
        private readonly List<UnitSlotView> allySlots = new List<UnitSlotView>();
        private readonly List<UnitSlotView> enemySlots = new List<UnitSlotView>();

        private Canvas rootCanvas;
        private RectTransform menuRoot;
        private RectTransform battleRoot;
        private Text titleText;
        private Text statusText;
        private Text turnBannerText;
        private Text actionDirectionText;
        private Text actionSummaryText;
        private Text timelineText;
        private Text logText;
        private RectTransform commandPanel;
        private Text commandTitleText;
        private Text boostText;
        private Button basicAttackButton;
        private Button tacticalSkillButton;
        private Button guardButton;
        private Button waitButton;
        private Button ultimateButton;
        private Button boostDownButton;
        private Button boostUpButton;
        private Button autoButton;
        private Button stepButton;
        private Button qteButton;
        private Text qteText;
        private float autoStepTimer;
        private bool autoBattle = true;
        private int selectedBoostLevel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ForceHarmonyBattleTestApp>() != null)
            {
                return;
            }

            var app = new GameObject("ForceHarmony Battle Test App");
            app.AddComponent<ForceHarmonyBattleTestApp>();
            DontDestroyOnLoad(app);
        }

        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();

            database = ForceHarmonyDatabase.LoadFromResources();
            simulator = new ForceHarmonyBattleSimulator(database);
            BuildUi();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "BattleTestScene")
            {
                StartStage(3);
            }
            else
            {
                ShowMenu();
            }

            foreach (var warning in database.Warnings)
            {
                Debug.LogWarning($"[ForceHarmony] {warning}");
            }
        }

        private void Update()
        {
            if (battleRoot == null || !battleRoot.gameObject.activeSelf)
            {
                return;
            }

            simulator.UpdateQteTimer(Time.unscaledDeltaTime);

            if (autoBattle && !simulator.HasPendingQte && !simulator.BattleEnded)
            {
                autoStepTimer += Time.deltaTime;
                if (autoStepTimer >= 1.1f)
                {
                    autoStepTimer = 0f;
                    simulator.StepAutoAction();
                }
            }

            RefreshBattleUi();
        }

        private void ShowMenu()
        {
            menuRoot.gameObject.SetActive(true);
            battleRoot.gameObject.SetActive(false);
        }

        private void StartStage(int stageId)
        {
            if (!database.Stages.TryGetValue(stageId, out var stage))
            {
                Debug.LogWarning($"[ForceHarmony] Missing stage: {stageId}");
                return;
            }

            simulator.StartStage(stage);
            menuRoot.gameObject.SetActive(false);
            battleRoot.gameObject.SetActive(true);
            autoStepTimer = 0f;
            autoBattle = true;
            selectedBoostLevel = 0;
            titleText.text = database.Text(stage.StringId, stage.StageName);
            RefreshBattleUi();
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("ForceHarmony Canvas");
            canvasObject.AddComponent<RectTransform>();
            canvasObject.transform.SetParent(transform, false);
            rootCanvas = canvasObject.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            menuRoot = CreatePanel("Outgame Stage Select", rootCanvas.transform as RectTransform, StretchFull, new Color(0.06f, 0.07f, 0.09f, 0.98f));
            battleRoot = CreatePanel("Ingame Battle Test", rootCanvas.transform as RectTransform, StretchFull, new Color(0.055f, 0.06f, 0.075f, 0.98f));
            BuildMenuUi();
            BuildBattleUi();
        }

        private void BuildMenuUi()
        {
            var header = CreateText("Project Force Harmony", menuRoot, 52, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.93f, 0.96f, 1f));
            SetRect(header.rectTransform, new Vector2(70, -120), new Vector2(-70, -40), new Vector2(0, 1), new Vector2(1, 1));

            var subtitle = CreateText("전투 테스트 스테이지", menuRoot, 34, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.70f, 0.86f, 0.98f));
            SetRect(subtitle.rectTransform, new Vector2(80, -205), new Vector2(-80, -145), new Vector2(0, 1), new Vector2(1, 1));

            var y = -370f;
            CreateStageButton("적 1명 배치 테스트", "1 Enemy", 1, y);
            CreateStageButton("적 3명 배치 테스트", "3 Enemies", 3, y - 170f);
            CreateStageButton("적 5명 배치 테스트", "5 Enemies", 5, y - 340f);

            var parserInfo = CreateText("CSV parser: rows 1-4 metadata, row 5+ live data, semicolon list enabled", menuRoot, 25, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.65f, 0.70f, 0.76f));
            SetRect(parserInfo.rectTransform, new Vector2(70, 80), new Vector2(-70, 180), new Vector2(0, 0), new Vector2(1, 0));
        }

        private void CreateStageButton(string korean, string english, int stageId, float anchoredY)
        {
            var button = CreateButton($"{korean}\n{english}", menuRoot, () => StartStage(stageId), new Color(0.16f, 0.27f, 0.36f), new Color(0.83f, 0.93f, 1f));
            SetRect(button.GetComponent<RectTransform>(), new Vector2(120, anchoredY - 110f), new Vector2(-120, anchoredY), new Vector2(0, 1), new Vector2(1, 1));
        }

        private void BuildBattleUi()
        {
            titleText = CreateText("Battle Test", battleRoot, 38, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.93f, 0.96f, 1f));
            SetRect(titleText.rectTransform, new Vector2(36, -82), new Vector2(-240, -24), new Vector2(0, 1), new Vector2(1, 1));

            var backButton = CreateButton("Back", battleRoot, ShowMenu, new Color(0.20f, 0.18f, 0.22f), Color.white);
            SetRect(backButton.GetComponent<RectTransform>(), new Vector2(-210, -88), new Vector2(-36, -26), new Vector2(1, 1), new Vector2(1, 1));

            statusText = CreateText("", battleRoot, 26, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.73f, 0.83f, 0.91f));
            SetRect(statusText.rectTransform, new Vector2(38, -150), new Vector2(-38, -94), new Vector2(0, 1), new Vector2(1, 1));

            var turnPanel = CreatePanel("Turn Flow Panel", battleRoot, StretchNone, new Color(0.095f, 0.105f, 0.125f, 0.96f));
            SetRect(turnPanel, new Vector2(38, -270), new Vector2(-38, -170), new Vector2(0, 1), new Vector2(1, 1));

            turnBannerText = CreateText("BATTLE READY", turnPanel, 32, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            SetRect(turnBannerText.rectTransform, new Vector2(24, 0), new Vector2(-500, 0), new Vector2(0, 0), new Vector2(1, 1));

            actionDirectionText = CreateText("ALLY -> ENEMY", turnPanel, 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.91f, 0.55f));
            SetRect(actionDirectionText.rectTransform, new Vector2(360, 0), new Vector2(-360, 0), new Vector2(0, 0), new Vector2(1, 1));

            actionSummaryText = CreateText("Waiting for first action.", turnPanel, 22, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.78f, 0.86f, 0.92f));
            SetRect(actionSummaryText.rectTransform, new Vector2(510, 8), new Vector2(-24, -8), new Vector2(0, 0), new Vector2(1, 1));

            for (var i = 0; i < 3; i++)
            {
                allySlots.Add(CreateUnitSlot($"Ally Slot {i + 1}", battleRoot, true, i));
            }

            for (var i = 0; i < 5; i++)
            {
                enemySlots.Add(CreateUnitSlot($"Enemy Slot {i + 1}", battleRoot, false, i));
            }

            timelineText = CreateText("", battleRoot, 27, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.92f, 0.91f, 0.80f));
            var timelinePanel = WrapWithPanel(timelineText.rectTransform, "Timeline", new Color(0.10f, 0.105f, 0.12f, 0.94f));
            SetRect(timelinePanel, new Vector2(38, 470), new Vector2(-38, 690), new Vector2(0, 0), new Vector2(1, 0));
            SetRect(timelineText.rectTransform, new Vector2(24, 20), new Vector2(-24, -20), new Vector2(0, 0), new Vector2(1, 1));

            logText = CreateText("", battleRoot, 23, FontStyle.Normal, TextAnchor.LowerLeft, new Color(0.78f, 0.86f, 0.88f));
            var logPanel = WrapWithPanel(logText.rectTransform, "Battle Log", new Color(0.08f, 0.085f, 0.10f, 0.94f));
            SetRect(logPanel, new Vector2(38, 40), new Vector2(-38, 430), new Vector2(0, 0), new Vector2(1, 0));
            SetRect(logText.rectTransform, new Vector2(24, 18), new Vector2(-24, -18), new Vector2(0, 0), new Vector2(1, 1));

            BuildCommandPanel();

            autoButton = CreateButton("AUTO ON", battleRoot, ToggleAutoBattle, new Color(0.12f, 0.28f, 0.20f), Color.white);
            SetRect(autoButton.GetComponent<RectTransform>(), new Vector2(38, 730), new Vector2(250, 810), new Vector2(0, 0), new Vector2(0, 0));

            stepButton = CreateButton("STEP TURN", battleRoot, StepTurn, new Color(0.18f, 0.23f, 0.34f), Color.white);
            SetRect(stepButton.GetComponent<RectTransform>(), new Vector2(270, 730), new Vector2(500, 810), new Vector2(0, 0), new Vector2(0, 0));

            qteButton = CreateButton("FORCE CHAIN", battleRoot, () => simulator.ResolveForceChain(), new Color(0.60f, 0.14f, 0.18f), Color.white);
            qteText = qteButton.GetComponentInChildren<Text>();
            SetRect(qteButton.GetComponent<RectTransform>(), new Vector2(530, 730), new Vector2(-38, 850), new Vector2(0, 0), new Vector2(1, 0));
        }

        private void BuildCommandPanel()
        {
            commandPanel = CreatePanel("Command Panel UI", battleRoot, StretchNone, new Color(0.075f, 0.085f, 0.105f, 0.98f));
            SetRect(commandPanel, new Vector2(38, 860), new Vector2(-38, 1110), new Vector2(0, 0), new Vector2(1, 0));

            commandTitleText = CreateText("Command Input", commandPanel, 28, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.90f, 0.95f, 1f));
            SetRect(commandTitleText.rectTransform, new Vector2(24, -66), new Vector2(-24, -12), new Vector2(0, 1), new Vector2(1, 1));

            boostDownButton = CreateButton("-", commandPanel, () => ChangeBoost(-1), new Color(0.16f, 0.18f, 0.22f), Color.white);
            SetRect(boostDownButton.GetComponent<RectTransform>(), new Vector2(24, 24), new Vector2(94, 88), new Vector2(0, 0), new Vector2(0, 0));

            boostText = CreateText("BOOST 0", commandPanel, 24, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.42f));
            SetRect(boostText.rectTransform, new Vector2(104, 24), new Vector2(244, 88), new Vector2(0, 0), new Vector2(0, 0));

            boostUpButton = CreateButton("+", commandPanel, () => ChangeBoost(1), new Color(0.16f, 0.18f, 0.22f), Color.white);
            SetRect(boostUpButton.GetComponent<RectTransform>(), new Vector2(254, 24), new Vector2(324, 88), new Vector2(0, 0), new Vector2(0, 0));

            basicAttackButton = CreateButton("Basic Attack", commandPanel, ExecuteBasicAttack, new Color(0.15f, 0.30f, 0.44f), Color.white);
            SetRect(basicAttackButton.GetComponent<RectTransform>(), new Vector2(350, 128), new Vector2(570, 208), new Vector2(0, 0), new Vector2(0, 0));

            tacticalSkillButton = CreateButton("Tactical Skill", commandPanel, ExecuteTacticalSkill, new Color(0.28f, 0.22f, 0.46f), Color.white);
            SetRect(tacticalSkillButton.GetComponent<RectTransform>(), new Vector2(590, 128), new Vector2(840, 208), new Vector2(0, 0), new Vector2(0, 0));

            guardButton = CreateButton("Guard", commandPanel, () => ExecuteGuardOrWait(true), new Color(0.18f, 0.32f, 0.24f), Color.white);
            SetRect(guardButton.GetComponent<RectTransform>(), new Vector2(350, 24), new Vector2(520, 104), new Vector2(0, 0), new Vector2(0, 0));

            waitButton = CreateButton("Wait", commandPanel, () => ExecuteGuardOrWait(false), new Color(0.24f, 0.25f, 0.30f), Color.white);
            SetRect(waitButton.GetComponent<RectTransform>(), new Vector2(540, 24), new Vector2(710, 104), new Vector2(0, 0), new Vector2(0, 0));

            ultimateButton = CreateButton("Ultimate Override", commandPanel, ExecuteUltimateOverride, new Color(0.48f, 0.18f, 0.12f), Color.white);
            SetRect(ultimateButton.GetComponent<RectTransform>(), new Vector2(730, 24), new Vector2(-24, 104), new Vector2(0, 0), new Vector2(1, 0));
        }

        private UnitSlotView CreateUnitSlot(string label, RectTransform parent, bool ally, int index)
        {
            var panel = CreatePanel(label, parent, StretchNone, ally ? new Color(0.10f, 0.17f, 0.21f, 0.96f) : new Color(0.21f, 0.11f, 0.13f, 0.96f));
            var width = ally ? 300f : 190f;
            var height = 155f;
            if (ally)
            {
                SetRect(panel, new Vector2(44 + index * 340f, -600f), new Vector2(44 + index * 340f + width, -600f + height), new Vector2(0, 1), new Vector2(0, 1));
            }
            else
            {
                var x = 34 + index * 208f;
                SetRect(panel, new Vector2(x, -360f), new Vector2(x + width, -360f + height), new Vector2(0, 1), new Vector2(0, 1));
            }

            var markerObject = new GameObject("Square Unit Image");
            var markerRect = markerObject.AddComponent<RectTransform>();
            markerRect.SetParent(panel, false);
            SetRect(markerRect, new Vector2(14, -116), new Vector2(104, -26), new Vector2(0, 1), new Vector2(0, 1));
            var markerImage = markerObject.AddComponent<Image>();
            markerImage.color = ally ? new Color(0.28f, 0.68f, 0.92f) : new Color(0.88f, 0.32f, 0.30f);

            var hpBarBack = CreatePanel("HP Bar Back", panel, StretchNone, new Color(0.05f, 0.055f, 0.065f, 1f));
            SetRect(hpBarBack, new Vector2(116, 16), new Vector2(-12, 28), new Vector2(0, 0), new Vector2(1, 0));
            var hpBarFill = CreatePanel("HP Bar Fill", hpBarBack, StretchNone, new Color(0.25f, 0.82f, 0.44f, 1f));
            SetRect(hpBarFill, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 1));

            var text = CreateText("", panel, 20, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
            SetRect(text.rectTransform, new Vector2(116, 34), new Vector2(-10, -10), new Vector2(0, 0), new Vector2(1, 1));
            return new UnitSlotView(panel, markerImage, hpBarFill, text, ally);
        }

        private void RefreshBattleUi()
        {
            for (var i = 0; i < allySlots.Count; i++)
            {
                var unit = i < simulator.Allies.Count ? simulator.Allies[i] : null;
                allySlots[i].Bind(unit, unit == simulator.LastActor, unit == simulator.LastTarget);
            }

            for (var i = 0; i < enemySlots.Count; i++)
            {
                var unit = i < simulator.Enemies.Count ? simulator.Enemies[i] : null;
                enemySlots[i].Bind(unit, unit == simulator.LastActor, unit == simulator.LastTarget);
            }

            var activeBp = simulator.ActiveCommandUnit != null
                ? $"{simulator.ActiveCommandUnit.CurrentBp}/{simulator.ActiveCommandUnit.MaxBp}"
                : "-";
            statusText.text = $"Tick {simulator.GlobalTick:0.0}   Active BP {activeBp}   QTE Lock {simulator.ForceOverheatTickRemaining}";
            turnBannerText.text = simulator.HasPendingQte ? "QTE WINDOW - FORCE CHAIN" : simulator.LastTurnLabel;
            turnBannerText.color = ResolveTurnColor();
            actionDirectionText.text = ResolveDirectionText();
            actionSummaryText.text = simulator.HasPendingQte ? $"Tap Force Chain against {simulator.PendingQteTarget.DisplayName}" : simulator.LastActionSummary;
            timelineText.text = "Timeline\n" + simulator.TimelineText();
            logText.text = string.Join("\n", simulator.Log);
            autoButton.GetComponentInChildren<Text>().text = autoBattle ? "AUTO ON" : "AUTO OFF";
            stepButton.interactable = !autoBattle && !simulator.HasPendingQte && !simulator.BattleEnded && !simulator.IsWaitingForCommand;
            RefreshCommandPanel();

            qteButton.gameObject.SetActive(simulator.HasPendingQte);
            if (simulator.HasPendingQte)
            {
                qteText.text = $"FORCE CHAIN\n{simulator.PendingQteTimeRemaining:0.0}s";
            }

            if (simulator.BattleEnded)
            {
                statusText.text += simulator.Enemies.Any(unit => unit.IsAlive) ? "   Defeat" : "   Victory";
            }
        }

        private void ToggleAutoBattle()
        {
            autoBattle = !autoBattle;
            autoStepTimer = 0f;
            RefreshBattleUi();
        }

        private void StepTurn()
        {
            if (autoBattle || simulator.HasPendingQte || simulator.BattleEnded || simulator.IsWaitingForCommand)
            {
                return;
            }

            simulator.StepAutoAction();
            RefreshBattleUi();
        }

        private void RefreshCommandPanel()
        {
            var waiting = simulator.IsWaitingForCommand;
            commandPanel.gameObject.SetActive(waiting);
            if (!waiting)
            {
                selectedBoostLevel = 0;
                return;
            }

            var unit = simulator.ActiveCommandUnit;
            selectedBoostLevel = Mathf.Clamp(selectedBoostLevel, 0, Mathf.Min(3, unit.CurrentBp));
            commandTitleText.text = $"{unit.DisplayName} Command  BP {unit.CurrentBp}/{unit.MaxBp}";
            boostText.text = $"BOOST {selectedBoostLevel}";
            boostDownButton.interactable = selectedBoostLevel > 0;
            boostUpButton.interactable = selectedBoostLevel < 3 && selectedBoostLevel < unit.CurrentBp;
            basicAttackButton.interactable = true;
            tacticalSkillButton.interactable = true;
            guardButton.interactable = true;
            waitButton.interactable = true;
            ultimateButton.interactable = true;
        }

        private void ChangeBoost(int delta)
        {
            selectedBoostLevel = Mathf.Clamp(selectedBoostLevel + delta, 0, 3);
            RefreshCommandPanel();
        }

        private void ExecuteBasicAttack()
        {
            simulator.ExecuteBasicAttack(selectedBoostLevel);
            selectedBoostLevel = 0;
            RefreshBattleUi();
        }

        private void ExecuteTacticalSkill()
        {
            simulator.ExecuteTacticalSkill(selectedBoostLevel);
            selectedBoostLevel = 0;
            RefreshBattleUi();
        }

        private void ExecuteGuardOrWait(bool isGuardMode)
        {
            simulator.ExecuteGuardOrWait(isGuardMode);
            selectedBoostLevel = 0;
            RefreshBattleUi();
        }

        private void ExecuteUltimateOverride()
        {
            simulator.ExecuteUltimateOverride();
            selectedBoostLevel = 0;
            RefreshBattleUi();
        }

        private Color ResolveTurnColor()
        {
            if (simulator.HasPendingQte)
            {
                return new Color(1f, 0.78f, 0.25f);
            }

            if (simulator.LastActor == null)
            {
                return Color.white;
            }

            return simulator.LastActor.Team == BattleTeam.Ally
                ? new Color(0.48f, 0.82f, 1f)
                : new Color(1f, 0.45f, 0.40f);
        }

        private string ResolveDirectionText()
        {
            if (simulator.HasPendingQte)
            {
                return "ALLY QTE -> ENEMY";
            }

            if (simulator.IsWaitingForCommand)
            {
                return "ALLY INPUT WAIT";
            }

            if (simulator.LastActor == null)
            {
                return "TIMELINE READY";
            }

            return simulator.LastActor.Team == BattleTeam.Ally
                ? "ALLY ATTACK -> ENEMY"
                : "ENEMY ATTACK -> ALLY";
        }

        private static RectTransform CreatePanel(string name, RectTransform parent, Action<RectTransform> rectSetup, Color color)
        {
            var panelObject = new GameObject(name);
            var rect = panelObject.AddComponent<RectTransform>();
            if (parent != null)
            {
                rect.SetParent(parent, false);
            }

            rectSetup(rect);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform WrapWithPanel(RectTransform child, string name, Color color)
        {
            var parent = child.parent as RectTransform;
            var panel = CreatePanel(name, parent, StretchNone, color);
            child.SetParent(panel, false);
            return panel;
        }

        private static Text CreateText(string value, RectTransform parent, int size, FontStyle style, TextAnchor anchor, Color color)
        {
            var textObject = new GameObject("Text");
            var rect = textObject.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = ResolveFont();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(string label, RectTransform parent, Action callback, Color background, Color foreground)
        {
            var buttonObject = new GameObject(label);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = background;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => callback());

            var text = CreateText(label, rect, 28, FontStyle.Bold, TextAnchor.MiddleCenter, foreground);
            SetRect(text.rectTransform, new Vector2(12, 8), new Vector2(-12, -8), new Vector2(0, 0), new Vector2(1, 1));
            return button;
        }

        private static void EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.05f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();

            var inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem")
                ?? Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.ForUI");
            if (inputSystemModuleType != null)
            {
                var module = eventSystemObject.AddComponent(inputSystemModuleType);
                var assignDefaultActions = inputSystemModuleType.GetMethod("AssignDefaultActions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                assignDefaultActions?.Invoke(module, null);
            }
            else
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
        }

        private static Font ResolveFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            return Font.CreateDynamicFontFromOSFont("Arial", 24);
        }

        private static void StretchFull(RectTransform rect)
        {
            SetRect(rect, Vector2.zero, Vector2.zero, new Vector2(0, 0), new Vector2(1, 1));
        }

        private static void StretchNone(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private sealed class UnitSlotView
        {
            private readonly RectTransform root;
            private readonly Image markerImage;
            private readonly RectTransform hpBarFill;
            private readonly Text text;
            private readonly bool ally;

            public UnitSlotView(RectTransform root, Image markerImage, RectTransform hpBarFill, Text text, bool ally)
            {
                this.root = root;
                this.markerImage = markerImage;
                this.hpBarFill = hpBarFill;
                this.text = text;
                this.ally = ally;
            }

            public void Bind(BattleUnit unit)
            {
                Bind(unit, false, false);
            }

            public void Bind(BattleUnit unit, bool isActor, bool isTarget)
            {
                if (unit == null)
                {
                    root.gameObject.SetActive(false);
                    return;
                }

                root.gameObject.SetActive(true);
                var panelImage = root.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = ResolvePanelColor(unit, ally, isActor, isTarget);
                }

                var hpRatio = unit.MaxHp <= 0 ? 0f : Mathf.Clamp01((float)unit.Hp / unit.MaxHp);
                hpBarFill.anchorMax = new Vector2(hpRatio, 1f);
                hpBarFill.offsetMax = Vector2.zero;

                markerImage.color = ResolveMarkerColor(unit, ally);
                var shield = unit.MaxShield > 0 ? $"  SP {unit.Shield}/{unit.MaxShield}" : string.Empty;
                var bp = ally ? $"\nBP {unit.CurrentBp}/{unit.MaxBp}" : string.Empty;
                var cc = unit.IsStunned ? "\nStun" : unit.IsWeakened ? "\nWeaken" : unit.IsGuarded ? "\nGuard" : string.Empty;
                text.text = $"{unit.DisplayName}\nHP {unit.Hp}/{unit.MaxHp}{shield}\nSPD {unit.Spd}{bp}{cc}";
                text.color = unit.IsAlive ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }

            private static Color ResolvePanelColor(BattleUnit unit, bool ally, bool isActor, bool isTarget)
            {
                if (isActor)
                {
                    return ally ? new Color(0.13f, 0.35f, 0.45f, 1f) : new Color(0.42f, 0.18f, 0.16f, 1f);
                }

                if (isTarget)
                {
                    return new Color(0.46f, 0.34f, 0.12f, 1f);
                }

                return ally ? new Color(0.10f, 0.17f, 0.21f, 0.96f) : new Color(0.21f, 0.11f, 0.13f, 0.96f);
            }

            private static Color ResolveMarkerColor(BattleUnit unit, bool ally)
            {
                if (!unit.IsAlive)
                {
                    return new Color(0.28f, 0.28f, 0.30f);
                }

                if (unit.IsBroken)
                {
                    return new Color(1f, 0.85f, 0.25f);
                }

                if (unit.IsStunned)
                {
                    return new Color(0.78f, 0.55f, 1f);
                }

                return ally
                    ? unit.WeaponStyle switch
                    {
                        WeaponStyle.Striker => new Color(0.25f, 0.58f, 0.95f),
                        WeaponStyle.Punisher => new Color(0.22f, 0.78f, 0.62f),
                        WeaponStyle.Resonance => new Color(0.70f, 0.50f, 0.96f),
                        _ => new Color(0.48f, 0.70f, 0.86f)
                    }
                    : new Color(0.90f, 0.30f, 0.26f);
            }
        }
    }
}
