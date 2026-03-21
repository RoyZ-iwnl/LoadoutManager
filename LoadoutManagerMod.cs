using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using GHPC.Player;
using GHPC.Weapons;
using GHPC.Weaponry;
using GHPC.Vehicle;
using GHPC.State;
using GHPC.Effects;

[assembly: MelonInfo(typeof(LoadoutManager.LoadoutManagerMod), "Loadout Manager", "1.2.0", "RoyZ")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace LoadoutManager
{
    public class LoadoutManagerMod : MelonMod
    {
        // MelonPreferences 配置
        public static MelonPreferences_Category cfg;
        public static MelonPreferences_Entry<bool> hideRack0ForAutocannon;
        public static MelonPreferences_Entry<float> uiScale;
        public static MelonPreferences_Entry<int> language;
        public static MelonPreferences_Entry<bool> limitTotalAmmoByOriginalVehicleCount;

        // M6A2-ADATS 兼容性：检测该 Mod 是否存在
        private static bool? _m6a2AdatsDetected = null;
        private static bool IsM6A2AdatsPresent()
        {
            if (_m6a2AdatsDetected == null)
            {
                _m6a2AdatsDetected = false;
                foreach (var melon in MelonMod.RegisteredMelons)
                {
                    if (melon.Info.Name.Contains("M6A2") || melon.Info.Name.Contains("ADATS"))
                    {
                        _m6a2AdatsDetected = true;
                        MelonLogger.Msg($"{melon.Info.Name} DETECTED,DELAY MENU DISPLAY");
                        break;
                    }
                }
            }
            return _m6a2AdatsDetected ?? false;
        }

        // 待延迟处理的载具队列（用于 M6A2-ADATS 兼容）
        private Queue<object> pendingVehicles = new Queue<object>();
        private float pendingVehicleTimer = 0f;
        private const float M6A2_ADATS_DELAY_SECONDS = 0.5f; // 延迟0.5秒等待 M6A2-ADATS 完成修改

        [System.Diagnostics.Conditional("DEBUG")]
        private static void Log(string msg)
        {
            MelonLogger.Msg($"[DEBUG] {msg}");
        }

        // 已配置的载具集合
        private HashSet<object> configuredVehicles = new HashSet<object>();

        // UI状态
        private bool showUI = false;
        private Rect windowRect = new Rect(Screen.width / 2 - 400, Screen.height / 2 - 300, 800, 600);
        private Vector2 scrollPosition = Vector2.zero;
        private bool uiPreviousPaused = false;
        private bool uiPreviousCursorVisible = false;
        private CursorLockMode uiPreviousCursorLockMode = CursorLockMode.Locked;
        private bool uiShouldRestorePreviousCursorState = false;
        private bool uiPreviousSuspendCameraInputs = false;
        private bool uiPreviousSuspendGunnerInputs = false;
        private bool uiPreviousSuspendInterfaceInputs = false;
        private bool uiControlStateCaptured = false;
        private bool uiPreviousPlanningOrBriefingUiActive = false;
        private GUIStyle opaqueWindowStyle;
        private GUIStyle titleLabelStyle;

        // 当前配置的载具数据
        private object currentUnit = null;
        private List<WeaponState> weaponStates = new List<WeaponState>();

        // 反射缓存
        private static MethodInfo removeVisualMethod;
        private static MethodInfo placeAmmoVisualMethod;
        private static MethodInfo rackAddVisibleClipMethod;
        private static MethodInfo rackAddInvisibleClipMethod;
        private static MethodInfo rackAddClipToAnySlotMethod;
        private static MethodInfo spawnCurrentLoadoutMethod;
        private static FieldInfo rackFlammablesField;
        private static MethodInfo flammablesRefreshExplosivesStatusMethod;
        private static MethodInfo flammablesRefreshUndetonatedExplosivesStatusMethod;
        private static MethodInfo registerBallisticsMethod;
        private static FieldInfo totalAmmoCountField;
        private static FieldInfo totalAmmoTypesField;
        private static PropertyInfo storedClipsProperty;
        private static FieldInfo slotIndicesByAmmoTypeField;
        private static FieldInfo visualSlotsField;
        private static PropertyInfo feedLoadedClipProperty;
        private static FieldInfo storedClipsBackingField;
        private static FieldInfo ammoTypeInBreechBackingField;
        private static FieldInfo loadedClipTypeBackingField;
        private static FieldInfo queuedClipTypeBackingField;
        private static FieldInfo queuedClipTypeLockedInField;
        private static FieldInfo feedClipMainField;
        private static FieldInfo feedClipAuxField;
        private static MethodInfo feedStartMethod;
        private static MethodInfo feedSetNextClipTypeMethod;
        private static PropertyInfo feedReloadingProperty;
        private static PropertyInfo feedForcePauseReloadProperty;
        private static FieldInfo feedAuxFeedModeField;
        private static MethodInfo rackRegenerateSlotIndicesMethod;
        private static MethodInfo refreshSnapshotMethod;
        private static MethodInfo timeControllerPauseGameMethod;
        private static MethodInfo timeControllerUnpauseGameMethod;
        private static PropertyInfo timeControllerPausedProperty;
        private static MethodInfo playerInputOnMenuTakingFocusMethod;
        private static MethodInfo playerInputOnMenuLosingFocusMethod;
        private static MethodInfo cameraManagerForceDetachCameraMethod;
        private static FieldInfo playerInputSuspendCameraInputsBackingField;
        private static FieldInfo playerInputSuspendGunnerInputsBackingField;
        private static FieldInfo playerInputSuspendInterfaceInputsBackingField;
        private static PropertyInfo weaponCurrentAmmoTypeProperty;
        private static PropertyInfo fcsCurrentAmmoTypeProperty;
        private static MethodInfo fcsAmmoTypeChangedMethod;
        private static MethodInfo weaponNewRoundInBreechMethod;
        private static FieldInfo mainGunAmmoField;
        private static FieldInfo mainGunReadyRackField;
        private static FieldInfo mainGunAvailableAmmoField;
        private static FieldInfo mainGunAmmoCountsField;
        private static FieldInfo mainGunAmmoIndexInBreechField;
        private static FieldInfo mainGunCurrentAmmoIndexField;
        private static FieldInfo mainGunNextAmmoIndexField;
        private static PropertyInfo mainGunUseAmmoRacksProperty;
        private static MethodInfo mainGunUpdateAmmoCountsMethod;
        private static MethodInfo mainGunSelectAmmoTypeMethod;
        private static MethodInfo mainGunForceRoundToBreechMethod;
        private static Type planningPhaseUiType;
        private static Type missionBriefingPanelUiType;

        // 游戏是否准备好
        private bool gameReady = false;

        public override void OnInitializeMelon()
        {
            // 初始化配置
            cfg = MelonPreferences.CreateCategory("LoadoutManager");

            hideRack0ForAutocannon = cfg.CreateEntry("HideRack0ForAutocannon", true);
            hideRack0ForAutocannon.Comment = "Hide Rack0 for autocannon vehicles. Rack0 is the ready-to-fire ammo on feed, default 0 in game. - 隐藏机炮车的Rack0。Rack0即为待发弹药架上的弹药，原版游戏对于机炮车一般默认为0";

            uiScale = cfg.CreateEntry("UIScale", 1.0f);
            uiScale.Comment = "UI Scale (0.5-2.0, default 1.0) - UI缩放";

            language = cfg.CreateEntry("Language", 0);
            language.Comment = "Language (0=English, 1=Chinese) - 语言";

            limitTotalAmmoByOriginalVehicleCount = cfg.CreateEntry("LimitTotalAmmoByOriginalVehicleCount", false);
            limitTotalAmmoByOriginalVehicleCount.Comment = "Limit editable rack ammo by the vehicle's current ammo total when opening UI (free distribution under fixed total). - 限制可编辑弹药总数为打开UI时该载具当前弹药总数（在固定总量下自由分配）";

            // 设置语言
            LocalizationManager.SetLanguage(language.Value);

            // 初始化反射
            removeVisualMethod = typeof(AmmoRack).GetMethod("RemoveAmmoVisualFromSlot", BindingFlags.Instance | BindingFlags.NonPublic);
            placeAmmoVisualMethod = typeof(AmmoRack).GetMethod("PlaceAmmoVisualInSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            rackAddVisibleClipMethod = typeof(AmmoRack).GetMethod("AddVisibleClip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            rackAddInvisibleClipMethod = typeof(AmmoRack).GetMethod("AddInvisibleClip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            rackAddClipToAnySlotMethod = typeof(AmmoRack).GetMethod("AddClipToAnySlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            spawnCurrentLoadoutMethod = typeof(GHPC.Weapons.LoadoutManager).GetMethod("SpawnCurrentLoadout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            rackFlammablesField = typeof(AmmoRack).GetField("Flammables", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            flammablesRefreshExplosivesStatusMethod = typeof(FlammablesCluster).GetMethod("RefreshExplosivesStatus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            flammablesRefreshUndetonatedExplosivesStatusMethod = typeof(FlammablesCluster).GetMethod("RefreshUndetonatedExplosivesStatus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            registerBallisticsMethod = typeof(GHPC.Weapons.LoadoutManager).GetMethod("RegisterAllBallistics", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            totalAmmoCountField = typeof(GHPC.Weapons.LoadoutManager).GetField("_totalAmmoCount", BindingFlags.Instance | BindingFlags.NonPublic);
            totalAmmoTypesField = typeof(GHPC.Weapons.LoadoutManager).GetField("_totalAmmoTypes", BindingFlags.Instance | BindingFlags.NonPublic);
            storedClipsProperty = typeof(AmmoRack).GetProperty("StoredClips", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            slotIndicesByAmmoTypeField = typeof(AmmoRack).GetField("SlotIndicesByAmmoType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            visualSlotsField = typeof(AmmoRack).GetField("VisualSlots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            feedLoadedClipProperty = typeof(AmmoFeed).GetProperty("LoadedClip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            storedClipsBackingField = typeof(AmmoRack).GetField("<StoredClips>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            ammoTypeInBreechBackingField = typeof(AmmoFeed).GetField("<AmmoTypeInBreech>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            loadedClipTypeBackingField = typeof(AmmoFeed).GetField("<LoadedClipType>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            queuedClipTypeBackingField = typeof(AmmoFeed).GetField("<QueuedClipType>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            queuedClipTypeLockedInField = typeof(AmmoFeed).GetField("_queuedClipTypeLockedIn", BindingFlags.Instance | BindingFlags.NonPublic);
            feedClipMainField = typeof(AmmoFeed).GetField("_feedClipMain", BindingFlags.Instance | BindingFlags.NonPublic);
            feedClipAuxField = typeof(AmmoFeed).GetField("_feedClipAux", BindingFlags.Instance | BindingFlags.NonPublic);
            feedStartMethod = typeof(AmmoFeed).GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            feedSetNextClipTypeMethod = typeof(AmmoFeed).GetMethod("SetNextClipType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            feedReloadingProperty = typeof(AmmoFeed).GetProperty("Reloading", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            feedForcePauseReloadProperty = typeof(AmmoFeed).GetProperty("ForcePauseReload", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            feedAuxFeedModeField = typeof(AmmoFeed).GetField("_auxFeedMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            rackRegenerateSlotIndicesMethod = typeof(AmmoRack).GetMethod("RegenerateSlotIndices", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            refreshSnapshotMethod = typeof(GHPC.Weapons.LoadoutManager).GetMethod("RefreshSnapshot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            timeControllerPauseGameMethod = typeof(TimeController).GetMethod("PauseGame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            timeControllerUnpauseGameMethod = typeof(TimeController).GetMethod("UnpauseGame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            timeControllerPausedProperty = typeof(TimeController).GetProperty("Paused", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            playerInputOnMenuTakingFocusMethod = typeof(PlayerInput).GetMethod("OnMenuTakingFocus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            playerInputOnMenuLosingFocusMethod = typeof(PlayerInput).GetMethod("OnMenuLosingFocus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            cameraManagerForceDetachCameraMethod = typeof(GHPC.Camera.CameraManager).GetMethod("ForceDetachCamera", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            playerInputSuspendCameraInputsBackingField = typeof(PlayerInput).GetField("<SuspendCameraInputs>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            playerInputSuspendGunnerInputsBackingField = typeof(PlayerInput).GetField("<SuspendGunnerInputs>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            playerInputSuspendInterfaceInputsBackingField = typeof(PlayerInput).GetField("<SuspendInterfaceInputs>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            weaponCurrentAmmoTypeProperty = typeof(WeaponSystem).GetProperty("CurrentAmmoType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            fcsCurrentAmmoTypeProperty = typeof(FireControlSystem).GetProperty("CurrentAmmoType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            fcsAmmoTypeChangedMethod = typeof(FireControlSystem).GetMethod("ammoTypeChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            weaponNewRoundInBreechMethod = typeof(WeaponSystem).GetMethod("newRoundInBreech", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunAmmoField = typeof(MainGun).GetField("Ammo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunReadyRackField = typeof(MainGun).GetField("_readyRack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunAvailableAmmoField = typeof(MainGun).GetField("AvailableAmmo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunAmmoCountsField = typeof(MainGun).GetField("AmmoCounts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunAmmoIndexInBreechField = typeof(MainGun).GetField("_ammoIndexInBreech", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunCurrentAmmoIndexField = typeof(MainGun).GetField("_currentAmmoIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunNextAmmoIndexField = typeof(MainGun).GetField("_nextAmmoIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunUseAmmoRacksProperty = typeof(MainGun).GetProperty("UseAmmoRacks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunUpdateAmmoCountsMethod = typeof(MainGun).GetMethod("UpdateAmmoCounts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunSelectAmmoTypeMethod = typeof(MainGun).GetMethod("SelectAmmoType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainGunForceRoundToBreechMethod = typeof(MainGun).GetMethod("ForceRoundToBreech", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Log($"反射缓存: RemoveAmmoVisualFromSlot={(removeVisualMethod != null)}, PlaceAmmoVisualInSlot={(placeAmmoVisualMethod != null)}, RackAddVisibleClip={(rackAddVisibleClipMethod != null)}, RackAddInvisibleClip={(rackAddInvisibleClipMethod != null)}, RackAddAnyClip={(rackAddClipToAnySlotMethod != null)}, RegisterAllBallistics={(registerBallisticsMethod != null)}, RefreshSnapshot={(refreshSnapshotMethod != null)}, _totalAmmoCount={(totalAmmoCountField != null)}, _totalAmmoTypes={(totalAmmoTypesField != null)}, StoredClips={(storedClipsProperty != null)}, StoredClipsBacking={(storedClipsBackingField != null)}, SlotIndices={(slotIndicesByAmmoTypeField != null)}, VisualSlots={(visualSlotsField != null)}, FeedLoadedClip={(feedLoadedClipProperty != null)}, AmmoTypeInBreechBacking={(ammoTypeInBreechBackingField != null)}, LoadedClipTypeBacking={(loadedClipTypeBackingField != null)}, QueuedClipTypeBacking={(queuedClipTypeBackingField != null)}, FeedClipMain={(feedClipMainField != null)}, FeedClipAux={(feedClipAuxField != null)}, FeedAuxMode={(feedAuxFeedModeField != null)}, FeedStart={(feedStartMethod != null)}, FeedSetNextClip={(feedSetNextClipTypeMethod != null)}, FeedReloading={(feedReloadingProperty != null)}, FeedPause={(feedForcePauseReloadProperty != null)}, TimePause={(timeControllerPauseGameMethod != null)}, TimeUnpause={(timeControllerUnpauseGameMethod != null)}, TimePausedProp={(timeControllerPausedProperty != null)}, MenuFocusIn={(playerInputOnMenuTakingFocusMethod != null)}, MenuFocusOut={(playerInputOnMenuLosingFocusMethod != null)}, CameraDetach={(cameraManagerForceDetachCameraMethod != null)}, SuspendCamera={(playerInputSuspendCameraInputsBackingField != null)}, SuspendGunner={(playerInputSuspendGunnerInputsBackingField != null)}, SuspendInterface={(playerInputSuspendInterfaceInputsBackingField != null)}, WeaponCurrentAmmo={(weaponCurrentAmmoTypeProperty != null)}, FcsCurrentAmmo={(fcsCurrentAmmoTypeProperty != null)}, FcsAmmoTypeChanged={(fcsAmmoTypeChangedMethod != null)}, WeaponNewRoundInBreech={(weaponNewRoundInBreechMethod != null)}, RackRegenerate={(rackRegenerateSlotIndicesMethod != null)}, MainGunAmmo={(mainGunAmmoField != null)}, MainGunReadyRack={(mainGunReadyRackField != null)}, MainGunCounts={(mainGunAmmoCountsField != null)}, MainGunSelect={(mainGunSelectAmmoTypeMethod != null)}, MainGunBreech={(mainGunForceRoundToBreechMethod != null)}");
            Log(LocalizationManager.Get("log_mod_initialized"));
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            gameReady = false;
            // 清空待处理的载具队列
            pendingVehicles.Clear();
            pendingVehicleTimer = 0f;
            StateController.RunOrDefer(GameState.GameReady, new GameStateEventHandler(OnGameReady), GameStatePriority.Lowest);
        }

        private IEnumerator OnGameReady(GameState _)
        {
            gameReady = true;
            Log(LocalizationManager.Get("log_game_ready"));
            yield break;
        }

        public override void OnUpdate()
        {
            if (!gameReady) return;

            // 处理待延迟的载具队列（M6A2-ADATS 兼容）
            ProcessPendingVehicles();

            // 检测玩家当前载具
            var playerInput = PlayerInput.Instance;
            if (playerInput == null) return;

            var playerUnit = playerInput.CurrentPlayerUnit;
            if (playerUnit == null) return;

            // 如果切换到新载具，关闭旧UI并显示新UI
            if (!configuredVehicles.Contains(playerUnit))
            {
                Log(LocalizationManager.Get("log_new_vehicle", playerUnit));
                configuredVehicles.Add(playerUnit);

                // M6A2-ADATS 兼容性：如果检测到该 Mod，延迟读取弹药状态
                if (IsM6A2AdatsPresent())
                {
                    Log($"检测到新载具，加入延迟处理队列: {playerUnit}");
                    pendingVehicles.Enqueue(playerUnit);
                    pendingVehicleTimer = Time.time;
                }
                else
                {
                    CaptureVehicleState(playerUnit);

                    if (weaponStates.Count > 0)
                    {
                        Log(LocalizationManager.Get("log_weapons_found", weaponStates.Count));
                        OpenUI(playerUnit);
                    }
                    else
                    {
                        Log(LocalizationManager.Get("log_no_weapons"));
                    }
                }
            }
            else if (showUI && !Equals(currentUnit, playerUnit))
            {
                // 切换到其他载具，关闭当前UI
                CloseUI();
            }
        }

        private void ProcessPendingVehicles()
        {
            if (pendingVehicles.Count == 0) return;

            // 等待延迟时间
            if (Time.time - pendingVehicleTimer < M6A2_ADATS_DELAY_SECONDS) return;

            // 获取当前玩家载具
            var playerInput = PlayerInput.Instance;
            var currentUnit = playerInput?.CurrentPlayerUnit;

            while (pendingVehicles.Count > 0)
            {
                var pendingUnit = pendingVehicles.Dequeue();

                // 如果玩家已经切换到其他载具，跳过此载具
                if (!Equals(pendingUnit, currentUnit))
                {
                    Log($"玩家已切换载具，跳过延迟处理: {pendingUnit}");
                    continue;
                }

                Log($"延迟处理载具: {pendingUnit}");

                CaptureVehicleState(pendingUnit);

                if (weaponStates.Count > 0)
                {
                    Log(LocalizationManager.Get("log_weapons_found", weaponStates.Count));
                    OpenUI(pendingUnit);
                }
                else
                {
                    Log(LocalizationManager.Get("log_no_weapons"));
                }
            }
        }

        public override void OnGUI()
        {
            if (!showUI) return;

            float scale = uiScale.Value;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);

            // 应用UI缩放
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));

            // 计算缩放后的窗口尺寸
            float scaledWidth = 800;
            float scaledHeight = 600;
            float scaledScreenWidth = screenSize.x / scale;
            float scaledScreenHeight = screenSize.y / scale;

            windowRect = new Rect(
                (scaledScreenWidth - scaledWidth) / 2,
                (scaledScreenHeight - scaledHeight) / 2,
                scaledWidth,
                scaledHeight
            );

            EnsureGuiStyles();
            windowRect = GUI.Window(12345, windowRect, DrawWindow, LocalizationManager.Get("window_title"), opaqueWindowStyle);
        }

        private void DrawWindow(int windowId)
        {
            DrawOpaqueWindowBackground();
            GUILayout.BeginVertical();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(windowRect.height - 80));

            foreach (var weapon in weaponStates)
            {
                GUILayout.Label($"{LocalizationManager.Get("weapon")}: {weapon.weaponName}", titleLabelStyle, GUILayout.Height(25));

                // 按rack组织UI
                int rackCount = weapon.ammoTypes.Count > 0 ? weapon.ammoTypes[0].rackCounts.Length : 0;
                if (IsOriginalTotalAmmoLimitEnabled())
                {
                    int currentTotal = GetWeaponRackGrandTotal(weapon);
                    GUILayout.Label($"Total Ammo Budget: {currentTotal}/{weapon.originalRackTotalBudget}", GUILayout.Height(20));
                }

                // 机炮/双弹链的 Rack 0 通常是供弹接口架，不是玩家真正要编辑的备弹架；先默认隐藏，后续可在这里加开关。
                int firstVisibleRackIndex = GetVisibleRackStartIndex(weapon);
                if (weapon.usesAutocannonFeedMode && firstVisibleRackIndex > 0)
                {
                    GUILayout.Label($"{LocalizationManager.Get("rack_0_hidden")}", GUILayout.Height(20));
                    GUILayout.Label($"({LocalizationManager.Get("rack_0_hidden_comment")})", GUILayout.Height(20));
                }

                for (int r = firstVisibleRackIndex; r < rackCount; r++)
                {
                    int rackCapacity = weapon.ammoTypes[0].rackCapacities[r];
                    GUILayout.Label($"{LocalizationManager.Get("ammo_rack")} {r} ({LocalizationManager.Get("capacity")}: {rackCapacity}):", GUILayout.Height(20));

                    for (int ammoIndex = 0; ammoIndex < weapon.ammoTypes.Count; ammoIndex++)
                    {
                        var ammo = weapon.ammoTypes[ammoIndex];
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"  {ammo.typeName}:", GUILayout.Width(150));

                        int newValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(ammo.rackCounts[r], 0, rackCapacity, GUILayout.Width(220)));
                        if (newValue != ammo.rackCounts[r])
                        {
                            RebalanceRackCounts(weapon, r, ammoIndex, newValue);
                        }

                        if (GUILayout.Button(LocalizationManager.Get("fill"), GUILayout.Width(50)))
                        {
                            FillRackExclusive(weapon, r, ammoIndex);
                        }

                        if (GUILayout.Button(LocalizationManager.Get("clear"), GUILayout.Width(50)))
                        {
                            weapon.ammoTypes[ammoIndex].rackCounts[r] = 0;
                        }

                        GUILayout.Label($"{ammo.rackCounts[r]}", GUILayout.Width(40));
                        GUILayout.EndHorizontal();
                    }

                    int currentRackTotal = 0;
                    foreach (var ammo in weapon.ammoTypes)
                    {
                        currentRackTotal += ammo.rackCounts[r];
                    }
                    GUILayout.Label($"  {LocalizationManager.Get("rack_total")}: {currentRackTotal}/{rackCapacity}");
                    GUILayout.Space(10);
                }

                if (weapon.usesAutocannonFeedMode)
                {
                    GUILayout.Label($"{LocalizationManager.Get("loaded_ammo")}:", GUILayout.Height(20));
                    GUILayout.Label($"  {LocalizationManager.Get("fixed_total_loaded")}: {weapon.totalLoadedRounds}", GUILayout.Height(20));
                    foreach (var ammo in weapon.ammoTypes)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"  {ammo.typeName}:", GUILayout.Width(150));
                        int newLoadedCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(ammo.currentLoadedCount, 0, weapon.totalLoadedRounds, GUILayout.Width(220)));
                        if (newLoadedCount != ammo.currentLoadedCount)
                        {
                            RebalanceLoadedAmmoCounts(weapon, weapon.ammoTypes.IndexOf(ammo), newLoadedCount);
                        }
                        GUILayout.Label($"{ammo.currentLoadedCount}", GUILayout.Width(50));
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.Label($"{LocalizationManager.Get("current_ammo")}:", GUILayout.Height(20));
                foreach (var ammo in weapon.ammoTypes)
                {
                    int total = weapon.usesAutocannonFeedMode ? GetVisibleRackAmmoTotal(weapon, ammo) : ammo.rackCounts.Sum();
                    if (weapon.usesAutocannonFeedMode)
                    {
                        int roundsPerBox = ammo.ammoClipType != null ? ammo.ammoClipType.Capacity : 0;
                        GUILayout.Label($"  {ammo.typeName}: {LocalizationManager.Get("loaded")}={ammo.currentLoadedCount}, {LocalizationManager.Get("ammo_box")}={total} ({roundsPerBox} {LocalizationManager.Get("rounds_per_box")})");
                    }
                    else
                    {
                        GUILayout.Label($"  {ammo.typeName}: {total}");
                    }
                }

                // 膛内弹药选择
                GUILayout.Space(10);
                GUILayout.Label($"{LocalizationManager.Get("chambered_ammo")}:", GUILayout.Height(20));

                if (weapon.isReloading)
                {
                    GUILayout.Label($"  {LocalizationManager.Get("reloading_warning")}", GUILayout.Height(20));
                    GUI.enabled = false;
                }

                GUILayout.BeginHorizontal();
                for (int i = 0; i < weapon.ammoTypes.Count; i++)
                {
                    if (GUILayout.Button(weapon.ammoTypes[i].typeName, GUILayout.Width(120)))
                    {
                        weapon.selectedChamberedIndex = i;
                    }
                }
                GUILayout.EndHorizontal();

                if (weapon.isReloading)
                {
                    GUI.enabled = true;
                }

                GUILayout.Label($"{LocalizationManager.Get("selected")}: {weapon.ammoTypes[weapon.selectedChamberedIndex].typeName}");

                GUILayout.Space(15);
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LocalizationManager.Get("apply"), GUILayout.Height(35)))
            {
                ApplyChanges();
                CloseUI();
            }
            if (GUILayout.Button(LocalizationManager.Get("cancel"), GUILayout.Height(35)))
            {
                CloseUI();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));

            // 允许调整窗口大小
            Rect resizeRect = new Rect(windowRect.width - 20, windowRect.height - 20, 20, 20);
            GUI.Box(resizeRect, "");
        }

        private void CaptureVehicleState(object unit)
        {
            weaponStates.Clear();

            try
            {
                Log(LocalizationManager.Get("log_capture_start"));

                var vic = unit as GHPC.Vehicle.Vehicle;
                if (vic == null)
                {
                    Log(LocalizationManager.Get("log_convert_failed"));
                    return;
                }

                var loadoutManager = vic.GetComponent<GHPC.Weapons.LoadoutManager>();
                if (loadoutManager == null)
                {
                    Log(LocalizationManager.Get("log_no_loadout_manager"));
                    return;
                }

                var weaponsManager = vic.GetComponent<WeaponsManager>();
                if (weaponsManager == null || weaponsManager.Weapons == null || weaponsManager.Weapons.Length == 0)
                {
                    Log(LocalizationManager.Get("log_no_weapons_manager"));
                    return;
                }

                Log(LocalizationManager.Get("log_found_loadout_weapons", weaponsManager.Weapons.Length));

                var weaponState = new WeaponState
                {
                    weapon = weaponsManager.Weapons[0].Weapon,
                    loadoutManager = loadoutManager,
                    weaponName = weaponsManager.Weapons[0].Name
                };

                ReadAmmoState(weaponState);
                if (weaponState.ammoTypes.Count > 0)
                {
                    weaponStates.Add(weaponState);
                    Log(LocalizationManager.Get("log_weapon_added", weaponState.ammoTypes.Count));
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error(LocalizationManager.Get("log_capture_failed", e.Message));
                MelonLogger.Error($"Stack: {e.StackTrace}");
            }
        }

        private void ReadAmmoState(WeaponState weaponState)
        {
            try
            {
                var lm = weaponState.loadoutManager as GHPC.Weapons.LoadoutManager;
                if (lm == null || lm.RackLoadouts == null || lm.RackLoadouts.Length == 0)
                {
                    Log(LocalizationManager.Get("log_loadout_invalid"));
                    return;
                }

                if (lm.TotalAmmoCounts == null || lm.LoadedAmmoList?.AmmoClips == null)
                {
                    Log(LocalizationManager.Get("log_ammo_data_invalid"));
                    return;
                }

                Log(LocalizationManager.Get("log_found_racks_ammo", lm.RackLoadouts.Length, lm.LoadedAmmoList.AmmoClips.Length));
                var weapon = weaponState.weapon as WeaponSystem;
                var feed = weapon?.Feed;
                weaponState.usesAutocannonFeedMode = IsAutocannonFeedWeapon(weapon, lm);
                weaponState.isReloading = feed != null && feed.Reloading;
                LogLoadoutSnapshot("读取前快照", lm, weapon);

                for (int i = 0; i < lm.LoadedAmmoList.AmmoClips.Length && i < lm.TotalAmmoCounts.Length; i++)
                {
                    var clip = lm.LoadedAmmoList.AmmoClips[i];
                    var clipType = lm.GetAmmoClipTypeByIndex(i);
                    var ammoCodex = GetAmmoCodexFromClipCodex(clip);
                    var ammoType = ammoCodex != null ? ammoCodex.AmmoType : null;
                    var ammoState = new AmmoTypeState
                    {
                        ammoClip = clip,
                        ammoClipType = clipType,
                        ammoCodex = ammoCodex,
                        ammoType = ammoType,
                        typeName = FormatAmmoDisplayName(clip.name),
                        originalTotal = lm.TotalAmmoCounts[i],
                        currentLoadedCount = weaponState.usesAutocannonFeedMode ? GetLoadedClipCountByType(feed, clipType) : 0,
                        rackCounts = new int[lm.RackLoadouts.Length],
                        rackCapacities = new int[lm.RackLoadouts.Length]
                    };

                    for (int r = 0; r < lm.RackLoadouts.Length; r++)
                    {
                        var rack = lm.RackLoadouts[r].Rack;
                        ammoState.rackCapacities[r] = rack.ClipCapacity;

                        var storedClips = GetStoredClips(rack);
                        if (storedClips != null)
                        {
                            foreach (var storedClip in storedClips)
                            {
                                if (storedClip != null && clipType != null && DescribeObject(storedClip) == clipType.Name)
                                {
                                    ammoState.rackCounts[r]++;
                                }
                            }
                        }

                        Log($"读取Rack {r} / {ammoState.typeName}: 容量={ammoState.rackCapacities[r]}, 当前={ammoState.rackCounts[r]}, Rack.StoredClips={GetStoredClipCount(rack)}");
                    }

                    weaponState.ammoTypes.Add(ammoState);
                }

                weaponState.originalRackTotalBudget = GetWeaponRackGrandTotal(weaponState);

                if (weaponState.usesAutocannonFeedMode)
                {
                    weaponState.totalLoadedRounds = weaponState.ammoTypes.Sum(ammo => ammo.currentLoadedCount);
                    if (feed?.LoadedClipType != null)
                    {
                        for (int ammoIndex = 0; ammoIndex < weaponState.ammoTypes.Count; ammoIndex++)
                        {
                            if (weaponState.ammoTypes[ammoIndex].ammoClipType == feed.LoadedClipType)
                            {
                                weaponState.selectedChamberedIndex = ammoIndex;
                                break;
                            }
                        }
                    }

                    Log($"读取机炮已装填弹链: TotalLoaded={weaponState.totalLoadedRounds}, SelectedIndex={weaponState.selectedChamberedIndex}, Counts=[{string.Join(", ", weaponState.ammoTypes.Select(ammo => ammo.currentLoadedCount))}]");
                }

                Log(LocalizationManager.Get("log_ammo_read_success", weaponState.ammoTypes.Count));
                LogDesiredWeaponState("读取后UI状态", weaponState);
            }
            catch (Exception e)
            {
                MelonLogger.Error(LocalizationManager.Get("log_ammo_read_failed", e.Message));
            }
        }

        private void ApplyChanges()
        {
            try
            {
                foreach (var weaponState in weaponStates)
                {
                    var lm = weaponState.loadoutManager as GHPC.Weapons.LoadoutManager;
                    if (lm == null) continue;

                    Log("=== 开始应用弹药配置 ===");
                    LogDesiredWeaponState("应用前目标状态", weaponState);
                    LogLoadoutSnapshot("应用前运行态", lm, weaponState.weapon as WeaponSystem);

                    int ammoTypeCount = Math.Min(weaponState.ammoTypes.Count, lm.TotalAmmoCounts?.Length ?? 0);
                    var clipTypes = new AmmoType.AmmoClip[ammoTypeCount];
                    var ammoCodexes = new AmmoCodexScriptable[ammoTypeCount];
                    var ammoTypes = new AmmoType[ammoTypeCount];
                    for (int i = 0; i < ammoTypeCount; i++)
                    {
                        clipTypes[i] = weaponState.ammoTypes[i].ammoClipType ?? lm.GetAmmoClipTypeByIndex(i);
                        ammoCodexes[i] = weaponState.ammoTypes[i].ammoCodex;
                        ammoTypes[i] = weaponState.ammoTypes[i].ammoType;
                    }

                    // 应用前兜底：限制总弹药不超过原车总量
                    EnforceWeaponRackTotalBudget(weaponState, -1, -1);

                    // 更新总数
                    Log("更新TotalAmmoCounts:");
                    int grandTotal = 0;
                    for (int i = 0; i < ammoTypeCount; i++)
                    {
                        int total = 0;
                        foreach (var count in weaponState.ammoTypes[i].rackCounts) total += count;
                        Log($"  {weaponState.ammoTypes[i].typeName}: {lm.TotalAmmoCounts[i]} -> {total}");
                        lm.TotalAmmoCounts[i] = total;
                        grandTotal += total;
                    }

                    if (totalAmmoTypesField != null)
                    {
                        Log($"设置_totalAmmoTypes = {ammoTypeCount}");
                        totalAmmoTypesField.SetValue(lm, ammoTypeCount);
                    }

                    // 设置_totalAmmoCount
                    if (totalAmmoCountField != null)
                    {
                        Log($"设置_totalAmmoCount = {grandTotal}");
                        totalAmmoCountField.SetValue(lm, grandTotal);
                    }

                    Log("写回RackLoadout.AmmoCounts与Rack.ClipTypes:");
                    for (int rackIndex = 0; rackIndex < lm.RackLoadouts.Length; rackIndex++)
                    {
                        var rackLoadout = lm.RackLoadouts[rackIndex];
                        var rack = rackLoadout.Rack;
                        var desiredRackCounts = new int[ammoTypeCount];
                        for (int ammoIndex = 0; ammoIndex < ammoTypeCount; ammoIndex++)
                        {
                            desiredRackCounts[ammoIndex] = weaponState.ammoTypes[ammoIndex].rackCounts[rackIndex];
                        }

                        rackLoadout.AmmoCounts = desiredRackCounts;
                        rack.ClipTypes = clipTypes;

                        Log($"  Rack {rackIndex}: AmmoCounts=[{string.Join(", ", desiredRackCounts)}], ClipTypes=[{string.Join(", ", clipTypes.Select(DescribeClipType))}]");
                        Log($"  Rack {rackIndex}: FixedChoices={FormatFixedChoices(rackLoadout.FixedChoices)}, Forbidden=[{FormatIntArray(rackLoadout.ForbiddenAmmoIndices)}], OverrideInitialClips=[{FormatCodexArray(rackLoadout.OverrideInitialClips)}]");
                    }

                    // 清空所有弹药架
                    Log($"清空 {lm.RackLoadouts.Length} 个弹药架");
                    foreach (var rackLoadout in lm.RackLoadouts)
                    {
                        var rack = rackLoadout.Rack;
                        Log($"  Rack清空前: {GetStoredClipCount(rack)} 个弹药 / [{FormatStoredClipList(GetStoredClips(rack))}]");
                        EmptyRack(rack);
                        Log($"  Rack清空后: {GetStoredClipCount(rack)} 个弹药 / [{FormatStoredClipList(GetStoredClips(rack))}]");
                    }

                    LogLoadoutSnapshot("清空后运行态", lm, weaponState.weapon as WeaponSystem);

                    bool spawnedViaLoadoutManager = TrySpawnCurrentLoadout(lm);
                    if (!spawnedViaLoadoutManager)
                    {
                        Log("Fallback: SpawnCurrentLoadout unavailable, using manual rack fill.");
                        for (int rackIndex = 0; rackIndex < lm.RackLoadouts.Length; rackIndex++)
                        {
                            var rack = lm.RackLoadouts[rackIndex].Rack;
                            var desiredRackCounts = lm.RackLoadouts[rackIndex].AmmoCounts;
                            FillRack(rack, clipTypes, ammoTypes, desiredRackCounts);
                        }
                    }
                    else
                    {
                        Log("已使用LoadoutManager.SpawnCurrentLoadout重建各弹药架");
                    }

                    TryRefreshSnapshot(lm);
                    foreach (var rackLoadout in lm.RackLoadouts)
                    {
                        TryRefreshRackFlammables(rackLoadout.Rack);
                    }
                    LogLoadoutSnapshot(spawnedViaLoadoutManager ? "原生重建后" : "手动填充后", lm, weaponState.weapon as WeaponSystem);

                    // 检查生成结果
                    Log("检查生成结果:");
                    for (int r = 0; r < lm.RackLoadouts.Length; r++)
                    {
                        var rack = lm.RackLoadouts[r].Rack;
                        Log($"  Rack {r}: {GetStoredClipCount(rack)} 个弹药 / [{FormatStoredClipList(GetStoredClips(rack))}]");
                    }

                    // 同步老式MainGun状态（如存在）
                    var weapon = weaponState.weapon as WeaponSystem;
                    AmmoType finalSelectedAmmoType = null;
                    bool preserveFeedState = ShouldPreserveFeedState(weapon, lm);
                    bool legacyMainGunHandled = false;
                    if (!preserveFeedState)
                    {
                        legacyMainGunHandled = SyncLegacyMainGun(weapon, ammoCodexes, ammoTypes, lm.TotalAmmoCounts, weaponState.selectedChamberedIndex, weaponState.isReloading);
                    }
                    else
                    {
                        Log("检测到双路供弹/已装填带武器，保留现有Feed运行态，不执行MainGun/Feed清空同步");
                    }

                    // 设置膛内弹药（填装中跳过，避免打断填装流程）
                    if (weaponState.isReloading)
                    {
                        Log("跳过膛内弹药设置（武器正在填装）");
                    }
                    else if (weapon?.Feed != null)
                    {
                        var selectedAmmo = weaponState.selectedChamberedIndex >= 0 && weaponState.selectedChamberedIndex < clipTypes.Length
                            ? clipTypes[weaponState.selectedChamberedIndex]
                            : null;
                        var selectedAmmoType = weaponState.selectedChamberedIndex >= 0 && weaponState.selectedChamberedIndex < ammoTypes.Length
                            ? ammoTypes[weaponState.selectedChamberedIndex]
                            : null;
                        finalSelectedAmmoType = selectedAmmoType;

                        if (preserveFeedState)
                        {
                            if (weaponState.usesAutocannonFeedMode)
                            {
                                SyncAutocannonLoadedFeed(weaponState, weapon, clipTypes, ammoTypes);
                            }
                            else
                            {
                                LogFeedState("Feed保留后", weapon.Feed);
                            }
                        }
                        else
                        {
                            TrySetWeaponCurrentAmmoType(weapon, selectedAmmoType);

                            if (legacyMainGunHandled)
                            {
                                Log($"膛内弹药(backing): {DescribeObject(GetAmmoTypeInBreechBacking(weapon.Feed))} -> {DescribeObject(selectedAmmoType)}");
                                SetAmmoTypeInBreechBacking(weapon.Feed, selectedAmmoType);
                                SetLoadedClipTypeBacking(weapon.Feed, selectedAmmo);
                                SetQueuedClipTypeBacking(weapon.Feed, selectedAmmo);
                                SetQueuedClipTypeLockedIn(weapon.Feed, selectedAmmo);
                                ClearFeedQueues(weapon.Feed);
                                TrySetFeedStateFlags(weapon.Feed, false, false);
                                Log("LegacyMainGun已上膛，仅同步Feed显示状态，跳过SetNextClipType/Feed.Start()");
                            }
                            else
                            {
                                Log($"膛内弹药(backing): {DescribeObject(GetAmmoTypeInBreechBacking(weapon.Feed))} -> {DescribeObject(selectedAmmoType)}");
                                SetAmmoTypeInBreechBacking(weapon.Feed, selectedAmmoType);
                                SetLoadedClipTypeBacking(weapon.Feed, selectedAmmo);
                                SetQueuedClipTypeBacking(weapon.Feed, selectedAmmo);
                                SetQueuedClipTypeLockedIn(weapon.Feed, selectedAmmo);
                                ClearFeedQueues(weapon.Feed);
                                TrySetFeedStateFlags(weapon.Feed, false, false);
                                Log("无LegacyMainGun，直接设置AmmoTypeInBreech，跳过Feed.Start()");
                            }

                            LogFeedState("Feed同步后", weapon.Feed);
                        }
                    }

                    if (!weaponState.isReloading
                        && weaponState.selectedChamberedIndex >= 0
                        && weaponState.selectedChamberedIndex < ammoTypes.Length)
                    {
                        finalSelectedAmmoType = ammoTypes[weaponState.selectedChamberedIndex];
                    }

                    // 注册弹道
                    Log("调用RegisterAllBallistics()");
                    TryRegisterAllBallistics(lm);
                    TrySyncWeaponBallisticsState(weapon, finalSelectedAmmoType, !weaponState.isReloading);
                    LogLoadoutSnapshot("RegisterAllBallistics后", lm, weapon);

                    Log("=== 弹药配置应用完成 ===");
                }

                Log(LocalizationManager.Get("log_applied"));
            }
            catch (Exception e)
            {
                MelonLogger.Error(LocalizationManager.Get("log_apply_failed", e.Message));
                MelonLogger.Error($"Stack: {e.StackTrace}");
            }
        }

        private void EmptyRack(GHPC.Weapons.AmmoRack rack)
        {
            try
            {
                EnsureRackReflection(rack);
                SetStoredClips(rack, new List<AmmoType.AmmoClip>());
                SetSlotIndicesByAmmoType(rack, new Dictionary<AmmoType, List<byte>>());

                var visualSlots = GetVisualSlots(rack);

                if (visualSlots != null && removeVisualMethod != null)
                {
                    foreach (var slot in visualSlots)
                    {
                        removeVisualMethod.Invoke(rack, new object[] { slot });
                    }
                }

                TryRefreshRackFlammables(rack);
                Log($"EmptyRack完成: Rack={rack.name}, StoredClips={GetStoredClipCount(rack)}, SlotTypes={GetSlotIndicesCount(rack)}");
            }
            catch (Exception e)
            {
                MelonLogger.Error(LocalizationManager.Get("log_empty_rack_failed", e.Message));
            }
        }

        private static bool TrySpawnCurrentLoadout(GHPC.Weapons.LoadoutManager lm)
        {
            if (lm == null || spawnCurrentLoadoutMethod == null)
            {
                return false;
            }

            try
            {
                var parameters = spawnCurrentLoadoutMethod.GetParameters();
                if (parameters.Length == 0)
                {
                    spawnCurrentLoadoutMethod.Invoke(lm, null);
                }
                else
                {
                    spawnCurrentLoadoutMethod.Invoke(lm, new object[] { true });
                }

                return true;
            }
            catch (Exception e)
            {
                Log($"SpawnCurrentLoadout failed: {e.Message}");
                return false;
            }
        }

        private static void TryRefreshRackFlammables(GHPC.Weapons.AmmoRack rack)
        {
            if (rack == null || rackFlammablesField == null)
            {
                return;
            }

            try
            {
                var flammables = rackFlammablesField.GetValue(rack);
                if (flammables == null)
                {
                    return;
                }

                flammablesRefreshExplosivesStatusMethod?.Invoke(flammables, null);
                flammablesRefreshUndetonatedExplosivesStatusMethod?.Invoke(flammables, null);
            }
            catch (Exception e)
            {
                Log($"Rack flammables refresh failed: Rack={rack.name}, Error={e.Message}");
            }
        }

        private void FillRack(GHPC.Weapons.AmmoRack rack, AmmoType.AmmoClip[] clipTypes, AmmoType[] ammoTypes, int[] desiredRackCounts)
        {
            try
            {
                if (rack == null || clipTypes == null || desiredRackCounts == null)
                {
                    Log("FillRack跳过: 参数无效");
                    return;
                }

                EnsureRackReflection(rack);
                Log($"开始填充Rack={rack.name}: 目标=[{FormatIntArray(desiredRackCounts)}]");

                if (TryPopulateRackWithNativeMethods(rack, clipTypes, desiredRackCounts))
                {
                    TryRefreshRackFlammables(rack);
                    Log($"FillRack完成(原生): Rack={rack.name}, StoredClips={GetStoredClipCount(rack)}, SlotTypes={GetSlotIndicesCount(rack)}, 内容=[{FormatStoredClipList(GetStoredClips(rack))}]");
                    return;
                }

                var newStoredClips = new List<AmmoType.AmmoClip>();
                var slotIndices = new Dictionary<AmmoType, List<byte>>();
                byte slotIndex = 0;

                for (int ammoIndex = 0; ammoIndex < desiredRackCounts.Length && ammoIndex < clipTypes.Length; ammoIndex++)
                {
                    var clipType = clipTypes[ammoIndex];
                    int count = desiredRackCounts[ammoIndex];
                    for (int n = 0; n < count; n++)
                    {
                        if (slotIndex >= rack.ClipCapacity)
                        {
                            Log($"Rack={rack.name} 已达容量上限 {rack.ClipCapacity}，停止继续填充");
                            break;
                        }

                        newStoredClips.Add(clipType);

                        var ammoType = ammoTypes != null && ammoIndex < ammoTypes.Length ? ammoTypes[ammoIndex] : GetAmmoTypeFromClip(clipType);
                        if (ammoType != null)
                        {
                            if (!slotIndices.TryGetValue(ammoType, out var indices))
                            {
                                indices = new List<byte>();
                                slotIndices.Add(ammoType, indices);
                            }

                            indices.Add(slotIndex);
                        }

                        slotIndex++;
                    }
                }

                SetStoredClips(rack, newStoredClips);
                SetSlotIndicesByAmmoType(rack, slotIndices);
                PlaceRackVisuals(rack, ammoTypes, desiredRackCounts);
                TryRegenerateSlotIndices(rack);
                TryRefreshRackFlammables(rack);

                Log($"FillRack完成: Rack={rack.name}, StoredClips={GetStoredClipCount(rack)}, SlotTypes={GetSlotIndicesCount(rack)}, 内容=[{FormatStoredClipList(GetStoredClips(rack))}]");
            }
            catch (Exception e)
            {
                MelonLogger.Error(LocalizationManager.Get("log_fill_rack_failed", e.Message));
            }
        }

        private static bool TryPopulateRackWithNativeMethods(GHPC.Weapons.AmmoRack rack, AmmoType.AmmoClip[] clipTypes, int[] desiredRackCounts)
        {
            if (rack == null || clipTypes == null || desiredRackCounts == null)
            {
                return false;
            }

            try
            {
                int desiredTotal = 0;
                foreach (int desiredCount in desiredRackCounts)
                {
                    desiredTotal += Math.Max(0, desiredCount);
                }

                int visibleSlotCount = 0;
                var visualSlots = GetVisualSlots(rack);
                if (visualSlots != null)
                {
                    foreach (var _ in visualSlots)
                    {
                        visibleSlotCount++;
                    }
                }

                int addedCount = 0;
                for (int ammoIndex = 0; ammoIndex < desiredRackCounts.Length && ammoIndex < clipTypes.Length; ammoIndex++)
                {
                    var clipType = clipTypes[ammoIndex];
                    int desiredCount = Math.Max(0, desiredRackCounts[ammoIndex]);
                    if (clipType == null)
                    {
                        continue;
                    }

                    for (int count = 0; count < desiredCount; count++)
                    {
                        if (addedCount >= rack.ClipCapacity)
                        {
                            Log($"Rack={rack.name} 已达容量上限 {rack.ClipCapacity}，停止继续填充(原生)");
                            TryRegenerateSlotIndices(rack);
                            return true;
                        }

                        if (addedCount < visibleSlotCount && rackAddVisibleClipMethod != null)
                        {
                            rackAddVisibleClipMethod.Invoke(rack, new object[] { addedCount, clipType, true });
                        }
                        else if (rackAddInvisibleClipMethod != null)
                        {
                            rackAddInvisibleClipMethod.Invoke(rack, new object[] { clipType });
                        }
                        else if (rackAddClipToAnySlotMethod != null)
                        {
                            rackAddClipToAnySlotMethod.Invoke(rack, new object[] { clipType });
                        }
                        else
                        {
                            return false;
                        }

                        addedCount++;
                    }
                }

                TryRegenerateSlotIndices(rack);
                return addedCount > 0 || desiredTotal == 0;
            }
            catch (Exception e)
            {
                Log($"TryPopulateRackWithNativeMethods failed: Rack={rack.name}, Error={e.Message}");
                return false;
            }
        }

        private void LogDesiredWeaponState(string stage, WeaponState weaponState)
        {
            if (weaponState == null)
            {
                Log($"{stage}: WeaponState=null");
                return;
            }

            Log($"--- {stage} / {weaponState.weaponName} ---");
            for (int ammoIndex = 0; ammoIndex < weaponState.ammoTypes.Count; ammoIndex++)
            {
                var ammo = weaponState.ammoTypes[ammoIndex];
                Log($"  Ammo[{ammoIndex}] {ammo.typeName}: RackCounts=[{FormatIntArray(ammo.rackCounts)}], RackCaps=[{FormatIntArray(ammo.rackCapacities)}], 合计={ammo.rackCounts.Sum()}, Loaded={ammo.currentLoadedCount}");
            }

            if (weaponState.ammoTypes.Count > 0 && weaponState.selectedChamberedIndex >= 0 && weaponState.selectedChamberedIndex < weaponState.ammoTypes.Count)
            {
                Log($"  目标膛内弹药: index={weaponState.selectedChamberedIndex}, type={weaponState.ammoTypes[weaponState.selectedChamberedIndex].typeName}");
            }
        }

        private void LogLoadoutSnapshot(string stage, GHPC.Weapons.LoadoutManager lm, WeaponSystem weapon)
        {
            if (lm == null)
            {
                Log($"{stage}: LoadoutManager=null");
                return;
            }

            Log($"--- {stage} ---");
            Log($"TotalAmmoCounts=[{FormatIntArray(lm.TotalAmmoCounts)}], _totalAmmoCount={totalAmmoCountField?.GetValue(lm) ?? "<null>"}, _totalAmmoTypes={totalAmmoTypesField?.GetValue(lm) ?? "<null>"}");
            Log($"LoadedAmmoList=[{FormatCodexArray(lm.LoadedAmmoList?.AmmoClips)}]");

            if (lm.RackLoadouts != null)
            {
                for (int rackIndex = 0; rackIndex < lm.RackLoadouts.Length; rackIndex++)
                {
                    var rackLoadout = lm.RackLoadouts[rackIndex];
                    var rack = rackLoadout.Rack;
                    if (rack == null)
                    {
                        Log($"  Rack {rackIndex}: null");
                        continue;
                    }

                    Log($"  Rack {rackIndex} {rack.name}: Capacity={rack.ClipCapacity}, Stored={GetStoredClipCount(rack)}, ClipTypes=[{FormatClipTypeArray(rack.ClipTypes)}], StoredClips=[{FormatStoredClipList(GetStoredClips(rack))}]");
                    Log($"    RackLoadout.AmmoCounts=[{FormatIntArray(rackLoadout.AmmoCounts)}], FixedChoices={FormatFixedChoices(rackLoadout.FixedChoices)}, Forbidden=[{FormatIntArray(rackLoadout.ForbiddenAmmoIndices)}], OverrideInitialClips=[{FormatCodexArray(rackLoadout.OverrideInitialClips)}]");
                    Log($"    SlotIndicesByAmmoType={FormatSlotIndices(GetSlotIndicesByAmmoType(rack))}");
                }
            }

            if (weapon?.Feed != null)
            {
                LogFeedState($"{stage} / Feed", weapon.Feed);
            }
        }

        private void LogFeedState(string stage, AmmoFeed feed)
        {
            if (feed == null)
            {
                Log($"{stage}: Feed=null");
                return;
            }

            try
            {
                Log($"{stage}: AmmoTypeInBreech={DescribeObject(feed.AmmoTypeInBreech)}, LoadedClipType={DescribeClipType(feed.LoadedClipType)}, QueuedClipType={DescribeClipType(feed.QueuedClipType)}, ReserveCount={feed.ReserveCount}, CurrentClipRemainingCount={feed.CurrentClipRemainingCount}, ReadyToReload={feed.ReadyToReload}, Reloading={feed.Reloading}");
                Log($"{stage}: ReadyRack={(feed.ReadyRack != null ? feed.ReadyRack.name : "<null>")}, LoadedClip=[{FormatAmmoTypeQueue(GetFeedLoadedClip(feed))}], FeedClipMain=[{FormatAmmoTypeQueue(GetFeedClipMain(feed))}], FeedClipAux=[{FormatAmmoTypeQueue(GetFeedClipAux(feed))}]");
            }
            catch (Exception e)
            {
                Log($"{stage}: Feed日志采集失败: {e.Message}");
            }
        }
        private static string FormatIntArray(int[] values)
        {
            return values == null ? "null" : string.Join(", ", values);
        }

        private static string FormatCodexArray(AmmoClipCodexScriptable[] values)
        {
            return values == null ? "null" : string.Join(", ", values.Select(DescribeObject));
        }

        private static string FormatClipTypeArray(AmmoType.AmmoClip[] values)
        {
            return values == null ? "null" : string.Join(", ", values.Select(DescribeClipType));
        }

        private static string FormatStoredClipList(IEnumerable values)
        {
            if (values == null)
            {
                return "null";
            }

            var items = new List<string>();
            foreach (var value in values)
            {
                items.Add(DescribeObject(value));
            }

            return string.Join(", ", items);
        }

        private static string FormatAmmoTypeQueue(IEnumerable values)
        {
            if (values == null)
            {
                return "null";
            }

            var items = new List<string>();
            foreach (var value in values)
            {
                items.Add(DescribeObject(value));
            }

            return string.Join(", ", items);
        }

        private static string FormatFixedChoices(GHPC.Weapons.LoadoutManager.RackLoadoutFixedChoice[] values)
        {
            if (values == null)
            {
                return "null";
            }

            return string.Join(", ", values.Select(choice => choice == null ? "null" : $"slot={choice.RackSlotIndex}/ammo={choice.AmmoClipIndex}"));
        }

        private static string FormatSlotIndices(IDictionary slotIndices)
        {
            if (slotIndices == null)
            {
                return "null";
            }

            var parts = new List<string>();
            foreach (DictionaryEntry entry in slotIndices)
            {
                var values = entry.Value as IEnumerable;
                var inner = new List<string>();
                if (values != null)
                {
                    foreach (var value in values)
                    {
                        inner.Add(value?.ToString() ?? "null");
                    }
                }

                parts.Add($"{DescribeObject(entry.Key)}=[{string.Join(", ", inner)}]");
            }

            return string.Join(", ", parts);
        }

        private static object GetPropertyValue(object obj, string propertyName)
        {
            return obj?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj, null);
        }

        private static string DescribeClipType(AmmoType.AmmoClip clip)
        {
            return clip == null ? "null" : clip.Name;
        }

        private static void EnsureRackReflection(GHPC.Weapons.AmmoRack rack)
        {
            if (rack == null)
            {
                return;
            }

            bool needed = removeVisualMethod == null
                || storedClipsProperty == null
                || storedClipsBackingField == null
                || slotIndicesByAmmoTypeField == null
                || visualSlotsField == null
                || rackRegenerateSlotIndicesMethod == null;

            if (!needed)
            {
                return;
            }

            var type = rack.GetType();
            if (removeVisualMethod == null) removeVisualMethod = FindMethodInHierarchy(type, "RemoveAmmoVisualFromSlot");
            if (storedClipsProperty == null) storedClipsProperty = FindPropertyInHierarchy(type, "StoredClips");
            if (storedClipsBackingField == null) storedClipsBackingField = FindFieldInHierarchy(type, "<StoredClips>k__BackingField");
            if (slotIndicesByAmmoTypeField == null) slotIndicesByAmmoTypeField = FindFieldInHierarchy(type, "SlotIndicesByAmmoType");
            if (visualSlotsField == null) visualSlotsField = FindFieldInHierarchy(type, "VisualSlots");
            if (rackRegenerateSlotIndicesMethod == null) rackRegenerateSlotIndicesMethod = FindMethodInHierarchy(type, "RegenerateSlotIndices");

            Log($"Rack反射解析: RuntimeType={type.FullName}, RemoveVisual={(removeVisualMethod != null)}, StoredClipsProp={(storedClipsProperty != null)}, StoredClipsBacking={(storedClipsBackingField != null)}, SlotIndicesField={(slotIndicesByAmmoTypeField != null)}, VisualSlotsField={(visualSlotsField != null)}, Regenerate={(rackRegenerateSlotIndicesMethod != null)}");
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string name)
        {
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static PropertyInfo FindPropertyInHierarchy(Type type, string name)
        {
            while (type != null)
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindMethodInHierarchy(Type type, string name)
        {
            while (type != null)
            {
                var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }
        private bool SyncLegacyMainGun(WeaponSystem weapon, AmmoCodexScriptable[] ammoCodexes, AmmoType[] ammoTypes, int[] ammoCounts, int selectedIndex, bool skipChamber = false)
        {
            try
            {
                if (weapon == null)
                {
                    return false;
                }

                var legacyMainGun = FindLegacyMainGun(weapon);
                if (legacyMainGun == null)
                {
                    Log($"SyncLegacyMainGun: 未找到匹配MainGun, Weapon={weapon.name}, ReadyRack={(weapon.Feed?.ReadyRack != null ? weapon.Feed.ReadyRack.name : "<null>")}");
                    return false;
                }

                mainGunUseAmmoRacksProperty?.SetValue(legacyMainGun, true, null);
                mainGunAmmoField?.SetValue(legacyMainGun, ammoCodexes);
                mainGunAvailableAmmoField?.SetValue(legacyMainGun, ammoTypes);
                mainGunAmmoCountsField?.SetValue(legacyMainGun, (int[])ammoCounts.Clone());
                mainGunUpdateAmmoCountsMethod?.Invoke(legacyMainGun, null);

                bool chambered = false;
                if (!skipChamber && selectedIndex >= 0 && selectedIndex < ammoCounts.Length)
                {
                    mainGunCurrentAmmoIndexField?.SetValue(legacyMainGun, selectedIndex);
                    mainGunNextAmmoIndexField?.SetValue(legacyMainGun, selectedIndex);
                    mainGunSelectAmmoTypeMethod?.Invoke(legacyMainGun, new object[] { selectedIndex });
                    if (ammoCounts[selectedIndex] > 0)
                    {
                        mainGunAmmoIndexInBreechField?.SetValue(legacyMainGun, selectedIndex);
                        mainGunForceRoundToBreechMethod?.Invoke(legacyMainGun, new object[] { selectedIndex });
                        chambered = true;
                        Log($"SyncLegacyMainGun: ForceRoundToBreech index={selectedIndex}");
                    }
                    else
                    {
                        mainGunAmmoIndexInBreechField?.SetValue(legacyMainGun, -1);
                    }
                }
                else if (skipChamber)
                {
                    Log("SyncLegacyMainGun: 跳过膛内弹药设置（正在填装）");
                }

                mainGunUpdateAmmoCountsMethod?.Invoke(legacyMainGun, null);
                var legacyAmmoCounts = mainGunAmmoCountsField?.GetValue(legacyMainGun) as int[];
                Log($"SyncLegacyMainGun: CurrentAmmo={DescribeObject(GetPropertyValue(legacyMainGun, "CurrentAmmo"))}, NextAmmo={DescribeObject(GetPropertyValue(legacyMainGun, "NextAmmo"))}, CurrentClipRemainingCount={GetPropertyValue(legacyMainGun, "CurrentClipRemainingCount")}, ReadyRackReserveCount={GetPropertyValue(legacyMainGun, "ReadyRackReserveCount")}, _ammoIndexInBreech={mainGunAmmoIndexInBreechField?.GetValue(legacyMainGun)}, _currentAmmoIndex={mainGunCurrentAmmoIndexField?.GetValue(legacyMainGun)}, _nextAmmoIndex={mainGunNextAmmoIndexField?.GetValue(legacyMainGun)}, AmmoCounts=[{FormatIntArray(legacyAmmoCounts)}]");
                return chambered;
            }
            catch (Exception e)
            {
                Log($"SyncLegacyMainGun失败: {e.Message}");
                return false;
            }
        }

        private static int GetEnumerableCount(IEnumerable values)
        {
            if (values == null)
            {
                return 0;
            }

            if (values is ICollection collection)
            {
                return collection.Count;
            }

            int count = 0;
            foreach (var _ in values)
            {
                count++;
            }

            return count;
        }

        private static bool GetDualFeedFlag(AmmoFeed feed)
        {
            try
            {
                var field = feed?.GetType().GetField("DualFeed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var value = field?.GetValue(feed);
                return value is bool enabled && enabled;
            }
            catch
            {
                return false;
            }
        }
        private static bool IsAutocannonFeedWeapon(WeaponSystem weapon, GHPC.Weapons.LoadoutManager lm)
        {
            var feed = weapon?.Feed;
            if (feed == null)
            {
                return false;
            }

            return GetDualFeedFlag(feed)
                || HasOverrideInitialClips(lm)
                || GetEnumerableCount(GetFeedClipAux(feed)) > 0
                || feed.CurrentClipRemainingCount > 1;
        }

        private static object GetLoadedClipQueueByType(AmmoFeed feed, AmmoType.AmmoClip clipType)
        {
            try
            {
                return feed?.GetType().GetMethod("GetLoadedClipByType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(feed, new object[] { clipType });
            }
            catch (Exception e)
            {
                Log($"GetLoadedClipByType失败: Clip={DescribeClipType(clipType)}, Error={e.Message}");
                return null;
            }
        }

        private static int GetLoadedClipCountByType(AmmoFeed feed, AmmoType.AmmoClip clipType)
        {
            try
            {
                if (feed == null || clipType == null)
                {
                    return 0;
                }

                int queueCount = GetEnumerableCount(GetLoadedClipQueueByType(feed, clipType) as IEnumerable);
                if (queueCount > 0)
                {
                    return queueCount;
                }

                var remainingMethod = feed.GetType().GetMethod("GetLoadedClipRemainingByType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var remainingValue = remainingMethod?.Invoke(feed, new object[] { clipType });
                if (remainingValue is int remainingCount && remainingCount > 0)
                {
                    return remainingCount;
                }

                if (feed.LoadedClipType == clipType)
                {
                    return Math.Max(0, feed.CurrentClipRemainingCount);
                }
            }
            catch (Exception e)
            {
                Log($"GetLoadedClipCountByType失败: Clip={DescribeClipType(clipType)}, Error={e.Message}");
            }

            return 0;
        }

        private static bool TryPopulateLoadedClipQueue(object queue, AmmoType ammoType, AmmoType.AmmoClip clipType, int count)
        {
            try
            {
                if (queue == null)
                {
                    return false;
                }

                var queueType = queue.GetType();
                var clearMethod = queueType.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
                var enqueueMethod = queueType.GetMethod("Enqueue", BindingFlags.Instance | BindingFlags.Public);
                var itemType = queueType.GetGenericArguments().FirstOrDefault();
                if (clearMethod == null || enqueueMethod == null || itemType == null)
                {
                    return false;
                }

                clearMethod.Invoke(queue, null);

                object queueItem = null;
                if (ammoType != null && itemType.IsInstanceOfType(ammoType))
                {
                    queueItem = ammoType;
                }
                else if (clipType != null && itemType.IsInstanceOfType(clipType))
                {
                    queueItem = clipType;
                }
                else
                {
                    var ammoCodex = clipType?.MinimalPattern != null && clipType.MinimalPattern.Length > 0 ? clipType.MinimalPattern[0] : null;
                    if (ammoCodex != null && itemType.IsInstanceOfType(ammoCodex))
                    {
                        queueItem = ammoCodex;
                    }
                }

                if (queueItem == null)
                {
                    return count == 0;
                }

                for (int i = 0; i < count; i++)
                {
                    enqueueMethod.Invoke(queue, new[] { queueItem });
                }

                return true;
            }
            catch (Exception e)
            {
                Log($"TryPopulateLoadedClipQueue失败: Clip={DescribeClipType(clipType)}, Count={count}, Error={e.Message}");
                return false;
            }
        }

        private static int GetVisibleRackStartIndex(WeaponState weapon)
        {
            if (weapon == null || weapon.ammoTypes == null || weapon.ammoTypes.Count == 0)
            {
                return 0;
            }

            int rackCount = weapon.ammoTypes[0].rackCounts?.Length ?? 0;
            // 只有在配置开启且是机炮供弹模式时才隐藏Rack0
            // Rack0即为待发弹药架上的弹药，原版游戏对于机炮车一般默认为0
            bool shouldHideRack0 = hideRack0ForAutocannon.Value && weapon.usesAutocannonFeedMode && rackCount > 0;
            return shouldHideRack0 ? 1 : 0;
        }

        private static int GetVisibleRackAmmoTotal(WeaponState weapon, AmmoTypeState ammo)
        {
            if (weapon == null || ammo?.rackCounts == null)
            {
                return 0;
            }

            int total = 0;
            for (int rackIndex = GetVisibleRackStartIndex(weapon); rackIndex < ammo.rackCounts.Length; rackIndex++)
            {
                total += ammo.rackCounts[rackIndex];
            }

            return total;
        }

        private static void RebalanceLoadedAmmoCounts(WeaponState weapon, int changedAmmoIndex, int requestedValue)
        {
            if (weapon == null || weapon.ammoTypes == null || weapon.ammoTypes.Count == 0)
            {
                return;
            }

            int fixedTotal = Math.Max(0, weapon.totalLoadedRounds);
            int clampedValue = Mathf.Clamp(requestedValue, 0, fixedTotal);
            weapon.ammoTypes[changedAmmoIndex].currentLoadedCount = clampedValue;

            int remaining = fixedTotal - clampedValue;
            int currentOtherTotal = 0;
            for (int ammoIndex = 0; ammoIndex < weapon.ammoTypes.Count; ammoIndex++)
            {
                if (ammoIndex == changedAmmoIndex)
                {
                    continue;
                }

                currentOtherTotal += Math.Max(0, weapon.ammoTypes[ammoIndex].currentLoadedCount);
            }

            if (currentOtherTotal > remaining)
            {
                int overflow = currentOtherTotal - remaining;
                for (int ammoIndex = 0; ammoIndex < weapon.ammoTypes.Count && overflow > 0; ammoIndex++)
                {
                    if (ammoIndex == changedAmmoIndex)
                    {
                        continue;
                    }

                    int current = Math.Max(0, weapon.ammoTypes[ammoIndex].currentLoadedCount);
                    int reduction = Math.Min(current, overflow);
                    weapon.ammoTypes[ammoIndex].currentLoadedCount = current - reduction;
                    overflow -= reduction;
                }
            }
            else if (currentOtherTotal < remaining)
            {
                int deficit = remaining - currentOtherTotal;
                for (int ammoIndex = 0; ammoIndex < weapon.ammoTypes.Count && deficit > 0; ammoIndex++)
                {
                    if (ammoIndex == changedAmmoIndex)
                    {
                        continue;
                    }

                    weapon.ammoTypes[ammoIndex].currentLoadedCount = Math.Max(0, weapon.ammoTypes[ammoIndex].currentLoadedCount) + deficit;
                    deficit = 0;
                }
            }
        }

        private static int ResolveAutocannonSelectedAmmoIndex(WeaponState weapon)
        {
            if (weapon == null || weapon.ammoTypes == null || weapon.ammoTypes.Count == 0)
            {
                return -1;
            }

            if (weapon.selectedChamberedIndex >= 0
                && weapon.selectedChamberedIndex < weapon.ammoTypes.Count
                && weapon.ammoTypes[weapon.selectedChamberedIndex].currentLoadedCount > 0)
            {
                return weapon.selectedChamberedIndex;
            }

            for (int ammoIndex = 0; ammoIndex < weapon.ammoTypes.Count; ammoIndex++)
            {
                if (weapon.ammoTypes[ammoIndex].currentLoadedCount > 0)
                {
                    return ammoIndex;
                }
            }

            return Mathf.Clamp(weapon.selectedChamberedIndex, 0, weapon.ammoTypes.Count - 1);
        }

        private void SyncAutocannonLoadedFeed(WeaponState weaponState, WeaponSystem weapon, AmmoType.AmmoClip[] clipTypes, AmmoType[] ammoTypes)
        {
            var feed = weapon?.Feed;
            if (feed == null || weaponState == null)
            {
                return;
            }

            for (int ammoIndex = 0; ammoIndex < weaponState.ammoTypes.Count && ammoIndex < clipTypes.Length; ammoIndex++)
            {
                var clipType = clipTypes[ammoIndex];
                var ammoType = ammoIndex < ammoTypes.Length ? ammoTypes[ammoIndex] : GetAmmoTypeFromClip(clipType);
                int desiredCount = Math.Max(0, weaponState.ammoTypes[ammoIndex].currentLoadedCount);
                var queue = GetLoadedClipQueueByType(feed, clipType);
                int beforeCount = GetEnumerableCount(queue as IEnumerable);
                bool applied = TryPopulateLoadedClipQueue(queue, ammoType, clipType, desiredCount);
                int afterCount = GetEnumerableCount(queue as IEnumerable);
                Log($"同步机炮已装填弹链: {weaponState.ammoTypes[ammoIndex].typeName}, Before={beforeCount}, Desired={desiredCount}, After={afterCount}, Applied={applied}");
            }

            int mainDesiredCount = weaponState.ammoTypes.Count > 0 ? Math.Max(0, weaponState.ammoTypes[0].currentLoadedCount) : 0;
            int auxDesiredCount = weaponState.ammoTypes.Count > 1 ? Math.Max(0, weaponState.ammoTypes[1].currentLoadedCount) : 0;

            var mainClipType = clipTypes.Length > 0 ? clipTypes[0] : null;
            var auxClipType = clipTypes.Length > 1 ? clipTypes[1] : null;
            var mainAmmoType = ammoTypes.Length > 0 ? ammoTypes[0] : GetAmmoTypeFromClip(mainClipType);
            var auxAmmoType = ammoTypes.Length > 1 ? ammoTypes[1] : GetAmmoTypeFromClip(auxClipType);

            object mainQueue = feedClipMainField?.GetValue(feed);
            object auxQueue = feedClipAuxField?.GetValue(feed);
            bool mainAppliedToFeed = TryPopulateLoadedClipQueue(mainQueue, mainAmmoType, mainClipType, mainDesiredCount);
            bool auxAppliedToFeed = TryPopulateLoadedClipQueue(auxQueue, auxAmmoType, auxClipType, auxDesiredCount);
            Log($"同步机炮主/副供弹链: Main={DescribeClipType(mainClipType)} {mainDesiredCount} Applied={mainAppliedToFeed} Actual={GetEnumerableCount(mainQueue as IEnumerable)}, Aux={DescribeClipType(auxClipType)} {auxDesiredCount} Applied={auxAppliedToFeed} Actual={GetEnumerableCount(auxQueue as IEnumerable)}");

            int selectedIndex = ResolveAutocannonSelectedAmmoIndex(weaponState);
            if (selectedIndex >= 0 && selectedIndex < clipTypes.Length)
            {
                var selectedClipType = clipTypes[selectedIndex];
                var selectedAmmoType = selectedIndex < ammoTypes.Length ? ammoTypes[selectedIndex] : GetAmmoTypeFromClip(selectedClipType);
                int selectedCount = selectedIndex < weaponState.ammoTypes.Count ? Math.Max(0, weaponState.ammoTypes[selectedIndex].currentLoadedCount) : 0;
                bool useAuxFeed = GetDualFeedFlag(feed) && selectedIndex > 0;

                TrySetAuxFeedMode(feed, useAuxFeed);
                TrySetNextClipType(feed, selectedClipType);

                object loadedQueue = GetFeedLoadedClipObject(feed);
                bool loadedApplied = TryPopulateLoadedClipQueue(loadedQueue, selectedAmmoType, selectedClipType, selectedCount);

                weaponState.selectedChamberedIndex = selectedIndex;
                TrySetWeaponCurrentAmmoType(weapon, selectedAmmoType);
                SetLoadedClipTypeBacking(feed, selectedClipType);
                SetQueuedClipTypeBacking(feed, selectedClipType);
                SetQueuedClipTypeLockedIn(feed, selectedClipType);
                SetAmmoTypeInBreechBacking(feed, selectedAmmoType);

                Log($"同步机炮当前供弹链: SelectedIndex={selectedIndex}, AuxMode={useAuxFeed}, Clip={DescribeClipType(selectedClipType)}, Desired={selectedCount}, LoadedApplied={loadedApplied}, LoadedActual={GetEnumerableCount(loadedQueue as IEnumerable)}");
            }

            TrySetFeedStateFlags(feed, false, false);
            LogFeedState("机炮弹链同步后", feed);
        }

        private static bool HasOverrideInitialClips(GHPC.Weapons.LoadoutManager lm)
        {
            if (lm?.RackLoadouts == null)
            {
                return false;
            }

            foreach (var rackLoadout in lm.RackLoadouts)
            {
                if (rackLoadout?.OverrideInitialClips != null && rackLoadout.OverrideInitialClips.Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldPreserveFeedState(WeaponSystem weapon, GHPC.Weapons.LoadoutManager lm)
        {
            var feed = weapon?.Feed;
            if (feed == null)
            {
                return false;
            }

            bool dualFeed = GetDualFeedFlag(feed);
            int loadedClipCount = GetEnumerableCount(GetFeedLoadedClip(feed));
            int feedClipMainCount = GetEnumerableCount(GetFeedClipMain(feed));
            int feedClipAuxCount = GetEnumerableCount(GetFeedClipAux(feed));
            bool hasOverrideInitialClips = HasOverrideInitialClips(lm);
            bool preserve = dualFeed
                || hasOverrideInitialClips
                || loadedClipCount > 0
                || feedClipMainCount > 0
                || feedClipAuxCount > 0
                || feed.CurrentClipRemainingCount > 1;

            Log($"Feed路径判定: Weapon={(weapon != null ? weapon.name : "<null>")}, DualFeed={dualFeed}, LoadedClipCount={loadedClipCount}, FeedClipMainCount={feedClipMainCount}, FeedClipAuxCount={feedClipAuxCount}, CurrentClipRemainingCount={feed.CurrentClipRemainingCount}, OverrideInitialClips={hasOverrideInitialClips}, Preserve={preserve}");
            return preserve;
        }

        private void PlaceRackVisuals(GHPC.Weapons.AmmoRack rack, AmmoType[] ammoTypes, int[] desiredRackCounts)
        {
            try
            {
                if (rack == null || ammoTypes == null || desiredRackCounts == null)
                {
                    return;
                }

                var visualSlots = GetVisualSlots(rack);
                var visualSlotList = new List<Transform>();
                if (visualSlots != null)
                {
                    foreach (var slot in visualSlots)
                    {
                        if (slot is Transform slotTransform)
                        {
                            visualSlotList.Add(slotTransform);
                        }
                    }
                }

                int maxVisualSlots = Math.Min(rack.ClipCapacity, visualSlotList.Count);
                if (maxVisualSlots <= 0)
                {
                    Log($"PlaceRackVisuals跳过: Rack={rack.name}, VisualSlots={visualSlotList.Count}, ClipCapacity={rack.ClipCapacity}");
                    return;
                }

                byte slotIndex = 0;
                for (int ammoIndex = 0; ammoIndex < desiredRackCounts.Length && ammoIndex < ammoTypes.Length; ammoIndex++)
                {
                    var ammoType = ammoTypes[ammoIndex];
                    int count = desiredRackCounts[ammoIndex];
                    for (int n = 0; n < count; n++)
                    {
                        if (slotIndex >= maxVisualSlots)
                        {
                            Log($"PlaceRackVisuals提前结束: Rack={rack.name}, SlotIndex={slotIndex}, MaxVisualSlots={maxVisualSlots}");
                            return;
                        }

                        if (ammoType != null)
                        {
                            try
                            {
                                if (placeAmmoVisualMethod != null)
                                {
                                    placeAmmoVisualMethod.Invoke(rack, new object[] { ammoType, visualSlotList[slotIndex] });
                                }
                                else
                                {
                                    rack.PlaceInertAmmoVisualInSlot(ammoType, slotIndex);
                                }
                            }
                            catch (Exception slotException)
                            {
                                Log($"PlaceRackVisuals槽位失败: Rack={rack.name}, Slot={slotIndex}, Ammo={DescribeObject(ammoType)}, Error={slotException.Message}");
                            }
                        }

                        slotIndex++;
                    }
                }
            }
            catch (Exception e)
            {
                Log($"PlaceRackVisuals失败: {e.Message}");
            }
        }

        private static AmmoCodexScriptable GetAmmoCodexFromClipCodex(AmmoClipCodexScriptable clipCodex)
        {
            if (clipCodex == null || clipCodex.ClipType == null || clipCodex.ClipType.MinimalPattern == null || clipCodex.ClipType.MinimalPattern.Length == 0)
            {
                return null;
            }

            return clipCodex.ClipType.MinimalPattern[0];
        }
        private static IEnumerable GetFeedLoadedClip(AmmoFeed feed)
        {
            try
            {
                return feedLoadedClipProperty?.GetValue(feed, null) as IEnumerable;
            }
            catch
            {
                return null;
            }
        }

        private static object GetFeedLoadedClipObject(AmmoFeed feed)
        {
            try
            {
                return feedLoadedClipProperty?.GetValue(feed, null);
            }
            catch
            {
                return null;
            }
        }
        private static object GetAmmoTypeInBreechBacking(AmmoFeed feed)
        {
            return ammoTypeInBreechBackingField?.GetValue(feed);
        }

        private static void SetAmmoTypeInBreechBacking(AmmoFeed feed, object value)
        {
            ammoTypeInBreechBackingField?.SetValue(feed, value);
        }

        private static void SetLoadedClipTypeBacking(AmmoFeed feed, AmmoType.AmmoClip value)
        {
            loadedClipTypeBackingField?.SetValue(feed, value);
        }

        private static void SetQueuedClipTypeBacking(AmmoFeed feed, AmmoType.AmmoClip value)
        {
            queuedClipTypeBackingField?.SetValue(feed, value);
        }

        private static void SetQueuedClipTypeLockedIn(AmmoFeed feed, AmmoType.AmmoClip value)
        {
            queuedClipTypeLockedInField?.SetValue(feed, value);
        }

        private static void TrySetAuxFeedMode(AmmoFeed feed, bool useAuxFeed)
        {
            try
            {
                feedAuxFeedModeField?.SetValue(feed, useAuxFeed);
            }
            catch (Exception e)
            {
                Log($"设置Feed._auxFeedMode失败: {e.Message}");
            }
        }

        private static void TrySetNextClipType(AmmoFeed feed, AmmoType.AmmoClip clipType)
        {
            try
            {
                if (feedSetNextClipTypeMethod != null && clipType != null)
                {
                    feedSetNextClipTypeMethod.Invoke(feed, new object[] { clipType });
                }
            }
            catch (Exception e)
            {
                Log($"调用Feed.SetNextClipType失败: Clip={DescribeClipType(clipType)}, Error={e.Message}");
            }
        }

        private static IEnumerable GetFeedClipMain(AmmoFeed feed)
        {
            return feedClipMainField?.GetValue(feed) as IEnumerable;
        }

        private static IEnumerable GetFeedClipAux(AmmoFeed feed)
        {
            return feedClipAuxField?.GetValue(feed) as IEnumerable;
        }

        private static void ClearFeedQueues(AmmoFeed feed)
        {
            ClearQueueObject(feedClipMainField?.GetValue(feed));
            ClearQueueObject(feedClipAuxField?.GetValue(feed));
        }

        private static void ClearQueueObject(object queue)
        {
            queue?.GetType().GetMethod("Clear")?.Invoke(queue, null);
        }
        private static AmmoType GetAmmoTypeFromClip(AmmoType.AmmoClip clip)
        {
            return clip?.MinimalPattern != null && clip.MinimalPattern.Length > 0 ? clip.MinimalPattern[0]?.AmmoType : null;
        }

        private static void TryRegenerateSlotIndices(GHPC.Weapons.AmmoRack rack)
        {
            try
            {
                EnsureRackReflection(rack);
                rackRegenerateSlotIndicesMethod?.Invoke(rack, null);
            }
            catch (Exception e)
            {
                Log($"TryRegenerateSlotIndices失败: {e.Message}");
            }
        }
        private static IList GetStoredClips(GHPC.Weapons.AmmoRack rack)
        {
            EnsureRackReflection(rack);
            return storedClipsProperty?.GetValue(rack, null) as IList ?? storedClipsBackingField?.GetValue(rack) as IList;
        }

        private static int GetStoredClipCount(GHPC.Weapons.AmmoRack rack)
        {
            return GetStoredClips(rack)?.Count ?? 0;
        }

        private static void SetStoredClips(GHPC.Weapons.AmmoRack rack, object value)
        {
            EnsureRackReflection(rack);
            if (storedClipsProperty != null)
            {
                storedClipsProperty.SetValue(rack, value, null);
            }

            if (storedClipsBackingField != null)
            {
                storedClipsBackingField.SetValue(rack, value);
            }
        }

        private static IDictionary GetSlotIndicesByAmmoType(GHPC.Weapons.AmmoRack rack)
        {
            EnsureRackReflection(rack);
            return slotIndicesByAmmoTypeField?.GetValue(rack) as IDictionary;
        }

        private static int GetSlotIndicesCount(GHPC.Weapons.AmmoRack rack)
        {
            return GetSlotIndicesByAmmoType(rack)?.Count ?? 0;
        }

        private static void SetSlotIndicesByAmmoType(GHPC.Weapons.AmmoRack rack, object value)
        {
            EnsureRackReflection(rack);
            slotIndicesByAmmoTypeField?.SetValue(rack, value);
        }

        private static IEnumerable GetVisualSlots(GHPC.Weapons.AmmoRack rack)
        {
            EnsureRackReflection(rack);
            return visualSlotsField?.GetValue(rack) as IEnumerable;
        }

        private static void TrySetWeaponCurrentAmmoType(WeaponSystem weapon, AmmoType ammoType)
        {
            try
            {
                weaponCurrentAmmoTypeProperty?.SetValue(weapon, ammoType, null);
                Log($"同步Weapon.CurrentAmmoType: {DescribeObject(ammoType)}");
            }
            catch (Exception e)
            {
                Log($"设置Weapon.CurrentAmmoType失败: {e.Message}");
            }
        }

        private static void TrySyncWeaponBallisticsState(WeaponSystem weapon, AmmoType ammoType, bool notifyRoundInBreech)
        {
            if (weapon == null || ammoType == null)
            {
                return;
            }

            TrySetWeaponCurrentAmmoType(weapon, ammoType);

            var fcs = weapon.FCS;
            if (fcs != null)
            {
                try
                {
                    fcsCurrentAmmoTypeProperty?.SetValue(fcs, ammoType, null);
                    Log($"同步FCS.CurrentAmmoType: {DescribeObject(ammoType)}");
                }
                catch (Exception e)
                {
                    Log($"设置FCS.CurrentAmmoType失败: {e.Message}");
                }

                try
                {
                    fcsAmmoTypeChangedMethod?.Invoke(fcs, new object[] { ammoType });
                    Log($"调用FCS.ammoTypeChanged: {DescribeObject(ammoType)}");
                }
                catch (Exception e)
                {
                    Log($"调用FCS.ammoTypeChanged失败: {e.Message}");
                }
            }

            if (!notifyRoundInBreech)
            {
                return;
            }

            try
            {
                weaponNewRoundInBreechMethod?.Invoke(weapon, new object[] { ammoType });
                Log($"调用Weapon.newRoundInBreech: {DescribeObject(ammoType)}");
            }
            catch (Exception e)
            {
                Log($"调用Weapon.newRoundInBreech失败: {e.Message}");
            }
        }

        private MainGun FindLegacyMainGun(WeaponSystem weapon)
        {
            if (weapon == null)
            {
                return null;
            }

            var candidates = new List<MainGun>();
            var seen = new HashSet<int>();

            void AddCandidates(IEnumerable<MainGun> source)
            {
                if (source == null)
                {
                    return;
                }

                foreach (var candidate in source)
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    int id = candidate.GetInstanceID();
                    if (seen.Add(id))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            AddCandidates(weapon.GetComponents<MainGun>());
            AddCandidates(weapon.GetComponentsInChildren<MainGun>(true));
            AddCandidates(weapon.GetComponentsInParent<MainGun>(true));
            if (weapon.transform != null && weapon.transform.root != null)
            {
                AddCandidates(weapon.transform.root.GetComponentsInChildren<MainGun>(true));
            }

            var readyRack = weapon.Feed?.ReadyRack;
            Log($"FindLegacyMainGun: Weapon={weapon.name}, ReadyRack={(readyRack != null ? readyRack.name : "<null>")}, Candidates={candidates.Count}");

            MainGun fallback = null;
            foreach (var candidate in candidates)
            {
                var candidateRack = mainGunReadyRackField?.GetValue(candidate) as AmmoRack;
                Log($"  Candidate MainGun={candidate.name}, ReadyRack={(candidateRack != null ? candidateRack.name : "<null>")}");

                if (candidateRack != null && readyRack != null && candidateRack == readyRack)
                {
                    Log($"FindLegacyMainGun: 使用ReadyRack匹配到 {candidate.name}");
                    return candidate;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }
            }

            if (fallback != null)
            {
                Log($"FindLegacyMainGun: ReadyRack did not match, falling back to {fallback.name}");
            }

            return fallback;
        }

        private static int GetRackTotal(WeaponState weapon, int rackIndex)
        {
            if (weapon == null || weapon.ammoTypes == null)
            {
                return 0;
            }

            int total = 0;
            foreach (var ammo in weapon.ammoTypes)
            {
                if (ammo?.rackCounts != null && rackIndex >= 0 && rackIndex < ammo.rackCounts.Length)
                {
                    total += ammo.rackCounts[rackIndex];
                }
            }

            return total;
        }

        private static void RebalanceRackCounts(WeaponState weapon, int rackIndex, int changedAmmoIndex, int requestedValue)
        {
            if (weapon == null || weapon.ammoTypes == null || weapon.ammoTypes.Count == 0)
            {
                return;
            }

            int rackCapacity = weapon.ammoTypes[0].rackCapacities[rackIndex];
            int clampedValue = Mathf.Clamp(requestedValue, 0, rackCapacity);
            weapon.ammoTypes[changedAmmoIndex].rackCounts[rackIndex] = clampedValue;

            int overflow = GetRackTotal(weapon, rackIndex) - rackCapacity;
            if (overflow <= 0)
            {
                return;
            }

            for (int ammoIndex = 0; ammoIndex < weapon.ammoTypes.Count && overflow > 0; ammoIndex++)
            {
                if (ammoIndex == changedAmmoIndex)
                {
                    continue;
                }

                int current = weapon.ammoTypes[ammoIndex].rackCounts[rackIndex];
                if (current <= 0)
                {
                    continue;
                }

                int reduction = Math.Min(current, overflow);
                weapon.ammoTypes[ammoIndex].rackCounts[rackIndex] = current - reduction;
                overflow -= reduction;
            }

            if (overflow > 0)
            {
                weapon.ammoTypes[changedAmmoIndex].rackCounts[rackIndex] = Math.Max(0, weapon.ammoTypes[changedAmmoIndex].rackCounts[rackIndex] - overflow);
            }

            EnforceWeaponRackTotalBudget(weapon, rackIndex, changedAmmoIndex);
        }

        private static void FillRackExclusive(WeaponState weapon, int rackIndex, int ammoIndex)
        {
            if (weapon == null || weapon.ammoTypes == null || weapon.ammoTypes.Count == 0)
            {
                return;
            }

            int rackCapacity = weapon.ammoTypes[0].rackCapacities[rackIndex];
            for (int i = 0; i < weapon.ammoTypes.Count; i++)
            {
                weapon.ammoTypes[i].rackCounts[rackIndex] = i == ammoIndex ? rackCapacity : 0;
            }

            EnforceWeaponRackTotalBudget(weapon, rackIndex, ammoIndex);
        }

        private static bool IsOriginalTotalAmmoLimitEnabled()
        {
            return limitTotalAmmoByOriginalVehicleCount != null && limitTotalAmmoByOriginalVehicleCount.Value;
        }

        private static int GetWeaponRackGrandTotal(WeaponState weapon)
        {
            if (weapon == null || weapon.ammoTypes == null)
            {
                return 0;
            }

            int total = 0;
            foreach (var ammo in weapon.ammoTypes)
            {
                if (ammo?.rackCounts == null)
                {
                    continue;
                }

                for (int rackIndex = 0; rackIndex < ammo.rackCounts.Length; rackIndex++)
                {
                    total += Math.Max(0, ammo.rackCounts[rackIndex]);
                }
            }

            return total;
        }

        private static void EnforceWeaponRackTotalBudget(WeaponState weapon, int changedRackIndex, int changedAmmoIndex)
        {
            if (!IsOriginalTotalAmmoLimitEnabled() || weapon == null || weapon.ammoTypes == null || weapon.ammoTypes.Count == 0)
            {
                return;
            }

            int budget = Math.Max(0, weapon.originalRackTotalBudget);
            int overflow = GetWeaponRackGrandTotal(weapon) - budget;
            if (overflow <= 0)
            {
                return;
            }

            for (int rackIndex = 0; rackIndex < weapon.ammoTypes[0].rackCounts.Length && overflow > 0; rackIndex++)
            {
                for (int ammoIndex = 0; ammoIndex < weapon.ammoTypes.Count && overflow > 0; ammoIndex++)
                {
                    if (rackIndex == changedRackIndex && ammoIndex == changedAmmoIndex)
                    {
                        continue;
                    }

                    int current = weapon.ammoTypes[ammoIndex].rackCounts[rackIndex];
                    if (current <= 0)
                    {
                        continue;
                    }

                    int reduction = Math.Min(current, overflow);
                    weapon.ammoTypes[ammoIndex].rackCounts[rackIndex] = current - reduction;
                    overflow -= reduction;
                }
            }

            if (overflow > 0
                && changedAmmoIndex >= 0
                && changedAmmoIndex < weapon.ammoTypes.Count
                && changedRackIndex >= 0
                && changedRackIndex < weapon.ammoTypes[changedAmmoIndex].rackCounts.Length)
            {
                weapon.ammoTypes[changedAmmoIndex].rackCounts[changedRackIndex] = Math.Max(0, weapon.ammoTypes[changedAmmoIndex].rackCounts[changedRackIndex] - overflow);
            }
        }

        private static void TryRefreshSnapshot(GHPC.Weapons.LoadoutManager lm)
        {
            try
            {
                refreshSnapshotMethod?.Invoke(lm, null);
                Log("反射调用RefreshSnapshot()");
            }
            catch (Exception e)
            {
                Log($"RefreshSnapshot调用失败: {e.Message}");
            }
        }

        private static void TryRegisterAllBallistics(GHPC.Weapons.LoadoutManager lm)
        {
            try
            {
                registerBallisticsMethod?.Invoke(lm, null);
            }
            catch (Exception e)
            {
                Log($"RegisterAllBallistics调用失败: {e.Message}");
            }
        }

        private static void TryStartFeed(AmmoFeed feed)
        {
            try
            {
                feedStartMethod?.Invoke(feed, null);
                Log("反射调用Feed.Start()");
            }
            catch (Exception e)
            {
                Log($"Feed.Start调用失败: {e.Message}");
            }
        }

        private static void TrySetFeedStateFlags(AmmoFeed feed, bool reloading, bool forcePauseReload)
        {
            try
            {
                feedReloadingProperty?.SetValue(feed, reloading, null);
            }
            catch (Exception e)
            {
                Log($"设置Feed.Reloading失败: {e.Message}");
            }

            try
            {
                feedForcePauseReloadProperty?.SetValue(feed, forcePauseReload, null);
            }
            catch (Exception e)
            {
                Log($"设置Feed.ForcePauseReload失败: {e.Message}");
            }
        }

        private static bool IsPlanningOrBriefingUiActive()
        {
            return IsRuntimeUiTypeActive(ref planningPhaseUiType, "GHPC.UI.Map.PlanningPhaseUI")
                || IsRuntimeUiTypeActive(ref missionBriefingPanelUiType, "GHPC.UI.Map.MapMissionBriefingPanel");
        }

        private static bool IsRuntimeUiTypeActive(ref Type cachedType, string fullName)
        {
            try
            {
                if (cachedType == null)
                {
                    cachedType = ResolveRuntimeType(fullName);
                }

                if (cachedType == null)
                {
                    return false;
                }

                var instances = Resources.FindObjectsOfTypeAll(cachedType);
                if (instances == null)
                {
                    return false;
                }

                foreach (var instance in instances)
                {
                    if (instance == null)
                    {
                        continue;
                    }

                    var behaviour = instance as Behaviour;
                    if (behaviour != null)
                    {
                        if (behaviour.isActiveAndEnabled && behaviour.gameObject != null && behaviour.gameObject.activeInHierarchy)
                        {
                            return true;
                        }

                        continue;
                    }

                    var gameObject = instance as GameObject;
                    if (gameObject != null && gameObject.activeInHierarchy)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Log($"检测UI状态失败: Type={fullName}, Error={e.Message}");
            }

            return false;
        }

        private static Type ResolveRuntimeType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private void OpenUI(object playerUnit)
        {
            showUI = true;
            currentUnit = playerUnit;
            scrollPosition = Vector2.zero;
            EnterUiInteractionMode();
        }

        private void EnterUiInteractionMode()
        {
            if (uiControlStateCaptured)
            {
                return;
            }

            try
            {
                uiPreviousPlanningOrBriefingUiActive = IsPlanningOrBriefingUiActive();
                uiPreviousPaused = GetCurrentPausedState();
                if (!uiPreviousPaused)
                {
                    TrySetPausedState(true);
                }

                var playerInput = PlayerInput.Instance;
                if (playerInput != null)
                {
                    uiPreviousSuspendCameraInputs = playerInput.SuspendCameraInputs;
                    uiPreviousSuspendGunnerInputs = playerInput.SuspendGunnerInputs;
                    uiPreviousSuspendInterfaceInputs = playerInput.SuspendInterfaceInputs;

                    TryNotifyMenuFocus(playerInput, true);


                    TrySetPlayerInputSuspendState(playerInput, true, true, false);
                }

                TryDetachCamera();


                uiPreviousCursorLockMode = Cursor.lockState;
                uiPreviousCursorVisible = Cursor.visible;
                uiShouldRestorePreviousCursorState = uiPreviousPlanningOrBriefingUiActive;
                Log($"进入UI前光标态: Cursor={uiPreviousCursorLockMode}/{uiPreviousCursorVisible}, SuspendCamera={uiPreviousSuspendCameraInputs}, SuspendGunner={uiPreviousSuspendGunnerInputs}, PlanningOrBriefingActive={uiPreviousPlanningOrBriefingUiActive}, RestorePrevious={uiShouldRestorePreviousCursorState}");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                uiControlStateCaptured = true;
            }
            catch (Exception e)
            {
                MelonLogger.Error(LocalizationManager.Get("log_ui_mode_enter_failed", e.Message));
            }
        }

        private void ExitUiInteractionMode()
        {
            if (!uiControlStateCaptured)
            {
                return;
            }

            try
            {
                var playerInput = PlayerInput.Instance;
                if (playerInput != null)
                {
                    TryNotifyMenuFocus(playerInput, false);


                    TrySetPlayerInputSuspendState(playerInput, uiPreviousSuspendCameraInputs, uiPreviousSuspendGunnerInputs, uiPreviousSuspendInterfaceInputs);
                }

                TrySetPausedState(uiPreviousPaused);

                if (uiShouldRestorePreviousCursorState)
                {
                    Cursor.lockState = uiPreviousCursorLockMode;
                    Cursor.visible = uiPreviousCursorVisible;
                    Log($"退出UI: 恢复原光标态, Cursor={uiPreviousCursorLockMode}/{uiPreviousCursorVisible}");
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    Log("退出UI: 恢复战斗态鼠标 Locked/False");
                }

            }
            catch (Exception e)
            {
                MelonLogger.Error(LocalizationManager.Get("log_ui_mode_exit_failed", e.Message));
            }
            finally
            {
                uiPreviousPlanningOrBriefingUiActive = false;
                uiShouldRestorePreviousCursorState = false;
                uiControlStateCaptured = false;
            }
        }

        private static bool GetCurrentPausedState()
        {
            try
            {
                var value = timeControllerPausedProperty != null ? timeControllerPausedProperty.GetValue(null, null) : null;
                return value is bool paused && paused;
            }
            catch
            {
                return TimeController.Paused;
            }
        }

        private static void TrySetPausedState(bool paused)
        {
            try
            {
                var timeController = TimeController.Instance;
                var method = paused ? timeControllerPauseGameMethod : timeControllerUnpauseGameMethod;
                if (timeController != null && method != null)
                {
                    method.Invoke(timeController, null);
                    Log($"反射调用TimeController.{method.Name}()");
                    return;
                }

                timeControllerPausedProperty?.SetValue(null, paused, null);
                Log($"Fallback: set TimeController.Paused = {paused}");
            }
            catch (Exception e)
            {
                Log($"设置暂停状态失败: {e.Message}");
                try
                {
                    TimeController.Paused = paused;
                }
                catch
                {
                }
            }
        }

        private static void TryNotifyMenuFocus(PlayerInput playerInput, bool takingFocus)
        {
            try
            {
                var method = takingFocus ? playerInputOnMenuTakingFocusMethod : playerInputOnMenuLosingFocusMethod;
                if (playerInput != null && method != null)
                {
                    method.Invoke(playerInput, null);
                    Log($"反射调用PlayerInput.{method.Name}()");
                }
            }
            catch (Exception e)
            {
                Log($"PlayerInput菜单焦点通知失败: {e.Message}");
            }
        }

        private static void TrySetPlayerInputSuspendState(PlayerInput playerInput, bool suspendCameraInputs, bool suspendGunnerInputs, bool suspendInterfaceInputs)
        {
            try
            {
                playerInputSuspendCameraInputsBackingField?.SetValue(playerInput, suspendCameraInputs);
                playerInputSuspendGunnerInputsBackingField?.SetValue(playerInput, suspendGunnerInputs);
                playerInputSuspendInterfaceInputsBackingField?.SetValue(playerInput, suspendInterfaceInputs);
                Log($"反射设置PlayerInput.Suspend*: Camera={suspendCameraInputs}, Gunner={suspendGunnerInputs}, Interface={suspendInterfaceInputs}");
            }
            catch (Exception e)
            {
                Log($"设置PlayerInput.Suspend*失败: {e.Message}");
            }
        }

        private static void TryDetachCamera()
        {
            try
            {
                var cameraManager = GHPC.Camera.CameraManager.Instance;
                if (cameraManager != null && cameraManagerForceDetachCameraMethod != null)
                {
                    cameraManagerForceDetachCameraMethod.Invoke(cameraManager, new object[] { 0f });
                    Log("反射调用CameraManager.ForceDetachCamera(0f)");
                }
            }
            catch (Exception e)
            {
                Log($"ForceDetachCamera失败: {e.Message}");
            }
        }

        private void EnsureGuiStyles()
        {
            if (opaqueWindowStyle == null)
            {
                opaqueWindowStyle = new GUIStyle(GUI.skin.window)
                {
                    normal = { textColor = Color.white },
                    focused = { textColor = Color.white },
                    hover = { textColor = Color.white },
                    active = { textColor = Color.white },
                    padding = new RectOffset(12, 12, 24, 12)
                };
            }

            if (titleLabelStyle == null)
            {
                titleLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    normal = { textColor = Color.white },
                    fontSize = 14
                };
            }
        }

        private void DrawOpaqueWindowBackground()
        {
            var previousColor = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, windowRect.width, windowRect.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static string FormatAmmoDisplayName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return "<UNKNOWN>";
            }

            if (rawName.StartsWith("clip_", StringComparison.OrdinalIgnoreCase))
            {
                return rawName.Substring(5).ToUpperInvariant();
            }

            return rawName;
        }

        private static string DescribeObject(object value)
        {
            if (value == null)
            {
                return "null";
            }

            var type = value.GetType();
            foreach (var memberName in new[] { "name", "Name", "ShortName" })
            {
                var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null)
                {
                    var propertyValue = property.GetValue(value, null);
                    if (propertyValue != null)
                    {
                        return propertyValue.ToString();
                    }
                }

                var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var fieldValue = field.GetValue(value);
                    if (fieldValue != null)
                    {
                        return fieldValue.ToString();
                    }
                }
            }

            return value.ToString();
        }

        private void CloseUI()
        {
            ExitUiInteractionMode();
            showUI = false;
            currentUnit = null;
            weaponStates.Clear();
        }

        // 数据类
        private class WeaponState
        {
            public object weapon;
            public object loadoutManager;
            public string weaponName;
            public List<AmmoTypeState> ammoTypes = new List<AmmoTypeState>();
            public bool usesAutocannonFeedMode = false;
            public int totalLoadedRounds = 0;
            public int selectedChamberedIndex = 0;
            public bool isReloading = false;
            public int originalRackTotalBudget = 0;
        }

        private class AmmoTypeState
        {
            public object ammoClip;
            public AmmoType.AmmoClip ammoClipType;
            public AmmoCodexScriptable ammoCodex;
            public AmmoType ammoType;
            public string typeName;
            public int originalTotal;
            public int currentLoadedCount;
            public int[] rackCounts;
            public int[] rackCapacities;
        }
    }
}
