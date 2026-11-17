using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace CouchCoop.UI
{
    /// <summary>
    /// The mod's UI, fully usable with a controller (TV, Steam Deck):
    ///  - a 3-tab hub (Controllers / Split screen / Options): hold Select+Start (or both stick clicks) for 1 s, or F8;
    ///    in the P2 window the same gesture (P2's controller) opens P2's own options (zoom, frame cap);
    ///  - a connection card (in split screen) and toasts;
    ///  - keyboard shortcuts: F8 hub, F9 start split screen, F10 half/full screen, F6 copy, F7 card, F11 diagnostics.
    /// Drawn with the game's own fonts and UI sprites (see GameUI).
    /// </summary>
    internal class Hub : MonoBehaviour
    {
        private enum Tab { Pads = 0, Duo = 1, Worlds = 2, Options = 3 }

        private const float ComboHoldSeconds = 1f;

        private bool _open, _startupDone, _checkAuto = true, _dismissed, _heartbeat, _showCard = true;
        private Tab _tab;
        private int _cursor;
        private float _comboStart = -1f, _openedAt;
        private readonly Dictionary<string, int> _draft = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _flash = new Dictionary<string, float>();
        private string _toast;
        private float _toastUntil;

        private static Hub _instance;

        private void Awake()
        {
            useGUILayout = false; // no IMGUI layout pass: we only draw with absolute rects
            _instance = this;
        }

        /// <summary>Shows a toast from anywhere (patches, background checks).</summary>
        public static void Notify(string msg, float seconds = 3f) => _instance?.Toast(msg, seconds);

        private void OnEnable() => PadAssignment.PadsChanged += OnPadsChanged;
        private void OnDisable() => PadAssignment.PadsChanged -= OnPadsChanged;

        private void OnPadsChanged()
        {
            _checkAuto = true;
            if (_open) SyncDraft();
            CheckMyPad();
        }

        private bool? _hadMyPad;

        /// <summary>
        /// In split screen, tells this window's player when their controller drops out (Bluetooth, battery) and
        /// when it is back. The same controller is re-assigned by itself when it reconnects.
        /// </summary>
        private void CheckMyPad()
        {
            bool has = PadAssignment.HasConnectedPadForThisWindow();
            bool? had = _hadMyPad;
            _hadMyPad = has;
            if (!Duo.Active || had == null || had == has) return;
            if (!has) Toast(L10n.T(Duo.IsJ2 ? "pads.lostP2" : "pads.lost"), 8f);
            else Toast(L10n.T("pads.back"));
        }

        // ================================================================ loop

        private void Update()
        {
            if (!_heartbeat)
            {
                _heartbeat = true; // proves the Unity loop runs in this window (useful when P2 hangs)
                Debug.Log($"{CouchCoopMod.Tag} UI running (window {(Duo.IsJ2Process ? "P2" : "P1")}, focused={Application.isFocused})");
            }
            WindowLayout.Tick();
            Duo.Tick();
            Diagnostics.Tick();
            Perf.Tick();
            if (!ReInput.isReady) return;

            if (!_startupDone)
            {
                bool ready;
                try { ready = Manager.main != null && Manager.input != null && Manager.input.singleplayerInputModule != null; }
                catch { ready = false; }
                if (!ready) return;
                _startupDone = true;
                Duo.OnGameReady();
                PadAssignment.Apply("startup");
                _hadMyPad = PadAssignment.HasConnectedPadForThisWindow();
            }

            LogButtonPresses();

            if (Duo.IsJ2 && Time.frameCount % 120 == 0 && PadStore.Reload())
            {
                PadAssignment.Apply("P1 changed the controllers");
                CheckMyPad();
            }

            if (_open) { HandleOpen(); return; }

            if (Input.GetKeyDown(KeyCode.F7)) _showCard = !_showCard;
            if (Input.GetKeyDown(KeyCode.F10)) WindowLayout.Toggle();
            if (Input.GetKeyDown(KeyCode.F6)) { Duo.CopyConnectionToClipboard(); Toast(L10n.T("duo.copied")); }
            if (Duo.IsJ2)
            {
                // The P2 window does not manage controllers or start anything: only its own options.
                if (Input.GetKeyDown(KeyCode.F8) || ComboHeld()) Open(Tab.Options);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F8) || ComboHeld()) { Open(Tab.Pads); return; }
            if (Input.GetKeyDown(KeyCode.F9)) { Toast(Duo.Start(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)), 6f); return; }

            if (_checkAuto)
            {
                _checkAuto = false;
                if (Settings.AutoOpenSelector && !_dismissed && PadAssignment.Joysticks.Count >= 2 && PadAssignment.HasUnknownPad())
                    Open(Tab.Pads);
            }
        }

        private bool ComboHeld()
        {
            bool held = false;
            foreach (var j in PadAssignment.Joysticks)
            {
                var gp = j.GetTemplate<IGamepadTemplate>();
                if (!IsMine(j)) continue; // P2's combo must not open P1's menu, and the other way round
                if (gp == null) continue;
                // Select + Start, or both stick clicks (some Xbox controllers over Bluetooth never report View).
                if ((gp.center1.value && gp.center2.value) || (gp.leftStick.press.value && gp.rightStick.press.value)) { held = true; break; }
            }
            if (!held) { _comboStart = -1f; return false; }
            if (_comboStart < 0f) _comboStart = Time.unscaledTime;
            return Time.unscaledTime - _comboStart >= ComboHoldSeconds;
        }

        private static int _padLogLines;
        private const int MaxPadLogLines = 80;

        /// <summary>
        /// Logs each button press of every controller (index, raw name, how Select / Start are read, whether this
        /// window listens to it), up to 80 lines per launch. To diagnose a combo that does not open the hub.
        /// </summary>
        private void LogButtonPresses()
        {
            if (_padLogLines >= MaxPadLogLines) return;
            foreach (var j in PadAssignment.Joysticks)
            {
                if (!j.GetAnyButtonDown()) continue;
                string pressed = "";
                try
                {
                    for (int i = 0; i < j.buttonCount; i++)
                        if (j.GetButtonDown(i)) pressed += $" [{i}:{j.ButtonElementIdentifiers[i].name}]";
                }
                catch { }
                var gp = j.GetTemplate<IGamepadTemplate>();
                string key = PadAssignment.KeyOf(j);
                Debug.Log($"{CouchCoopMod.Tag} [pads] {Time.unscaledTime:0.00} {j.name} slot={PadStore.GetSlot(key)} mine={IsMine(j)} hub={(_open ? "open" : "closed")} "
                    + $"template={(gp != null ? "gamepad" : "none")} select={(gp != null && gp.center1.value)} start={(gp != null && gp.center2.value)} "
                    + $"L3={(gp != null && gp.leftStick.press.value)} R3={(gp != null && gp.rightStick.press.value)} down:{pressed}");
                if (++_padLogLines >= MaxPadLogLines) return;
            }
        }

        /// <summary>Controllers this window listens to outside the Controllers tab (P1 window: all but P2's).</summary>
        private static bool IsMine(Joystick j)
        {
            int slot = PadStore.GetSlot(PadAssignment.KeyOf(j));
            return Duo.IsJ2 ? slot == 2 : slot != 2;
        }

        private Tab[] Tabs => Duo.IsJ2 ? new[] { Tab.Options } : new[] { Tab.Pads, Tab.Duo, Tab.Worlds, Tab.Options };

        // ================================================================ open / close

        private void Open(Tab tab)
        {
            _open = true;
            _tab = tab;
            _cursor = 0;
            _comboStart = -1f;
            _openedAt = Time.unscaledTime;
            _draft.Clear();
            SyncDraft();
            PadAssignment.Suspended = true;
            PadAssignment.Apply("hub opened");
        }

        private void SyncDraft()
        {
            foreach (var j in PadAssignment.Joysticks)
            {
                string key = PadAssignment.KeyOf(j);
                if (_draft.ContainsKey(key)) continue;
                int slot = Mathf.Max(0, PadStore.GetSlot(key));
                if (slot != 0 && Holder(slot) != null) slot = 0; // one controller per player
                _draft[key] = slot;
            }
        }

        private void CloseAndSave()
        {
            if (Duo.IsJ2) { Cancel(); return; } // P2 only changes its options (saved right away), never the controllers
            foreach (var j in PadAssignment.Joysticks)
            {
                string key = PadAssignment.KeyOf(j);
                if (_draft.TryGetValue(key, out int slot)) PadStore.SetSlot(key, j.name, slot);
            }
            PadStore.Save();
            _open = false;
            PadAssignment.Suspended = false;
            PadAssignment.Apply("hub closed");
            Toast(L10n.T("pads.saved"));
        }

        private void Cancel()
        {
            if (!Duo.IsJ2) _dismissed = true;
            _open = false;
            PadAssignment.Suspended = false;
            if (Duo.IsJ2 || PadAssignment.HasConnectedPadForThisWindow()) PadAssignment.Apply("hub cancelled");
            else PadAssignment.RestoreGameDefault();
        }

        // ================================================================ hub input

        private void HandleOpen()
        {
            if (Time.unscaledTime - _openedAt < 0.25f) return; // the opening combo must not validate right away

            if (_editing) { HandleRename(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F8)) { Cancel(); return; }

            int tabDelta = 0, dy = 0, dx = 0;
            bool accept = false, back = false, close = false;
            if (Input.GetKeyDown(KeyCode.Q)) tabDelta = -1;
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Tab)) tabDelta = 1;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = 1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) dy = -1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) accept = true;
            if (Input.GetKeyDown(KeyCode.Backspace)) back = true;

            foreach (var j in PadAssignment.Joysticks)
            {
                var f = PadReader.Read(j);
                if (!f.Any) continue;
                if (_tab != Tab.Pads && !IsMine(j)) continue; // the other player's controller does not drive this menu
                string key = PadAssignment.KeyOf(j);
                _flash[key] = Time.unscaledTime;
                if (f.lb) tabDelta = -1;
                if (f.rb) tabDelta = 1;
                if (f.start) close = true;
                if (f.select) { Cancel(); return; }

                if (_tab == Tab.Pads)
                {
                    // Controllers tab: each controller moves itself.
                    if (!_draft.ContainsKey(key)) _draft[key] = 0;
                    if (f.b && _draft[key] != 0) { _draft[key] = 0; PadReader.Buzz(j, 0.3f); }
                    else if (f.dx != 0) MovePad(j, key, f.dx);
                    if (f.a) close = true;
                }
                else
                {
                    if (f.dy != 0) dy = f.dy;
                    if (f.dx != 0) dx = f.dx;
                    if (f.a) accept = true;
                    if (f.b) back = true;
                }
            }

            if (tabDelta != 0)
            {
                var tabs = Tabs;
                int i = System.Array.IndexOf(tabs, _tab);
                _tab = tabs[((i < 0 ? 0 : i) + tabDelta + tabs.Length) % tabs.Length];
                _cursor = 0;
                return;
            }
            if (close) { CloseAndSave(); return; }

            if (_tab == Tab.Pads) { if (accept) CloseAndSave(); return; }
            if (back) { CloseAndSave(); return; }

            int count = ListRows(_tab).Count;
            if (dy != 0) { _cursor = (_cursor - dy + count) % count; _confirmRow = -1; } // up = previous item
            if (_tab == Tab.Duo && (accept || dx != 0)) RunDuoAction(_cursor, accept, dx);
            if (_tab == Tab.Worlds && (accept || dx != 0)) RunWorldAction(_cursor, accept, dx);
            if (_tab == Tab.Options && (accept || dx != 0)) OptionRows()[_cursor].Change?.Invoke(dx == 0 ? 1 : dx);
        }

        private void MovePad(Joystick j, string key, int dir)
        {
            int cur = _draft[key];
            int target = cur == 0 ? (dir < 0 ? 1 : 2) : (cur == 1 && dir > 0) || (cur == 2 && dir < 0) ? 0 : cur;
            if (target == cur) return;
            if (target != 0 && Holder(target) != null)
            {
                PadReader.Buzz(j, 0.8f);
                Toast(L10n.T(target == 1 ? "pads.taken1" : "pads.taken2"));
                return;
            }
            _draft[key] = target;
            PadReader.Buzz(j, 0.4f);
        }

        private string Holder(int slot)
        {
            foreach (var kv in _draft) if (kv.Value == slot) return kv.Key;
            return null;
        }

        private int _confirmRow = -1;
        private float _confirmUntil;

        /// <summary>Actions that replace or stop something need A twice (within 4 s).</summary>
        private bool Confirm(int row)
        {
            if (_confirmRow == row && Time.unscaledTime < _confirmUntil) { _confirmRow = -1; return true; }
            _confirmRow = row;
            _confirmUntil = Time.unscaledTime + 4f;
            Toast(L10n.T("confirm.again"), 4f);
            return false;
        }

        private bool Confirming(int row) => _confirmRow == row && Time.unscaledTime < _confirmUntil;

        private void RunDuoAction(int index, bool accept, int dx)
        {
            switch (index)
            {
                case 0: CloseAndSave(); Toast(Duo.Start(restartP2: Duo.Active), 6f); break;
                case 1: WindowLayout.Toggle(); break;
                case 2: Duo.CopyConnectionToClipboard(); Toast(L10n.T("duo.copied")); break;
                case 3:
                    if (accept && Duo.Active && Confirm(3)) { CloseAndSave(); Toast(Duo.Stop(), 6f); }
                    break;
            }
        }

        // ---------------- Worlds tab ----------------

        private static readonly WorldMode[] Modes = { WorldMode.Normal, WorldMode.Hard, WorldMode.Casual, WorldMode.Creative };
        private int _newMode;
        private bool _editing;
        private string _editText = "";

        private void RunWorldAction(int index, bool accept, int dx)
        {
            bool idle = ServerWorld.NoServerRunning;
            switch (index)
            {
                case 0: // server world used by split screen
                    if (dx != 0 || accept) Toast(ServerWorld.Select(dx == 0 ? 1 : dx), 5f);
                    break;
                case 1: // rename (keyboard)
                    if (accept) { _editing = true; _editText = ServerWorld.CurrentName; Toast(L10n.T("world.typeName"), 6f); }
                    break;
                case 2: // new world, mode picked with left / right
                    if (dx != 0) { _newMode = (_newMode + dx + Modes.Length) % Modes.Length; _confirmRow = -1; }
                    else if (accept && idle && Confirm(102)) Toast(ServerWorld.CreateNew(Modes[_newMode]), 8f);
                    break;
                case 3: // bring back to solo
                    if (accept && idle && Confirm(103)) Toast(ServerWorld.BringBack(), 8f);
                    break;
                case 4: // add a solo world to the server
                    if (dx != 0) { Settings.SourceWorld = ServerWorld.NextSolo(Settings.SourceWorld, dx); ServerWorld.Invalidate(); _confirmRow = -1; }
                    else if (accept && idle && Confirm(104)) Toast(ServerWorld.AddFromSolo(Settings.SourceWorld), 8f);
                    break;
            }
        }

        /// <summary>Rename row: typed on the keyboard. Enter / A saves, Esc / B cancels.</summary>
        private void HandleRename()
        {
            bool save = false, cancel = Input.GetKeyDown(KeyCode.Escape);
            foreach (char c in Input.inputString)
            {
                if (c == '\b') { if (_editText.Length > 0) _editText = _editText.Substring(0, _editText.Length - 1); }
                else if (c == '\n' || c == '\r') save = true;
                else if (!char.IsControl(c) && c != '"' && _editText.Length < ServerWorld.MaxNameLength) _editText += c;
            }
            foreach (var j in PadAssignment.Joysticks)
            {
                if (!IsMine(j)) continue;
                var f = PadReader.Read(j);
                if (f.a) save = true;
                if (f.b || f.select) cancel = true;
            }
            if (cancel) { _editing = false; return; }
            if (!save) return;
            _editing = false;
            Toast(ServerWorld.Rename(_editText), 5f);
        }

        private void Toast(string msg, float seconds = 3f)
        {
            if (string.IsNullOrEmpty(msg)) return;
            _toast = msg;
            _toastUntil = Time.unscaledTime + seconds;
        }

        // ================================================================ drawing (sizes in game pixels × u)

        private void OnGUI()
        {
            int u = GameUI.Px;
            if (Duo.Active && _showCard) DrawCard(u);
            if (!string.IsNullOrEmpty(_toast) && Time.unscaledTime < _toastUntil) DrawToast(u);
            if (_open) DrawHub(u);
        }

        private float _cardWaitSince = -1f;
        private const float CardMaxWait = 15f;

        /// <summary>
        /// Connection card (top left). Sized to its content so no text ever overflows.
        /// It waits for the game's UI sprites and fonts so it appears directly in the game's style;
        /// after 15 s without them it shows the fallback frame so the join info is never missing.
        /// </summary>
        private void DrawCard(int u)
        {
            if (!GameUI.IsReady(GameUI.Frame.Box))
            {
                if (_cardWaitSince < 0f) _cardWaitSince = Time.unscaledTime;
                if (Time.unscaledTime - _cardWaitSince < CardMaxWait) return;
            }

            var c = Duo.ConnectionInfo;
            bool hasInfo = c != null && !string.IsNullOrEmpty(c.password);

            string status, detail; Color dot;
            if (Duo.IsJ2)
            {
                status = L10n.T(hasInfo ? "card.p2.ready" : "status.waitInfo");
                detail = hasInfo ? L10n.T("duo.howto") : null;
                dot = hasInfo ? GameUI.Ok : GameUI.Warn;
            }
            else if (Duo.ServerReady) { status = L10n.T("status.ready"); detail = L10n.T("duo.howto"); dot = GameUI.Ok; }
            else if (Duo.ServerRunning) { status = L10n.T(Duo.J2Pending ? "status.p2wait" : "status.starting"); detail = null; dot = GameUI.Warn; }
            else { status = L10n.T("status.stopped"); detail = null; dot = GameUI.Bad; }

            string address = hasInfo ? $"{c.ip}:{c.port}  {c.password}" : null;
            string keys = "F6 " + L10n.T("card.copy") + "   F7 " + L10n.T("card.hide");

            // Lines: [badge + dot + status], detail, address, keys
            float pad = 7 * u, lineL = GameUI.LineHeight(GameUI.Font.Large), lineS = GameUI.LineHeight(GameUI.Font.Small) + 3 * u;
            float headerW = 20 * u + 10 * u + GameUI.Width(status, GameUI.Font.Medium);
            float contentW = Mathf.Max(headerW, GameUI.Width(detail, GameUI.Font.Small), GameUI.Width(address, GameUI.Font.Small), GameUI.Width(keys, GameUI.Font.Small));
            float w = Mathf.Min(Screen.width - 8 * u, contentW + 2 * pad + 2 * u);
            float h = pad + lineL + 4 * u + (detail != null ? lineS : 0) + (address != null ? lineS : 0) + lineS + pad - 3 * u;
            var r = new Rect(4 * u, 4 * u, Mathf.Round(w), Mathf.Round(h));
            GameUI.Draw(GameUI.Frame.Box, r);

            float x = r.x + pad, y = r.y + pad, inner = r.width - 2 * pad;
            GameUI.Text(new Rect(x, y, 20 * u, lineL), L10n.T(Duo.IsJ2 ? "pads.p2" : "pads.p1"), GameUI.Font.Large, Duo.IsJ2 ? GameUI.P2 : GameUI.P1);
            GameUI.Draw(GameUI.Frame.Box, new Rect(x + 20 * u, y + (lineL - 5 * u) / 2f, 5 * u, 5 * u), dot);
            GameUI.Text(new Rect(x + 30 * u, y, inner - 30 * u, lineL), status, GameUI.Font.Medium, GameUI.White);
            y += lineL + 4 * u;
            if (detail != null) { GameUI.Text(new Rect(x, y, inner, lineS), detail, GameUI.Font.Small, GameUI.Gold); y += lineS; }
            if (address != null) { GameUI.Text(new Rect(x, y, inner, lineS), address, GameUI.Font.Small, GameUI.Dim); y += lineS; }
            GameUI.Text(new Rect(x, y, inner, lineS), keys, GameUI.Font.Small, GameUI.Dim);
        }

        private void DrawToast(int u)
        {
            float w = Mathf.Min(Screen.width - 8 * u, 260 * u), h = 24 * u;
            var r = new Rect((Screen.width - w) / 2f, Screen.height - h - 8 * u, w, h);
            GameUI.Draw(GameUI.Frame.Window, r);
            GameUI.Text(new Rect(r.x + 10 * u, r.y, r.width - 20 * u, r.height), _toast, GameUI.Font.Medium, GameUI.White, TextAnchor.MiddleCenter);
        }

        private void DrawHub(int u)
        {
            var pads = PadAssignment.Joysticks;
            float rowH = 18 * u, gap = 2 * u, pad = 10 * u;
            int rows = _tab == Tab.Pads ? Mathf.Max(1, pads.Count) : ListRows(_tab).Count;
            float bodyH = (_tab == Tab.Pads ? 16 * u : 0) + rows * (rowH + gap) + (_tab == Tab.Pads ? 12 * u : 0);
            float w = Mathf.Min(Screen.width - 8 * u, 360 * u);
            float h = Mathf.Min(Screen.height - 8 * u, pad + 16 * u + 6 * u + 18 * u + 8 * u + bodyH + 16 * u + pad);
            var r = new Rect(Mathf.Round((Screen.width - w) / 2f), Mathf.Round((Screen.height - h) / 2f), w, h);
            GameUI.Draw(GameUI.Frame.Window, r);

            float x = r.x + pad, inner = r.width - 2 * pad, y = r.y + pad;
            GameUI.Text(new Rect(x, y, inner, 16 * u), L10n.T("title"), GameUI.Font.Large, GameUI.Gold, TextAnchor.MiddleCenter);
            y += 16 * u + 6 * u;

            // Tabs: the selected one uses the hotbar slot (wood), the others the tooltip box.
            var tabIds = Tabs;
            float tw = (inner - (tabIds.Length - 1) * gap) / tabIds.Length;
            for (int i = 0; i < tabIds.Length; i++)
            {
                var tr = new Rect(x + i * (tw + gap), y, tw, 18 * u);
                bool sel = _tab == tabIds[i];
                string label = tabIds[i] == Tab.Pads ? L10n.T("tab.pads") : tabIds[i] == Tab.Duo ? L10n.T("tab.duo")
                    : tabIds[i] == Tab.Worlds ? L10n.T("tab.worlds")
                    : Duo.IsJ2 ? L10n.T("tab.optionsP2") : L10n.T("tab.options");
                GameUI.Draw(sel ? GameUI.Frame.Slot : GameUI.Frame.Box, tr, sel ? (Color?)null : new Color(0.7f, 0.7f, 0.75f));
                GameUI.Text(tr, label, GameUI.Font.Medium, sel ? GameUI.White : GameUI.Dim, TextAnchor.MiddleCenter);
            }
            y += 18 * u + 8 * u;

            var body = new Rect(x, y, inner, bodyH);
            if (_tab == Tab.Pads) DrawPads(body, u, rowH, gap, pads);
            else DrawList(body, rowH, gap, ListRows(_tab));

            string hint = _editing ? L10n.T("hint.rename")
                : (Tabs.Length > 1 ? L10n.T("hint.tabs") + "    " : "") + L10n.T(_tab == Tab.Pads ? "hint.pads" : "hint.list");
            GameUI.Text(new Rect(x, r.yMax - pad - 12 * u, inner, 12 * u), hint, GameUI.Font.Small, GameUI.Dim, TextAnchor.MiddleCenter);
        }

        private void DrawPads(Rect body, int u, float rowH, float gap, IList<Joystick> pads)
        {
            float colW = body.width / 3f;
            GameUI.Text(new Rect(body.x, body.y, colW, 14 * u), L10n.T("pads.p1"), GameUI.Font.Large, GameUI.P1, TextAnchor.MiddleCenter);
            GameUI.Text(new Rect(body.x + colW, body.y, colW, 14 * u), L10n.T("pads.none"), GameUI.Font.Small, GameUI.Dim, TextAnchor.MiddleCenter);
            GameUI.Text(new Rect(body.x + 2 * colW, body.y, colW, 14 * u), L10n.T("pads.p2"), GameUI.Font.Large, GameUI.P2, TextAnchor.MiddleCenter);

            float y = body.y + 16 * u;
            if (pads.Count == 0)
            {
                GameUI.Text(new Rect(body.x, y, body.width, rowH), L10n.T("pads.empty"), GameUI.Font.Medium, GameUI.Dim, TextAnchor.MiddleCenter);
                return;
            }
            for (int i = 0; i < pads.Count; i++)
            {
                var j = pads[i];
                string key = PadAssignment.KeyOf(j);
                _draft.TryGetValue(key, out int slot);
                int col = slot == 1 ? 0 : slot == 2 ? 2 : 1;
                var box = new Rect(body.x + col * colW + gap, y + i * (rowH + gap), colW - 2 * gap, rowH);
                bool flash = _flash.TryGetValue(key, out float ft) && Time.unscaledTime - ft < 0.2f;
                Color tint = slot == 1 ? new Color(0.75f, 0.85f, 1f) : slot == 2 ? new Color(1f, 0.62f, 0.55f) : new Color(0.62f, 0.62f, 0.66f);
                GameUI.Draw(slot == 0 ? GameUI.Frame.Box : GameUI.Frame.Box, box, flash ? Color.white : tint);
                string l = slot == 0 || slot == 2 ? "< " : "  ";
                string rr = slot == 0 || slot == 1 ? " >" : "  ";
                GameUI.Text(box, $"{l}{i + 1}. {Short(j.name)}{rr}", GameUI.Font.Medium, GameUI.White, TextAnchor.MiddleCenter);
            }
            if (Holder(2) != null)
                GameUI.Text(new Rect(body.x, y + pads.Count * (rowH + gap), body.width, 10 * u), L10n.T("pads.p2note"), GameUI.Font.Small, GameUI.Dim, TextAnchor.MiddleCenter);
        }

        private struct Row
        {
            public string Label, Value;
            public bool Enabled;
            public bool? Check; // non-null: draw the game's check box instead of a text value
            public System.Action<int> Change; // called with -1 / +1 (left / right, A = +1)
        }

        private static readonly int[] FpsSteps = { 0, 30, 45, 60, 90, 120 };

        private static int NextFps(int cur, int dir)
        {
            int i = System.Array.IndexOf(FpsSteps, cur);
            if (i < 0) i = 3;
            return FpsSteps[(i + dir + FpsSteps.Length) % FpsSteps.Length];
        }

        private Row ZoomRow() => new Row
        {
            Label = L10n.T("opt.scale"),
            Value = Settings.MyScale == 0 ? $"< {L10n.T("opt.auto")} (x{Settings.PixelScaleFor(Screen.height)}) >" : $"< x{Settings.MyScale} >",
            Enabled = true,
            Change = dir => Settings.MyScale = (Settings.MyScale + dir + Settings.MaxScale + 1) % (Settings.MaxScale + 1),
        };

        private Row FpsRow() => new Row
        {
            Label = L10n.T("opt.fps"),
            Value = Settings.FpsLimit == 0 ? $"< {L10n.T("opt.fps.game")} >" : $"< {Settings.FpsLimit} >",
            Enabled = true,
            Change = dir => Settings.FpsLimit = NextFps(Settings.FpsLimit, dir),
        };

        private List<Row> ListRows(Tab tab) => tab == Tab.Duo ? DuoRows() : tab == Tab.Worlds ? WorldRows() : OptionRows();

        private List<Row> DuoRows() => new List<Row>
        {
            new Row { Label = L10n.T(Duo.Active ? "duo.restartP2" : "duo.start"), Value = "F9", Enabled = Duo.ServerInstalled },
            new Row { Label = L10n.T("duo.layout"), Value = "F10", Enabled = true },
            new Row { Label = L10n.T("duo.copy"), Value = "F6", Enabled = Duo.ConnectionInfo != null },
            new Row { Label = L10n.T("duo.stop"), Value = Confirming(3) ? L10n.T("confirm.press") : "", Enabled = Duo.Active },
        };

        private List<Row> WorldRows()
        {
            bool idle = ServerWorld.NoServerRunning;
            var v = ServerWorld.View; // cached: building it reads up to 60 files
            string confirm = L10n.T("confirm.press");
            string running = L10n.T("world.stopFirst.short");

            string current = $"< {v.Server.Index}. {Short(v.Server.Name)}{(v.Server.Generated ? " *" : "")} >";

            string backValue;
            if (!idle) backValue = running;
            else if (!v.WorldGenerated) backValue = L10n.T("world.notGenerated");
            else if (v.BackTarget < 0) backValue = L10n.T("world.noFreeSlot.short");
            else backValue = v.BackSame ? string.Format(L10n.T("world.backSame"), Short(v.BackName ?? "?"))
                : string.Format(L10n.T("world.backNew"), v.BackTarget);

            string addValue = v.HasSolo ? $"< {v.Solo.Index}. {Short(v.Solo.Name)} >" : $"< {Settings.SourceWorld}. - >";

            return new List<Row>
            {
                new Row { Label = L10n.T("world.current"), Value = idle ? current : running, Enabled = idle },
                new Row { Label = L10n.T("world.rename"), Value = _editing ? _editText + "_" : Short(v.Server.Name), Enabled = true },
                new Row
                {
                    Label = L10n.T("world.new"), Enabled = idle,
                    Value = Confirming(102) ? confirm : $"< {L10n.T("mode." + Modes[_newMode].ToString().ToLowerInvariant())} >",
                },
                new Row
                {
                    Label = string.Format(L10n.T("world.bringBack"), Short(v.Server.Name)),
                    Value = Confirming(103) ? confirm : backValue, Enabled = idle && v.WorldGenerated,
                },
                new Row { Label = L10n.T("world.add"), Value = Confirming(104) ? confirm : addValue, Enabled = idle },
            };
        }

        private List<Row> OptionRows()
        {
            if (Duo.IsJ2) return new List<Row> { ZoomRow(), FpsRow() };

            return new List<Row>
            {
                new Row
                {
                    Label = L10n.T("opt.layout"), Value = L10n.T(Settings.SideBySide ? "opt.layout.side" : "opt.layout.stacked"), Enabled = true,
                    Change = _ =>
                    {
                        Settings.SideBySide = !Settings.SideBySide;
                        if (Duo.Active && WindowLayout.IsSplit(WindowLayout.Current)) WindowLayout.Request(WindowLayout.DuoSide(Duo.IsJ2), 0f);
                    },
                },
                new Row { Label = L10n.T("opt.fill"), Check = Settings.FillWindow, Enabled = true, Change = _ => Settings.FillWindow = !Settings.FillWindow },
                new Row { Label = L10n.T("opt.music"), Check = Settings.MuteJ2Music, Enabled = true, Change = _ => { Settings.MuteJ2Music = !Settings.MuteJ2Music; Toast(L10n.T("opt.restart")); } },
                new Row { Label = L10n.T("opt.autofill"), Check = Settings.AutoFillJoin, Enabled = true, Change = _ => Settings.AutoFillJoin = !Settings.AutoFillJoin },
                new Row { Label = L10n.T("opt.autoopen"), Check = Settings.AutoOpenSelector, Enabled = true, Change = _ => Settings.AutoOpenSelector = !Settings.AutoOpenSelector },
                ZoomRow(),
                FpsRow(),
            };
        }

        private void DrawList(Rect body, float rowH, float gap, List<Row> rows)
        {
            int u = GameUI.Px;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = new Rect(body.x, body.y + i * (rowH + gap), body.width, rowH);
                bool sel = i == _cursor;
                GameUI.Draw(sel ? GameUI.Frame.Slot : GameUI.Frame.Box, row, sel ? (Color?)null : new Color(0.78f, 0.8f, 0.86f));
                Color fg = rows[i].Enabled ? GameUI.White : GameUI.Dim;
                GameUI.Text(new Rect(row.x + 8 * u, row.y, row.width * 0.68f, row.height), rows[i].Label, GameUI.Font.Medium, fg);
                if (rows[i].Check.HasValue)
                {
                    var cb = new Rect(row.xMax - 8 * u - 12 * u, row.y + (row.height - 12 * u) / 2f, 12 * u, 12 * u);
                    GameUI.Draw(rows[i].Check.Value ? GameUI.Frame.CheckOn : GameUI.Frame.CheckOff, cb);
                }
                else
                {
                    GameUI.Text(new Rect(row.x + row.width * 0.5f, row.y, row.width * 0.5f - 8 * u, row.height), rows[i].Value, GameUI.Font.Medium, sel ? GameUI.Gold : GameUI.Dim, TextAnchor.MiddleRight);
                }
            }
        }

        private static string Short(string n)
        {
            if (string.IsNullOrEmpty(n)) return "Controller";
            return n.Length > 22 ? n.Substring(0, 21) + "." : n;
        }
    }
}
