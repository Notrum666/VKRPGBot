using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace VKRPGBot
{
    static class Translator
    {
        private static Dictionary<string, string> phrases = new Dictionary<string, string>();
        public static string Get(string keyword)
        {
            if (!phrases.ContainsKey(keyword))
                return "";
            return phrases[keyword];
        }
        public static void Set(string keyword, string value)
        {
            phrases[keyword] = value;
        }
        public static void Load(string lang)
        {
            MatchCollection matches = Regex.Matches(File.ReadAllText("lang/" + lang + ".lang"), "-(?<keyword>[\\w. ]+)=(?<phrase>.*?)(?=-[\\w. ]+=|$)", RegexOptions.Singleline);
            foreach (Match match in matches)
                phrases[match.Groups["keyword"].Value.Trim()] = match.Groups["phrase"].Value.Trim();
            matches = Regex.Matches(File.ReadAllText("World/" + lang + ".lang"), "-(?<keyword>[\\w. ]+)=(?<phrase>.*?)(?=-[\\w. ]+=|$)", RegexOptions.Singleline);
            foreach (Match match in matches)
                phrases[match.Groups["keyword"].Value.Trim()] = match.Groups["phrase"].Value.Trim();
        }
        //public static void Save(string lang)
        //{
        //    string text = "";
        //    foreach (KeyValuePair<string, string> phrase in phrases)
        //        text += "-" + phrase.Key + " = " + phrase.Value + '\n';
        //    File.WriteAllText("lang/" + lang + ".lang", text);
        //}
    }
}
