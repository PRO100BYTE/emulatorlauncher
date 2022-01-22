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

namespace emulatorLauncher
{
    class DemulGenerator : Generator
    {
        bool _oldVersion = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = (emulator == "demul-old" || core == "demul-old") ? "demul-old" : "demul";
            if (folderName == "demul-old")
                _oldVersion = true;

            string path = AppConfig.GetFullPath(folderName);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("demul");

            string exe = Path.Combine(path, "demul.exe");
            if (!File.Exists(exe))
                return null;

            SetupGeneralConfig(path, rom, system, core);
            SetupDx11Config(path, rom, system);

            string demulCore = "dreamcast";

            if (emulator == "demul-hikaru" || core == "hikaru")
                demulCore = "hikaru";
            else if (emulator == "demul-gaelco" || core == "gaelco")
                demulCore = "gaelco";
            else if (emulator == "demul-atomiswave" || core == "atomiswave")
                demulCore = "awave";
            else if (emulator == "demul-naomi" || emulator == "demul-naomi2" || core == "naomi")
                demulCore = "naomi";
            else
            {
                switch (system)
                {
                    case "hikaru":
                        demulCore = "hikaru"; break;
                    case "gaelco":
                        demulCore = "gaelco"; break;
                    case "naomi":
                    case "naomi2":
                        demulCore = "naomi"; break;
                    case "atomiswave":
                        demulCore = "awave"; break;
                }
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,                
                Arguments = "-run=" + demulCore + " -rom=\"" + Path.GetFileNameWithoutExtension(rom).ToLower() + "\"",
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            var process = Process.Start(path);

            while (process != null)
            {
                if (process.WaitForExit(50))
                {
                    process = null;
                    break;
                }

                var hWnd = User32.FindHwnd(process.Id);
                if (hWnd == IntPtr.Zero)
                    continue;
                
                var name = User32.GetWindowText(hWnd);
                if (name != null && name.StartsWith("gpu"))
                {                    
                    SendKeys.SendWait("%~");
                    break;
                }
            }

            if (process != null)
            {
                process.WaitForExit();
                return process.ExitCode;
            }

            return -1;
        }

        private void SetupGeneralConfig(string path, string rom, string system, string core)
        {
            string iniFile = Path.Combine(path, "Demul.ini");

            try
            {
                using (var ini = new IniFile(iniFile, true))
                {
                    ini.WriteValue("files", "roms0", AppConfig.GetFullPath("bios"));
                    ini.WriteValue("files", "roms1", Path.GetDirectoryName(rom));
                    ini.WriteValue("files", "romsPathsCount", "2");

                    ini.WriteValue("plugins", "directory", @".\plugins\");

                    string gpu = "gpuDX11.dll";
                    if (_oldVersion || core == "gaelco" || system == "galeco")
                        gpu = "gpuDX11old.dll";

                    ini.WriteValue("plugins", "gpu", gpu);

                    if (string.IsNullOrEmpty(ini.GetValue("plugins", "gpu")))
                        ini.WriteValue("plugins", "pad", "padDemul.dll");

                    if (string.IsNullOrEmpty(ini.GetValue("plugins", "gdr")))
                        ini.WriteValue("plugins", "gdr", "gdrCHD.dll");

                    if (string.IsNullOrEmpty(ini.GetValue("plugins", "spu")))
                        ini.WriteValue("plugins", "spu", "spuDemul.dll");

                    if (ini.GetValue("plugins", "net") == null)
                        ini.WriteValue("plugins", "net", "netDemul.dll");
                }
            }

            catch { }
        }

        private void SetupDx11Config(string path, string rom, string system)
        {
            string iniFile = Path.Combine(path, "gpuDX11.ini");

            try
            {
                using (var ini = new IniFile(iniFile, true))
                {
                    ini.WriteValue("main", "UseFullscreen", "0");
                    ini.WriteValue("main", "Vsync", SystemConfig["VSync"] != "false" ? "1" : "0");                    
                }
            }

            catch { }
        }
    }
}
