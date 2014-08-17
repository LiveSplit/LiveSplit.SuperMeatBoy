//#define GAME_TIME

using LiveSplit.GrooveCity;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;
using LiveSplit.UI.Components;
using LiveSplit.Web;
using LiveSplit.Web.Share;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;

namespace LiveSplit.UI.Components
{
    class Component : IComponent
    {
        public ComponentSettings Settings { get; set; }

        public string ComponentName
        {
            get { return "Super Meat Boy Auto Splitter"; }
        }

        public float PaddingBottom { get { return 0; } }
        public float PaddingTop { get { return 0; } }
        public float PaddingLeft { get { return 0; } }
        public float PaddingRight { get { return 0; } }

        public bool Refresh { get; set; }

        public IDictionary<string, Action> ContextMenuControls { get; protected set; }

        public Process Game { get; set; }

        protected static readonly DeepPointer AliveTimer = new DeepPointer("supermeatboy.exe", 0x2D4C14, 0x37C);
        protected static readonly DeepPointer LevelTimer = new DeepPointer("SuperMeatBoy.exe", 0x2D63DC, 0x28, 0x0, 0x728);
        protected static readonly DeepPointer LevelID = new DeepPointer("SuperMeatBoy.exe", 0x2D5588);
        protected static readonly DeepPointer ChapterID = new DeepPointer("supermeatboy.exe", 0x2D54BC, 0x1A8);
        protected static readonly DeepPointer ChapterSelectID = new DeepPointer("SuperMeatBoy.exe", 0x2D5EA0, 0x380);
        protected static readonly DeepPointer CreditsPlaying = new DeepPointer("SuperMeatBoy.exe", 0x2D5EB4);
        protected static readonly DeepPointer TitleScreenShowing = new DeepPointer("SuperMeatBoy.exe", 0x2D5F10);
        protected static readonly DeepPointer IsNotAtLevelEnd = new DeepPointer("SuperMeatBoy.exe", 0x1B6638);
        protected static readonly DeepPointer IsInALevel = new DeepPointer("SuperMeatBoy.exe", 0x2D5484);
        protected static readonly DeepPointer LevelType = new DeepPointer("SuperMeatBoy.exe", 0x2D54BC, 0x1ac);
        protected static readonly DeepPointer IsAtLevelSelect = new DeepPointer("SuperMeatBoy.exe", 0x2D5EA0, 0x37c);

        public TimeSpan GameTime { get; set; }

        //public bool WasTimeRunning { get; set; }
        //public TimeSpan? OldAliveTime { get; set; }
        public TimeSpan? OldLevelTime { get; set; }
        public TimeSpan FinishedLevelTime { get; set; }
        //public int OldDeathCounter { get; set; }
        public int OldLevelID { get; set; }
        public int OldChapterSelectID { get; set; }
        public bool WasAtLevelEnd { get; set; }
        public bool WasAtLevelSelect { get; set; }
        public bool OldCreditsPlaying { get; set; }
        public bool OldTitleScreenShowing { get; set; }

        protected TimerModel Model { get; set; }

        public Component()
        {
            Settings = new ComponentSettings();

            //ContextMenuControls = new Dictionary<String, Action>();
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            if (Game == null || Game.HasExited)
            {
                Game = null;
                var process = Process.GetProcessesByName("SuperMeatBoy").FirstOrDefault();
                if (process != null)
                {
                    Game = process;
                }
            }

#if GAME_TIME
            if (!state.Run.CustomComparisons.Contains("Game Time"))
                state.Run.CustomComparisons.Add("Game Time");
#endif

            if (Model == null)
            {
                Model = new TimerModel() { CurrentState = state };
                state.OnStart += state_OnStart;
            }

            if (Game != null)
            {
                float time;
                TimeSpan levelTime = OldLevelTime ?? TimeSpan.Zero;

                bool isInALevel;
                IsInALevel.Deref<bool>(Game, out isInALevel);
                if (isInALevel)
                {
                    try
                    {
                        LevelTimer.Deref<float>(Game, out time);
                        levelTime = TimeSpan.FromSeconds(time);
                    }
                    catch { }
                }

                //AliveTimer.Deref<float>(Game, out time);
                //var aliveTime = TimeSpan.FromSeconds(time);

                //int deathCounter;
                //DeathCounter.Deref<int>(Game, out deathCounter);

                int levelID;
                LevelID.Deref<int>(Game, out levelID);

                int chapterID;
                ChapterID.Deref<int>(Game, out chapterID);

                int levelType;
                LevelType.Deref<int>(Game, out levelType);

                int chapterSelectID;
                ChapterSelectID.Deref<int>(Game, out chapterSelectID);

                bool creditsPlaying;
                CreditsPlaying.Deref<bool>(Game, out creditsPlaying);

                bool isAtLevelSelect;
                IsAtLevelSelect.Deref<bool>(Game, out isAtLevelSelect);

                bool isNotAtLevelEnd;
                IsNotAtLevelEnd.Deref<bool>(Game, out isNotAtLevelEnd);
                if (!WasAtLevelEnd && !isNotAtLevelEnd)
                {
                    WasAtLevelEnd = true;
                    FinishedLevelTime = levelTime;
                }

                bool titleScreenShowing;
                bool couldFetch = TitleScreenShowing.Deref<bool>(Game, out titleScreenShowing);
                if (!couldFetch)
                    titleScreenShowing = true;

                //bool isTimeRunning = OldLevelTime != levelTime;

                if (OldLevelTime != null)// && OldLevelTime != null)
                {
                    if (state.CurrentPhase == TimerPhase.NotRunning && !titleScreenShowing && OldTitleScreenShowing)
                    {
                        Model.Start();
                    }
                    else if (state.CurrentPhase == TimerPhase.Running)
                    {
                        if (chapterID == 6 && levelID == 99)
                        {
                            if (creditsPlaying && !OldCreditsPlaying)
                                Model.Split(); //The End
                        }
                        else
                        {
                            if (levelType != 0 || chapterID == 7)
                            {
                                if (((levelType <= 1) ? (levelID == 20) : true) && !isInALevel && !isAtLevelSelect && WasAtLevelSelect)
                                    Model.Split(); //Dark World or Cotton Alley
                            }
                            else if (!creditsPlaying && OldCreditsPlaying)
                                Model.Split(); //Chapter Splits
                        }

                        if (titleScreenShowing && !creditsPlaying)
                            Model.Reset();
                    }
                    if (OldLevelTime > levelTime)
                    {
                        //OldDeathCounter = deathCounter;
                        GameTime += (WasAtLevelEnd ? FinishedLevelTime : OldLevelTime ?? TimeSpan.Zero);
                        WasAtLevelEnd = false;
                    }
                }

#if GAME_TIME
                state.IsLoading = true;
                var currentGameTime = GameTime + (WasAtLevelEnd ? FinishedLevelTime : levelTime);
                state.CurrentGameTime = currentGameTime < TimeSpan.Zero ? TimeSpan.Zero : currentGameTime;
#endif

                //WasTimeRunning = isTimeRunning;
                //OldLevelTime = levelTime;
                OldLevelTime = levelTime;
                OldLevelID = levelID;
                OldChapterSelectID = chapterSelectID;
                OldCreditsPlaying = creditsPlaying;
                WasAtLevelSelect = isAtLevelSelect;
                OldTitleScreenShowing = titleScreenShowing;
            }
        }

        void state_OnStart(object sender, EventArgs e)
        {
            GameTime = TimeSpan.Zero - (OldLevelTime ?? TimeSpan.Zero);
            //OldLevelTime = null;
            OldLevelTime = null;
            OldChapterSelectID = 0;
            WasAtLevelEnd = false;
        }

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
        }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
        }

        public float VerticalHeight
        {
            get { return 0; }
        }

        public float MinimumWidth
        {
            get { return 0; }
        }

        public float HorizontalWidth
        {
            get { return 0; }
        }

        public float MinimumHeight
        {
            get { return 0; }
        }

        public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document)
        {
            return document.CreateElement("x");
        }

        public System.Windows.Forms.Control GetSettingsControl(UI.LayoutMode mode)
        {
            return null;
        }

        public void SetSettings(System.Xml.XmlNode settings)
        {
        }

        public void RenameComparison(string oldName, string newName)
        {
        }

        public void Dispose()
        {
            if (Model != null)
                Model.CurrentState.OnStart -= state_OnStart;
        }
    }
}
