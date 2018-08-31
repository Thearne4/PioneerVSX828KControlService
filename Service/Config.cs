using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Reflection;

namespace Service
{
    public class Config : IEquatable<Config>
    {
        public bool? FuckConfig { get; set; }

        public IPAddress IpAddress { get; set; }
        public int Port { get; set; }
        public string Name { get; set; }

        public bool? TurnOnOnStart { get; set; }
        public bool? TurnOffOnStart { get; set; }

        public bool? TurnOnOnStop { get; set; }
        public bool? TurnOffOnStop { get; set; }

        public bool? TurnOnOnShutdown { get; set; }
        public bool? TurnOffOnShutdown { get; set; }

        public bool TurnOnOnPause { get; set; }
        public bool TurnOffOnPause { get; set; }

        public bool TurnOnOnContinue { get; set; }
        public bool TurnOffOnContinue { get; set; }

        public bool? KeepAlive { get; set; }
        public double? KeepAliveTime { get; set; }

        internal bool IsComplete()
        {
            if (IpAddress == null) return false;
            if (Port <= 0) return false;
            if (Name == null) return false;

            if (!KeepAlive.HasValue) return false;
            if (KeepAlive.Value && !KeepAliveTime.HasValue) return false;

            if (!TurnOnOnStart.HasValue) return false;
            if (!TurnOffOnStart.HasValue) return false;
            if (!TurnOnOnStop.HasValue) return false;
            if (!TurnOffOnStop.HasValue) return false;
            if (!TurnOnOnShutdown.HasValue) return false;
            if (!TurnOffOnShutdown.HasValue) return false;

            return true;
        }

        internal bool IsValid()
        {
            if (IpAddress == null) return false;
            if (Port <= 0) return false;

            if (!KeepAlive.HasValue) return false;
            if (KeepAlive.Value && !KeepAliveTime.HasValue) return false;

            if (TurnOnOnStart.HasValue && TurnOnOnStart == TurnOffOnStart) return false;
            if (TurnOnOnStop.HasValue && TurnOnOnStop == TurnOffOnStop) return false;
            if (TurnOnOnShutdown.HasValue && TurnOnOnShutdown == TurnOffOnShutdown) return false;

            return true;
        }

        internal void FillBlanks(Config otherConfig)
        {
            if (IpAddress == null) IpAddress = otherConfig.IpAddress;
            if (Port <= 0) Port = otherConfig.Port;
            if (Name == null) Name = otherConfig.Name;

            if (KeepAlive == null) KeepAlive = otherConfig.KeepAlive;
            if (KeepAliveTime == null) KeepAliveTime = otherConfig.KeepAliveTime;

            if (TurnOnOnStart == null) TurnOnOnStart = otherConfig.TurnOnOnStart;
            if (TurnOffOnStart == null) TurnOffOnStart = otherConfig.TurnOffOnStart;
            if (TurnOnOnStop == null) TurnOnOnStop = otherConfig.TurnOnOnStop;
            if (TurnOffOnStop == null) TurnOffOnStop = otherConfig.TurnOffOnStop;
            if (TurnOnOnShutdown == null) TurnOnOnShutdown = otherConfig.TurnOnOnShutdown;
            if (TurnOffOnShutdown == null) TurnOffOnShutdown = otherConfig.TurnOffOnShutdown;
        }

        internal void FillBlanksWithDefault()
        {
            if (!KeepAlive.HasValue) KeepAlive = true;
            if (!KeepAliveTime.HasValue) KeepAliveTime = 300000;

            if (TurnOnOnStart.HasValue) TurnOffOnStart = !TurnOnOnStart;
            if (TurnOffOnStart.HasValue) TurnOnOnStart = !TurnOffOnStart;
            if (!TurnOnOnStart.HasValue)
            {
                TurnOnOnStart = false;
                TurnOffOnStart = false;
            }

            if (TurnOnOnStop.HasValue) TurnOffOnStop = !TurnOnOnStop;
            if (TurnOffOnStop.HasValue) TurnOnOnStop = !TurnOffOnStop;
            if (!TurnOnOnStop.HasValue)
            {
                TurnOnOnStop = false;
                TurnOffOnStop = false;
            }

            if (TurnOnOnShutdown.HasValue) TurnOffOnShutdown = !TurnOnOnShutdown;
            if (TurnOffOnShutdown.HasValue) TurnOnOnShutdown = !TurnOffOnShutdown;
            if (!TurnOnOnShutdown.HasValue)
            {
                TurnOnOnShutdown = false;
                TurnOffOnShutdown = false;
            }
        }

        internal static Config FromArgs(Dictionary<string, string> parsedArgs)
        {
            if (parsedArgs == null) return null;

            Config config = new Config();
            foreach (KeyValuePair<string, string> kvp in parsedArgs)
            {
                PropertyInfo prop = typeof(Config).GetProperty(kvp.Key,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null) continue;

                if (prop.PropertyType == typeof(bool?))
                {
                    if (kvp.Value.ToLowerInvariant().StartsWith("t"))
                    {
                        prop.SetValue(config, true);
                    }
                    else if (kvp.Value.ToLowerInvariant().StartsWith("f"))
                    {
                        prop.SetValue(config, false);
                    }
                    else
                    {
                        prop.SetValue(config, null);
                    }
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    prop.SetValue(config, kvp.Value.ToLowerInvariant().StartsWith("t"));
                }
                else if (prop.PropertyType == typeof(string))
                {
                    prop.SetValue(config, kvp.Value);
                }
                else if (prop.PropertyType == typeof(int?) || prop.PropertyType == typeof(int))
                {
                    if (int.TryParse(kvp.Value, out int value))
                    {
                        prop.SetValue(config, value);
                    }
                }
                else if (prop.PropertyType == typeof(IPAddress))
                {
                    if (IPAddress.TryParse(kvp.Value, out IPAddress ip))
                    {
                        prop.SetValue(config, ip);
                    }
                }
            }

            return config;
        }

        internal static Config FromConfig()
        {
            Config config = new Config();
            foreach (string key in ConfigurationManager.AppSettings.AllKeys)
            {
                PropertyInfo prop = typeof(Config).GetProperty(key,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null) continue;

                if (prop.PropertyType == typeof(bool?))
                {
                    if (ConfigurationManager.AppSettings[key].ToLowerInvariant().StartsWith("t"))
                    {
                        prop.SetValue(config, true);
                    }
                    else if (ConfigurationManager.AppSettings[key].ToLowerInvariant().StartsWith("f"))
                    {
                        prop.SetValue(config, false);
                    }
                    else
                    {
                        prop.SetValue(config, null);
                    }
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    prop.SetValue(config, ConfigurationManager.AppSettings[key].ToLowerInvariant().StartsWith("t"));
                }
                else if (prop.PropertyType == typeof(string))
                {
                    prop.SetValue(config, ConfigurationManager.AppSettings[key]);
                }
                else if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(int?))
                {
                    if (int.TryParse(ConfigurationManager.AppSettings[key], out int value))
                    {
                        prop.SetValue(config, value);
                    }
                }
                else if (prop.PropertyType == typeof(double) || prop.PropertyType == typeof(double?))
                {
                    if (double.TryParse(ConfigurationManager.AppSettings[key], out double value))
                    {
                        prop.SetValue(config, value);
                    }
                }
                else if (prop.PropertyType == typeof(IPAddress))
                {
                    if (IPAddress.TryParse(ConfigurationManager.AppSettings[key], out IPAddress ip))
                    {
                        prop.SetValue(config, ip);
                    }
                }
            }

            return config;
        }

        internal static Config FillBlanksWithDefault(Config config)
        {
            Config clone = (Config)config.MemberwiseClone();
            clone.FillBlanksWithDefault();
            return clone;
        }

        internal static Config LoadConfigOrArgs(string[] args)
        {
            var parsedArgs = ParseArgs(args);

            Config argConfig = FromArgs(parsedArgs);
            Config fileConfig = FromConfig();

            if (argConfig != null && argConfig.FuckConfig == true) return FillBlanksWithDefault(argConfig);
            if (argConfig != null && argConfig.IsComplete()) return argConfig;
            if (argConfig != null)
            {
                argConfig.FillBlanks(fileConfig);
                return FillBlanksWithDefault(argConfig);
            }

            return FillBlanksWithDefault(fileConfig);

        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            if (args == null) return null;

            Dictionary<string, string> parsedArgs = new Dictionary<string, string>();
            string curKey = null;
            foreach (string arg in args)
            {
                if (arg.StartsWith("-") || arg.StartsWith("/"))
                {
                    curKey = arg.Substring(1);
                    parsedArgs.Add(curKey, String.Empty);
                }
                else if (!string.IsNullOrEmpty(curKey))
                {
                    parsedArgs[curKey] += arg;
                }
            }

            return parsedArgs;
        }


        public override bool Equals(object obj)
        {
            return Equals(obj as Config);
        }
        public bool Equals(Config other)
        {
            return other != null &&
                   EqualityComparer<bool?>.Default.Equals(FuckConfig, other.FuckConfig) &&
                   EqualityComparer<bool?>.Default.Equals(TurnOnOnStart, other.TurnOnOnStart) &&
                   EqualityComparer<bool?>.Default.Equals(TurnOffOnStart, other.TurnOffOnStart) &&
                   EqualityComparer<bool?>.Default.Equals(TurnOnOnStop, other.TurnOnOnStop) &&
                   EqualityComparer<bool?>.Default.Equals(TurnOffOnStop, other.TurnOffOnStop) &&
                   EqualityComparer<bool?>.Default.Equals(TurnOnOnShutdown, other.TurnOnOnShutdown) &&
                   EqualityComparer<bool?>.Default.Equals(TurnOffOnShutdown, other.TurnOffOnShutdown) &&
                   EqualityComparer<IPAddress>.Default.Equals(IpAddress, other.IpAddress) &&
                   EqualityComparer<int?>.Default.Equals(Port, other.Port) &&
                   Name == other.Name &&
                   EqualityComparer<bool?>.Default.Equals(KeepAlive, other.KeepAlive) &&
                   EqualityComparer<double?>.Default.Equals(KeepAliveTime, other.KeepAliveTime);
        }

        public static bool operator ==(Config config1, Config config2)
        {
            return EqualityComparer<Config>.Default.Equals(config1, config2);
        }
        public static bool operator !=(Config config1, Config config2)
        {
            return !(config1 == config2);
        }
    }
}
