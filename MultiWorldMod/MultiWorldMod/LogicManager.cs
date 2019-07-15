using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using SeanprCore;
using static MultiWorldMod.LogHelper;

// Slightly modified version of LogicManager from RandomizerMod

namespace MultiWorldMod
{
    internal enum ItemType
    {
        Big,
        Charm,
        Shop,
        Spell,
        Geo
    }

    // ReSharper disable InconsistentNaming
#pragma warning disable 0649 // Assigned via reflection
    internal struct ReqDef
    {
        // Control variables
        public string boolName;
        public string sceneName;
        public string objectName;
        public string altObjectName;
        public string fsmName;
        public bool replace;
        public string[] logic;

        public ItemType type;

        public bool newShiny;
        public int x;
        public int y;

        // Big item variables
        public string bigSpriteKey;
        public string takeKey;
        public string nameKey;
        public string buttonKey;
        public string descOneKey;
        public string descTwoKey;

        // Shop variables
        public string shopDescKey;
        public string shopSpriteKey;
        public string notchCost;

        // Item tier flags
        public bool progression;
        public bool isGoodItem;

        // Geo flags
        public bool inChest;
        public int geo;

        public string chestName;
        public string chestFsmName;

        // For pricey items such as dash slash location
        public int cost;
    }

    internal struct ShopDef
    {
        public string sceneName;
        public string objectName;
        public string[] logic;
        public string requiredPlayerDataBool;
        public bool dungDiscount;
    }
#pragma warning restore 0649
    // ReSharper restore InconsistentNaming

    internal static class LogicManager
    {
        private static Dictionary<string, ReqDef> _items;
        private static Dictionary<string, ShopDef> _shops;
        private static Dictionary<string, string[]> _additiveItems;

        public static string[] ItemNames => _items.Keys.ToArray();

        public static string[] ShopNames => _shops.Keys.ToArray();

        public static string[] AdditiveItemNames => _additiveItems.Keys.ToArray();

        public static void ParseXML(object streamObj)
        {
            if (!(streamObj is Stream xmlStream))
            {
                LogWarn("Non-Stream object passed to ParseXML");
                return;
            }

            Stopwatch watch = new Stopwatch();
            watch.Start();

            try
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(xmlStream);
                xmlStream.Dispose();

                _additiveItems = new Dictionary<string, string[]>();
                _items = new Dictionary<string, ReqDef>();
                _shops = new Dictionary<string, ShopDef>();

                ParseAdditiveItemXML(xml.SelectNodes("randomizer/additiveItemSet"));
                ParseItemXML(xml.SelectNodes("randomizer/item"));
                ParseShopXML(xml.SelectNodes("randomizer/shop"));
            }
            catch (Exception e)
            {
                LogError("Could not parse items.xml:\n" + e);
            }

            watch.Stop();
            LogDebug("Parsed items.xml in " + watch.Elapsed.TotalSeconds + " seconds");
        }

        public static ReqDef GetItemDef(string name)
        {
            if (!_items.TryGetValue(name, out ReqDef def))
            {
                LogWarn($"Nonexistent item \"{name}\" requested");
            }

            return def;
        }

        public static ShopDef GetShopDef(string name)
        {
            if (!_shops.TryGetValue(name, out ShopDef def))
            {
                LogWarn($"Nonexistent shop \"{name}\" requested");
            }

            return def;
        }

        public static bool TryGetAdditiveSet(string itemName, out string[] set)
        {
            set = _additiveItems.FirstOrDefault(pair => pair.Value.Contains(itemName)).Value;

            return set != null;
        }

        public static string[] GetAdditiveItems(string name)
        {
            if (!_additiveItems.TryGetValue(name, out string[] items))
            {
                LogWarn($"Nonexistent additive item set \"{name}\" requested");
                return null;
            }

            return (string[])items.Clone();
        }

        private static void ParseAdditiveItemXML(XmlNodeList nodes)
        {
            foreach (XmlNode setNode in nodes)
            {
                XmlAttribute nameAttr = setNode.Attributes?["name"];
                if (nameAttr == null)
                {
                    LogWarn("Node in items.xml has no name attribute");
                    continue;
                }

                string[] additiveSet = new string[setNode.ChildNodes.Count];
                for (int i = 0; i < additiveSet.Length; i++)
                {
                    additiveSet[i] = setNode.ChildNodes[i].InnerText;
                }

                LogDebug($"Parsed XML for item set \"{nameAttr.InnerText}\"");
                _additiveItems.Add(nameAttr.InnerText, additiveSet);
            }
        }

        private static void ParseItemXML(XmlNodeList nodes)
        {
            Dictionary<string, FieldInfo> reqFields = new Dictionary<string, FieldInfo>();
            typeof(ReqDef).GetFields().ToList().ForEach(f => reqFields.Add(f.Name, f));

            foreach (XmlNode itemNode in nodes)
            {
                XmlAttribute nameAttr = itemNode.Attributes?["name"];
                if (nameAttr == null)
                {
                    LogWarn("Node in items.xml has no name attribute");
                    continue;
                }

                // Setting as object prevents boxing in FieldInfo.SetValue calls
                object def = new ReqDef();

                foreach (XmlNode fieldNode in itemNode.ChildNodes)
                {
                    if (!reqFields.TryGetValue(fieldNode.Name, out FieldInfo field))
                    {
                        LogWarn(
                            $"Xml node \"{fieldNode.Name}\" does not map to a field in struct ReqDef");
                        continue;
                    }

                    if (field.FieldType == typeof(string))
                    {
                        field.SetValue(def, fieldNode.InnerText);
                    }
                    else if (field.FieldType == typeof(string[]))
                    {
                        if (field.Name != "logic")
                        {
                            LogWarn(
                                "string[] field not named \"logic\" found in ReqDef, ignoring");
                        }
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        if (bool.TryParse(fieldNode.InnerText, out bool xmlBool))
                        {
                            field.SetValue(def, xmlBool);
                        }
                        else
                        {
                            LogWarn($"Could not parse \"{fieldNode.InnerText}\" to bool");
                        }
                    }
                    else if (field.FieldType == typeof(ItemType))
                    {
                        if (fieldNode.InnerText.TryToEnum(out ItemType type))
                        {
                            field.SetValue(def, type);
                        }
                        else
                        {
                            LogWarn($"Could not parse \"{fieldNode.InnerText}\" to ItemType");
                        }
                    }
                    else if (field.FieldType == typeof(int))
                    {
                        if (int.TryParse(fieldNode.InnerText, out int xmlInt))
                        {
                            field.SetValue(def, xmlInt);
                        }
                        else
                        {
                            LogWarn($"Could not parse \"{fieldNode.InnerText}\" to int");
                        }
                    }
                    else
                    {
                        LogWarn("Unsupported type in ReqDef: " + field.FieldType.Name);
                    }
                }

                LogDebug($"Parsed XML for item \"{nameAttr.InnerText}\"");
                _items.Add(nameAttr.InnerText, (ReqDef)def);
            }
        }

        private static void ParseShopXML(XmlNodeList nodes)
        {
            Dictionary<string, FieldInfo> shopFields = new Dictionary<string, FieldInfo>();
            typeof(ShopDef).GetFields().ToList().ForEach(f => shopFields.Add(f.Name, f));

            foreach (XmlNode shopNode in nodes)
            {
                XmlAttribute nameAttr = shopNode.Attributes?["name"];
                if (nameAttr == null)
                {
                    LogWarn("Node in items.xml has no name attribute");
                    continue;
                }

                // Setting as object prevents boxing in FieldInfo.SetValue calls
                object def = new ShopDef();

                foreach (XmlNode fieldNode in shopNode.ChildNodes)
                {
                    if (!shopFields.TryGetValue(fieldNode.Name, out FieldInfo field))
                    {
                        LogWarn(
                            $"Xml node \"{fieldNode.Name}\" does not map to a field in struct ReqDef");
                        continue;
                    }

                    if (field.FieldType == typeof(string))
                    {
                        field.SetValue(def, fieldNode.InnerText);
                    }
                    else if (field.FieldType == typeof(string[]))
                    {
                        if (field.Name != "logic")
                        {
                            LogWarn(
                                "string[] field not named \"logic\" found in ShopDef, ignoring");
                        }
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        if (bool.TryParse(fieldNode.InnerText, out bool xmlBool))
                        {
                            field.SetValue(def, xmlBool);
                        }
                        else
                        {
                            LogWarn($"Could not parse \"{fieldNode.InnerText}\" to bool");
                        }
                    }
                    else
                    {
                        LogWarn("Unsupported type in ShopDef: " + field.FieldType.Name);
                    }
                }

                LogDebug($"Parsed XML for shop \"{nameAttr.InnerText}\"");
                _shops.Add(nameAttr.InnerText, (ShopDef)def);
            }
        }
    }
}