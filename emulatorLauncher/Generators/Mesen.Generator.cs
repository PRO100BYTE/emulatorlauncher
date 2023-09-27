﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using emulatorLauncher.PadToKeyboard;
using System.Windows.Forms;
using System.Threading;
using System.Xml.Linq;
using System.Drawing;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class MesenGenerator : Generator
    {

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mesen");

            string exe = Path.Combine(path, "Mesen.exe");
            if (!File.Exists(exe))
                return null;

            // settings (xml configuration)
            SetupJsonConfiguration(path, system, rom);

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            _resolution = resolution;

            // command line parameters
            var commandArray = new List<string>();

            commandArray.Add("\"" + rom + "\"");
            commandArray.Add("--fullscreen");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupJsonConfiguration(string path, string system, string rom)
        {
            string settingsFile = Path.Combine(path, "settings.json");
            if (!File.Exists(settingsFile))
                File.WriteAllText(settingsFile, "{}");

            var json = DynamicJson.Load(settingsFile);

            string mesenSystem = GetMesenSystem(system);
            if (mesenSystem == "none")
                return;

            json["FirstRun"] = "false";

            // System preferences
            var systemSection = json.GetOrCreateContainer(mesenSystem);
            ConfigureNes(systemSection, system, rom);
            ConfigurePCEngine(systemSection, system, rom);
            ConfigureSnes(systemSection, system, rom);
            ConfigureGameboy(systemSection, system, rom);

            // Emulator preferences
            var preference = json.GetOrCreateContainer("Preferences");

            preference["AutomaticallyCheckForUpdates"] = "false";
            preference["SingleInstance"] = "true";
            preference["AutoLoadPatches"] = "true";
            preference["PauseWhenInBackground"] = "true";
            preference["PauseWhenInMenusAndConfig"] = "true";
            preference["AllowBackgroundInput"] = "true";
            preference["ConfirmExitResetPower"] = "false";
            preference["AssociateSnesRomFiles"] = "false";
            preference["AssociateSnesMusicFiles"] = "false";
            preference["AssociateNesRomFiles"] = "false";
            preference["AssociateNesMusicFiles"] = "false";
            preference["AssociateGbRomFiles"] = "false";
            preference["AssociateGbMusicFiles"] = "false";
            preference["AssociatePceRomFiles"] = "false";
            preference["AssociatePceMusicFiles"] = "false";

            if (SystemConfig.isOptSet("mesen_autosave") && SystemConfig["mesen_autosave"] != "false")
            {
                preference["EnableAutoSaveState"] = "true";
                preference["AutoSaveStateDelay"] = SystemConfig["mesen_autosave"];
            }
            else
                preference["EnableAutoSaveState"] = "false";

            BindBoolFeature(preference, "EnableRewind", "rewind", "true", "false");
            BindBoolFeature(preference, "DisableOsd", "mesen_osd", "false", "true");
            BindBoolFeature(preference, "ShowGameTimer", "mesen_timecounter", "true", "false");
            BindBoolFeature(preference, "ShowFps", "mesen_fps", "true", "false");

            // define folders
            string gamesFolder = Path.GetDirectoryName(rom);
            if (!string.IsNullOrEmpty(gamesFolder) && Directory.Exists(gamesFolder))
            {
                preference["OverrideGameFolder"] = "true";
                preference["GameFolder"] = gamesFolder;
            }

            string recordsFolder = Path.Combine(AppConfig.GetFullPath("records"), "output", "mesen");
            if (!Directory.Exists(recordsFolder)) try { Directory.CreateDirectory(recordsFolder); }
                catch { }
            if (!string.IsNullOrEmpty(recordsFolder) && Directory.Exists(recordsFolder))
            {
                preference["OverrideAviFolder"] = "true";
                preference["AviFolder"] = recordsFolder;
            }

            string savesFolder = Path.Combine(AppConfig.GetFullPath("saves"), system, "mesen");
            if (!Directory.Exists(savesFolder)) try { Directory.CreateDirectory(savesFolder); }
                catch { }
            if (!string.IsNullOrEmpty(savesFolder) && Directory.Exists(savesFolder))
            {
                preference["OverrideSaveDataFolder"] = "true";
                preference["SaveDataFolder"] = savesFolder;
            }

            string saveStateFolder = Path.Combine(AppConfig.GetFullPath("saves"), system, "mesen", "SaveStates");
            if (!Directory.Exists(saveStateFolder)) try { Directory.CreateDirectory(saveStateFolder); }
                catch { }
            if (!string.IsNullOrEmpty(saveStateFolder) && Directory.Exists(saveStateFolder))
            {
                preference["OverrideSaveStateFolder"] = "true";
                preference["SaveStateFolder"] = saveStateFolder;
            }

            string screenshotsFolder = Path.Combine(AppConfig.GetFullPath("screenshots"), "mesen");
            if (!Directory.Exists(screenshotsFolder)) try { Directory.CreateDirectory(screenshotsFolder); }
                catch { }
            if (!string.IsNullOrEmpty(screenshotsFolder) && Directory.Exists(screenshotsFolder))
            {
                preference["OverrideScreenshotFolder"] = "true";
                preference["ScreenshotFolder"] = screenshotsFolder;
            }

            // Video menu
            var video = json.GetOrCreateContainer("Video");
            BindFeature(video, "VideoFilter", "mesen_filter", "None");
            BindFeature(video, "AspectRatio", "mesen_ratio", "Auto");
            BindBoolFeature(video, "UseBilinearInterpolation", "bilinear_filtering", "true", "false");
            BindBoolFeature(video, "VerticalSync", "mesen_vsync", "false", "true");
            BindFeature(video, "ScanlineIntensity", "mesen_scanlines", "0");
            BindBoolFeature(video, "FullscreenForceIntegerScale", "integerscale", "true", "false");

            // Emulation menu
            var emulation = json.GetOrCreateContainer("Emulation");
            BindFeature(emulation, "RunAheadFrames", "mesen_runahead", "0");

            // Input menu
            var input = json.GetOrCreateContainer("Input");
            BindBoolFeature(input, "HidePointerForLightGuns", "mesen_target", "false", "true");

            // Controllers configuration
            SetupControllers(systemSection, mesenSystem);
            SetupGuns(systemSection, mesenSystem);

            // Save json file
            json.Save();
        }

        private void ConfigureNes(DynamicJson section, string system, string rom)
        {
            if (system != "nes" && system != "fds")
                return;

            BindBoolFeature(section, "EnableHdPacks", "mesen_customtextures", "true", "false");
            BindFeature(section, "Region", "mesen_region", "Auto");
            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit", "true", "false");

            if (system == "fds")
            {
                BindBoolFeature(section, "FdsAutoInsertDisk", "mesen_fdsautoinsertdisk", "true", "false");
                BindBoolFeature(section, "FdsFastForwardOnLoad", "mesen_fdsfastforwardload", "true", "false");
                section["FdsAutoLoadDisk"] = "true";
            }
        }

        private void ConfigurePCEngine(DynamicJson section, string system, string rom)
        {
            if (system != "pcengine")
                return;

            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit", "true", "false");
        }

        private void ConfigureGameboy(DynamicJson section, string system, string rom)
        {
            if (system != "gb" && system != "gbc")
                return;
        }

        private void ConfigureSnes(DynamicJson section, string system, string rom)
        {
            if (system != "snes")
                return;

            BindFeature(section, "Region", "mesen_region", "Auto");
        }

        private void SetupGuns(DynamicJson section, string mesenSystem)
        {
            if (mesenSystem == "Nes")
            {
                if (SystemConfig.isOptSet("mesen_zapper") && !string.IsNullOrEmpty(SystemConfig["mesen_zapper"]) && SystemConfig["mesen_zapper"] != "none")
                {
                    var portSection = section.GetOrCreateContainer(SystemConfig["mesen_zapper"]);
                    var mapping = portSection.GetOrCreateContainer("Mapping1");
                    List<int> mouseID = new List<int>();
                    mouseID.Add(512);
                    mouseID.Add(513);
                    mapping.SetObject("ZapperButtons", mouseID);

                    portSection["Type"] = "Zapper";
                }
            }

            else if (mesenSystem == "Snes")
            {
                if (SystemConfig.isOptSet("mesen_superscope") && !string.IsNullOrEmpty(SystemConfig["mesen_superscope"]) && SystemConfig["mesen_superscope"] != "none")
                {
                    var portSection = section.GetOrCreateContainer(SystemConfig["mesen_superscope"]);
                    var mapping = portSection.GetOrCreateContainer("Mapping1");
                    List<int> mouseID = new List<int>();
                    mouseID.Add(512);
                    mouseID.Add(513);
                    mouseID.Add(514);
                    mouseID.Add(6);
                    mapping.SetObject("SuperScopeButtons", mouseID);

                    portSection["Type"] = "SuperScope";
                }
            }
        }

        private string GetMesenSystem(string System)
        {
            switch (System)
            {
                case "nes":
                case "fds":
                    return "Nes";
                case "snes":
                    return "Snes";
                case "gb":
                case "gbc":
                    return "Gameboy";
                case "pcengine":
                    return "PcEngine";
            }
            return "none";
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}
