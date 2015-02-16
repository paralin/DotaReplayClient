using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DOTAReplayClient
{
    public class SteamFinder
    {
        private static readonly string[] knownLocations =
        {
            @"C:\Steam\", @"C:\Program Files (x86)\Steam\", @"C:\Program Files\Steam\"
        };

        private string cachedDotaLocation = "";
        private string cachedLocation = "";

        private bool ContainsSteam(string dir)
        {
            return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "Steam.exe"));
        }

        public string FindSteam(bool delCache, bool useProtocol = true)
        {
            if (delCache) cachedLocation = "";
            if (delCache || cachedLocation == "")
            {
                foreach (string loc in knownLocations)
                {
                    if (ContainsSteam(loc))
                    {
                        cachedLocation = loc;
                        return loc;
                    }
                }

                //Get from registry?
                RegistryKey regKey = Registry.CurrentUser;
                try
                {
                    regKey = regKey.OpenSubKey(@"Software\Valve\Steam");

                    if (regKey != null)
                    {
                        cachedLocation = regKey.GetValue("SteamPath").ToString();
                        return cachedLocation;
                    }
                }
                catch (Exception ex)
                {
                }

                if (useProtocol)
                {
                    Process.Start("steam://");
                    int tries = 0;
                    while (tries < 20)
                    {
                        Process[] processes = Process.GetProcessesByName("STEAM");
                        if (processes.Length > 0)
                        {
                            try
                            {
                                string dir = processes[0].MainModule.FileName.Substring(0,
                                    processes[0].MainModule.FileName.Length - 9);
                                if (Directory.Exists(dir))
                                {
                                    cachedLocation = dir;

                                    return cachedLocation;
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                        else
                        {
                            Thread.Sleep(500);
                            tries++;
                        }
                    }
                }

                return null;
            }
            return cachedLocation;
        }

        public string FindDota(bool delCache, bool useProtocol = true)
        {
            if (!delCache && cachedDotaLocation != null) return cachedDotaLocation;
            string steamDir = FindSteam(false);
            //Get from registry
            RegistryKey regKey = Registry.LocalMachine;
            try
            {
                regKey = regKey.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 570");
                if (regKey != null)
                {
                    string dir = regKey.GetValue("InstallLocation").ToString();
                    if (checkDotaDir(dir))
                    {
                        cachedDotaLocation = dir;
                        return cachedDotaLocation;
                    }
                }
            }
            catch (Exception ex)
            {
            }

            if (steamDir != null)
            {
                string dir = Path.Combine(steamDir, @"steamapps\common\dota 2 beta\");
                if (checkDotaDir(dir))
                {
                    cachedDotaLocation = dir;
                    return cachedDotaLocation;
                }
            }

            if (useProtocol)
            {
                Process.Start("steam://rungameid/570");
                int tries = 0;
                while (tries < 20)
                {
                    Process[] processes = Process.GetProcessesByName("DOTA");
                    if (processes.Length > 0)
                    {
                        try
                        {
                            string dir = processes[0].MainModule.FileName.Substring(0, processes[0].MainModule.FileName.Length - 8);
                            processes[0].Kill();
                            if (checkDotaDir(dir))
                            {
                                cachedLocation = dir;

                                return cachedLocation;
                            }

                        }
                        catch (Exception ex)
                        {
                        }
                    }
                    else
                    {
                        Thread.Sleep(500);
                        tries++;
                    }
                }
            }
            return null;
        }

        public Dictionary<int, string> FindUsers()
        {
            Dictionary<int, string> usersDict = new Dictionary<int, string>();
            string steamDir = FindSteam(false);

            // Detect steam account id which was logged in most recently
            string config = File.ReadAllText(Path.Combine(steamDir, @"config\loginusers.vdf"));

            MatchCollection idMatches = Regex.Matches(config, "\"\\d{17}\"");
            MatchCollection timestampMatches = Regex.Matches(config, "(?m)(?<=\"Timestamp\".{2}).*$", RegexOptions.IgnoreCase);

            if (idMatches.Count > 0)
            {
                for (int i = 0; i < idMatches.Count; i++)
                {
                    try
                    {
                        string steamid = idMatches[i].Value.Trim(' ', '"');
                        string timestamp = timestampMatches[i].Value.Trim(' ', '"');
                        int iTimestamp = Convert.ToInt32(timestamp);
                        usersDict.Add(iTimestamp, steamid);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }

            return usersDict;
        }

        public static bool checkDotaDir(string path)
        {
            return Directory.Exists(path) && Directory.Exists(Path.Combine(path, "dota")) && File.Exists(Path.Combine(path, "dota/gameinfo.txt"));
        }
        public bool checkProtocol()
        {
            RegistryKey regKey = Registry.ClassesRoot;
            regKey = regKey.OpenSubKey(@"steam");
            if (regKey != null)
            {
                string protocolVal = regKey.GetValue(null).ToString();
                if (protocolVal.Contains("URL:steam protocol"))
                {
                    var commandKey = regKey.OpenSubKey(@"Shell\Open\Command");
                    if (commandKey != null && commandKey.GetValue(null) != null)
                        return true;
                }
            }
            return false;
        }
    }
}
