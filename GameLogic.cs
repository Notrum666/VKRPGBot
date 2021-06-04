using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
//using System.Text.Json;
//using System.Text.Json.Serialization;

namespace VKRPGBot
{
    class Interval
    {
        public double from, to;
        public Interval(double from, double to)
        {
            this.from = from;
            this.to = to;
        }
        public bool isIn(double value)
        {
            return value >= from && value <= to;
        }
    }
    class Race
    {
        public Interval preferedTemperature;
        public Interval comfortTemperature;
        public Race(Interval preferedTemperature, Interval comfortTemperature)
        {
            this.preferedTemperature = preferedTemperature;
            this.comfortTemperature = comfortTemperature;
        }
    }
    static class Races
    {
        public static Race Human = new Race(new Interval(15, 25), new Interval(5, 35));
        public static Race Gnome = new Race(new Interval(-5, 5), new Interval(-20, 10));
        public static new string ToString()
        {
            FieldInfo[] fields = typeof(Races).GetFields();
            return string.Join(", ", fields.Select((FieldInfo field) => { return Translator.Get("races." + field.Name.ToLower()); }));
        }
        public static bool TryParse(string raceName, bool ignoreCase, out Race race)
        {
            FieldInfo info = Array.Find(typeof(Races).GetFields(), 
                (FieldInfo field) => { if (ignoreCase) return Translator.Get("races." + field.Name.ToLower()).ToLower() == raceName.ToLower(); else return Translator.Get("races." + field.Name.ToLower()) == raceName; });
            if (info == null)
            {
                race = null;
                return false;
            }
            race = (Race)info.GetValue(null);
            return true;
        }
    }
    abstract class Character
    {
        public string name;
        public Race race = Races.Human;
        public Location curLocation;
        public abstract void update();
    }
    class Player : Character
    {
        public enum PlayerStates
        {
            Default,
            Traveling
        }
        private User user;
        #region Stats

        public class onModifyStatEventArgs
        {
            public int origValue { get; private set; }
            public bool locked = false;
            private int _value;
            public int curValue { get { return _value; } set { if (!locked) _value = Math.Max(value, 0); } }
            public onModifyStatEventArgs(int origValue)
            {
                this.origValue = origValue;
                curValue = origValue;
            }
        }
        public delegate int onModifyStat_del(Player player, onModifyStatEventArgs e);

        public int origStrength = 1;
        public int strength { get { return onModifyStrength != null ? onModifyStrength(this, new onModifyStatEventArgs(origStrength)) : origStrength; } }
        public event onModifyStat_del onModifyStrength;
        public int origStamina = 1;
        public int stamina { get { return onModifyStamina != null ? onModifyStamina(this, new onModifyStatEventArgs(origStamina)) : origStamina; } }
        public event onModifyStat_del onModifyStamina;
        public int origIntelligence = 1;
        public int intelligence { get { return onModifyIntelligence != null ? onModifyIntelligence(this, new onModifyStatEventArgs(origIntelligence)) : origIntelligence; } }
        public event onModifyStat_del onModifyIntelligence;
        public int origWisdom = 1;
        public int wisdom { get { return onModifyWisdom != null ? onModifyWisdom(this, new onModifyStatEventArgs(origWisdom)) : origWisdom; } }
        public event onModifyStat_del onModifyWisdom;
        public int origSpeed = 0;
        public int speed { get { return onModifySpeed != null ? onModifySpeed(this, new onModifyStatEventArgs(origSpeed)) : origSpeed; } }
        public event onModifyStat_del onModifySpeed;
        public int origSpirit = 1;
        public int spirit { get { return onModifySpirit != null ? onModifySpirit(this, new onModifyStatEventArgs(origSpirit)) : origSpirit; } }
        public event onModifyStat_del onModifySpirit;
        public int origVitality = 1;
        public int vitality { get { return onModifyVitality != null ? onModifyVitality(this, new onModifyStatEventArgs(origVitality)) : origVitality; } }
        public event onModifyStat_del onModifyVitality;

        public int origPerception = 0;
        public int perception { get { return onModifyPerception != null ? onModifyPerception(this, new onModifyStatEventArgs(origPerception)) : origPerception; } }
        public event onModifyStat_del onModifyPerception;

        public int physicalDamage { get { return Math.Max(1, strength / 2); } }
        public int carryingCapacity { get { return strength * 10 / 4; } }

        public int maxMana { get { return wisdom * 10; } }
        public int curMana;
        public int maxHealth { get { return stamina * 10; } }
        public int curHealth;

        public int healthRegen { get { return vitality * 10; } }
        public int manaRegen { get { return spirit * 10; } }
        public int movespeed { get { return 10 + speed / 2; } }
        public int sightRange { get { return 1000 + perception * 50; } }

        public string statsToString()
        {
            return Translator.Get("stats.strenght") + ": " + strength.ToString() + '\n' +
                   Translator.Get("stats.stamina") + ": " + stamina.ToString() + '\n' +
                   Translator.Get("stats.intelligence") + ": " + intelligence.ToString() + '\n' +
                   Translator.Get("stats.wisdom") + ": " + wisdom.ToString() + '\n' +
                   Translator.Get("stats.speed") + ": " + speed.ToString() + '\n' +
                   Translator.Get("stats.spirit") + ": " + spirit.ToString() + '\n' +
                   Translator.Get("stats.vitality") + ": " + vitality.ToString() + '\n' +
                   Translator.Get("stats.perception") + ": " + perception.ToString() + '\n' +
                   "-----------------------\n" +
                   Translator.Get("stats.physical_damage") + ": " + physicalDamage.ToString() + ' ' + Translator.Get("stats.physical_damage_formula") + '\n' +
                   Translator.Get("stats.carrying_capacity") + ": " + carryingCapacity.ToString() + ' ' + Translator.Get("stats.carrying_capacity_formula") + '\n' +
                   Translator.Get("stats.health") + ": " + curHealth.ToString() + "/" + maxHealth.ToString() + ' ' + Translator.Get("stats.health_formula") + '\n' +
                   Translator.Get("stats.mana") + ": " + curMana.ToString() + "/" + maxMana.ToString() + ' ' + Translator.Get("stats.mana_formula") + '\n' +
                   Translator.Get("stats.health_regen") + ": " + healthRegen.ToString() + Translator.Get("stats.health_regen_formula") + '\n' +
                   Translator.Get("stats.mana_regen") + ": " + manaRegen.ToString() + Translator.Get("stats.mana_regen_formula") + '\n' +
                   Translator.Get("stats.move_speed") + ": " + movespeed.ToString() + ' ' + Translator.Get("stats.move_speed_formula") + '\n' +
                   Translator.Get("stats.sight_range") + ": " + sightRange.ToString() + ' ' + Translator.Get("stats.sight_range_formula");
        }
        #endregion
        public List<Commands.Command> availableCommands {
            get
            {
                List<Commands.Command> commands = new List<Commands.Command>();
                commands.Add(Commands.comList[typeof(Commands.Command_Help)]);
                commands.Add(Commands.comList[typeof(Commands.Command_Me)]);
                commands.Add(Commands.comList[typeof(Commands.Command_LookAround)]);
                commands.Add(Commands.comList[typeof(Commands.Command_Say)]);
                if (state == PlayerStates.Default)
                    commands.Add(Commands.comList[typeof(Commands.Command_GoTo)]);
                if (state == PlayerStates.Traveling)
                    commands.Add(Commands.comList[typeof(Commands.Command_Traveling_Stop)]);
                if (Game.curGame.admins.Contains(name))
                    commands.Add(Commands.comList[typeof(Commands.Command_Admin)]);
                return commands;
            }
        }
        private Commands.Command getCommand(string keyword)
        {
            foreach (Commands.Command command in availableCommands)
                if (command.keywords.Contains(keyword))
                    return command;
            return null;
        }
        public PlayerStates state = PlayerStates.Default;
        public bool chatMode = false;

        public Queue<KeyValuePair<string, string>> commandsQueue = new Queue<KeyValuePair<string, string>>();

        public Player(User user, string nickname, Race race)
        {
            this.user = user;
            name = nickname;
            this.race = race;
            user.onMessageEvent = onMessage;

            curHealth = maxHealth;
            curMana = maxMana;
        }
        public override void update()
        {
            curHealth = (int)Math.Min(maxHealth, curHealth + healthRegen * Time.deltaTime);
            curMana = (int)Math.Min(maxMana, curMana + manaRegen * Time.deltaTime);
        }
        public void moveTo(Location loc)
        {
            curLocation.characters.Remove(this);
            loc.characters.Add(this);
            curLocation = loc;
            sendMessage("Your current location is " + curLocation.name);
        }
        public void sendMessage(string msg, bool rememberMessage = false)
        {
            user.sendMessage(msg, rememberMessage);
        }
        public void onMessage(string msg)
        {
            string keyword = msg.Split(' ')[0].ToLower();
            Commands.Command command = getCommand(keyword.ToLower());
            if (command != null)
            {
                if (chatMode)
                    chatMode = false;
                handleCommand(keyword, msg.Length > keyword.Length + 1 ? msg.Substring(keyword.Length + 1) : "");
            }
            else
            {
                if (chatMode)
                    Commands.comList[typeof(Commands.Command_Say)].trigger(this, msg);
                else
                    sendMessage(Translator.Get("commands.no_such_command"));
            }
        }
        public void markAsRead()
        {
            user.markAsRead();
        }
        public void editLastMessage(string msg)
        {
            user.editLastMessage(msg);
        }
        public void deleteLastMessage()
        {
            user.deleteLastMessage();
        }
        public void handleNextCommand()
        {
            if (commandsQueue.Count == 0)
                return;
            Thread thread = new Thread(new ThreadStart(_handleNextCommand));
            thread.Start();
        }
        private void _handleNextCommand()
        {
            KeyValuePair<string, string> curCommand = commandsQueue.Dequeue();
            Commands.getCommand(curCommand.Key).trigger(this, curCommand.Value);
        }
        public void handleCommand(string keyword, string args)
        {
            Commands.getCommand(keyword).trigger(this, args);
        }
        public override string ToString()
        {
            return name;
        }
    }
    static class Commands
    {
        public static Dictionary<Type, Command> comList = new Dictionary<Type, Command>();
        public static void Initialize()
        {
            comList.Clear();
            foreach (Type t in typeof(Commands).GetNestedTypes().Where((Type t) => { return t.IsSubclassOf(typeof(Command)); }))
                comList[t] = (Command)Activator.CreateInstance(t);
        }
        public static Command getCommand(string keyword)
        {
            foreach (Command command in comList.Values)
                if (command.keywords.Contains(keyword))
                    return command;
            return null;
        }
        public abstract class Command
        {
            public string[] keywords;
            public string args;
            public string description;
            public Command()
            {
                keywords = Translator.Get("commands." + GetType().Name.ToLower() + ".keywords").Split(' ');
                args = Translator.Get("commands." + GetType().Name.ToLower() + ".args");
                description = Translator.Get("commands." + GetType().Name.ToLower() + ".description");
            }
            public abstract void trigger(Player player, string args);
        }
        public class Command_Help : Command
        {
            public override void trigger(Player player, string args)
            {
                string text = "";
                foreach (Command command in player.availableCommands)
                    text += string.Join("/", command.keywords) + (command.args != "" ? " " + command.args : "") + " - " + command.description + "\n\n";
                player.sendMessage(text);
            }
        }
        public class Command_Me : Command
        {
            public override void trigger(Player player, string args)
            {
                player.sendMessage(Translator.Get("commands.command_me.text.name") + ": " + player.name + '\n' +
                                   Translator.Get("commands.command_me.text.race") + ": " + player.race.ToString() + '\n' +
                                   Translator.Get("commands.command_me.text.stats") + ":\n" + player.statsToString());
            }
        }
        public class Command_LookAround : Command
        {
            public override void trigger(Player player, string args)
            {
                Weather weather = player.curLocation.weather;
                string weatherDescription = getWeatherDescription(weather);
                string timeDescription = getTimeDescription(weather, player);
                string temperatureDescription = getTemperatureDescription(player);
                string lightDescription = getLightDescription(player);
                string locationDescription = getLocationDescription(player);
                Dictionary<Location, int> locationsWithDistances = player.curLocation.getLocationsAround(player.sightRange);
                List<Location> locations = locationsWithDistances.Where((KeyValuePair<Location, int> pair) => { return pair.Value < Int32.MaxValue; }).Select((KeyValuePair<Location, int> pair) => { return pair.Key; }).ToList();
                locationsWithDistances.Remove(player.curLocation);
                List<Location> paths = locationsWithDistances.Select((KeyValuePair<Location, int> pair) => { return pair.Key; }).ToList();
                string charactersStr = string.Join(", ", locations.SelectMany((Location loc) => { return loc.characters; }).Select((Character character) => { return character.ToString(); }));
                string pathsStr = string.Join(", ", paths.Select((Location loc) => { return loc.name; }));
                player.sendMessage(player.curLocation.name + '\n' +
                                   locationDescription + '\n' +
                                   (weatherDescription != "" ? weatherDescription + '\n' : "") +
                                   (timeDescription != "" ? timeDescription + '\n' : "") +
                                   temperatureDescription + '\n' +
                                   (lightDescription != "" ? lightDescription + '\n' : "") +
                                   (charactersStr != "" ? Translator.Get("commands.command_lookaround.text.characters_here") + ": " + charactersStr + '\n' : "") +
                                   (pathsStr != "" ? Translator.Get("commands.command_lookaround.text.paths") + ": " + pathsStr : ""));
            }
            private string getWeatherDescription(Weather weather)
            {
                switch (weather)
                {
                    case Weather.None:
                        return "";
                    case Weather.Clear:
                        return Translator.Get("commands.command_lookaround.weather_description.clear");
                    case Weather.Cloudy:
                        return Translator.Get("commands.command_lookaround.weather_description.cloudy");
                    case Weather.Rain_Weak:
                        return Translator.Get("commands.command_lookaround.weather_description.rain_weak");
                    case Weather.Rain_Medium:
                        return Translator.Get("commands.command_lookaround.weather_description.rain_medium");
                    case Weather.Rain_Heavy:
                        return Translator.Get("commands.command_lookaround.weather_description.rain_heavy");
                    case Weather.Snowfall_Weak:
                        return Translator.Get("commands.command_lookaround.weather_description.snowfall_weak");
                    case Weather.Snowfall_Medium:
                        return Translator.Get("commands.command_lookaround.weather_description.snowfall_medium");
                    case Weather.Snowfall_Heavy:
                        return Translator.Get("commands.command_lookaround.weather_description.snowfall_heavy");
                }
                return "";
            }
            private string getTimeDescription(Weather weather, Player player)
            {
                int perception = player.perception;
                if (player.curLocation.isSkyOpened)
                {
                    if (perception >= 400)
                    {
                        if (weather == Weather.Clear)
                            return Game.curGame.getTimeDescription(TimeDescription.ExactDetailed);
                        else
                            return Game.curGame.getTimeDescription(TimeDescription.Exact);
                    }
                    if (perception >= 250)
                    {
                        if (weather == Weather.Clear)
                            return Game.curGame.getTimeDescription(TimeDescription.PreciseDetailed);
                        else
                            return Game.curGame.getTimeDescription(TimeDescription.Precise);
                    }
                    if (perception >= 100)
                    {
                        if (weather == Weather.Clear)
                            return Game.curGame.getTimeDescription(TimeDescription.Detailed);
                        else
                            return "";
                    }
                    if (weather == Weather.Clear)
                        return Game.curGame.getTimeDescription(TimeDescription.Simple);
                }
                return "";
            }
            private string getLightDescription(Player player)
            {
                if (player.curLocation.light <= 0.2)
                    return Translator.Get("commands.command_lookaround.light_description.quite_dark");
                else if (player.curLocation.light <= 0.1)
                    return Translator.Get("commands.command_lookaround.light_description.dark");
                else if (player.curLocation.light <= 0.05)
                    return Translator.Get("commands.command_lookaround.light_description.very_dark");
                return "";
            }
            private string getTemperatureDescription(Player player)
            {
                if (player.race.preferedTemperature.isIn(player.curLocation.temperature))
                    return Translator.Get("commands.command_lookaround.temperature_description.warm");
                else if (player.race.comfortTemperature.isIn(player.curLocation.temperature))
                {
                    if (player.curLocation.temperature < player.race.preferedTemperature.from)
                        return Translator.Get("commands.command_lookaround.temperature_description.quite_cold");
                    else
                        return Translator.Get("commands.command_lookaround.temperature_description.quite_hot");
                } else
                {
                    if (player.curLocation.temperature < player.race.comfortTemperature.from)
                        return Translator.Get("commands.command_lookaround.temperature_description.very_cold");
                    else
                        return Translator.Get("commands.command_lookaround.temperature_description.very_hot");
                }
            }
            private string getLocationDescription(Player player)
            {
                string[] words = player.curLocation.description.Split(' ');
                int neededPerception = (int)Math.Max(0, 1000 * (1 / (player.curLocation.light + 0.5) - 1.4));
                double perceptionCoef = 1.0;
                double perception = player.perception;
                if (perception < neededPerception)
                    perceptionCoef = Math.Max(0.0, (perception - neededPerception + 100) / 100);
                for (int i = 0; i < words.Length; i++)
                    if (perceptionCoef < player.curLocation.descriptionNoise[i])
                        words[i] = new string('*', words[i].Length);
                return string.Join(" ", words);
            }
        }
        public class Command_GoTo : Command
        {
            public override void trigger(Player player, string args)
            {
                string word = args.Split(' ')[0].ToLower();
                if (args.Split(' ')[0].Trim('[', ']').Split('/').Contains(word))
                {
                    args = args.Substring(word.Length + 1).ToLower();

                    foreach (Path path in player.curLocation.paths)
                        if (path.destination.name.ToLower() == args)
                        {
                            player.state = Player.PlayerStates.Traveling;
                            int travelSpeed = (int)(player.movespeed / 3.6);
                            if (path.distance / travelSpeed > 5)
                            {
                                int distance = 0;
                                int timeStep = 5;
                                Stopwatch time = new Stopwatch();
                                time.Start();
                                player.sendMessage(Translator.Get("commands.command_goto.text.traveling_to") + path.destination.name + ", " + Translator.Get("commands.command_goto.text.progress") + ": 0/" + path.distance.ToString(), true);
                                while (player.state == Player.PlayerStates.Traveling && distance < path.distance)
                                {
                                    Thread.Sleep(100);
                                    if (time.Elapsed.TotalSeconds >= Math.Min(timeStep, (path.distance - distance) / travelSpeed))
                                    {
                                        distance += travelSpeed * timeStep;
                                        player.editLastMessage(Translator.Get("commands.command_goto.text.traveling_to") + path.destination.name + ", " + Translator.Get("commands.command_goto.text.progress") + ": " + distance.ToString() + "/" + path.distance.ToString());
                                        time.Restart();
                                    }
                                }
                                time.Stop();
                                player.deleteLastMessage();
                            }
                            else
                                Thread.Sleep(1000 * path.distance / travelSpeed);
                            if (player.state == Player.PlayerStates.Traveling)
                                player.moveTo(path.destination);
                            player.state = Player.PlayerStates.Default;
                            player.handleNextCommand();
                            return;
                        }
                }
                else
                {
                    Dictionary<Location, KeyValuePair<int, List<Location>>> locations = player.curLocation.getPathsAround(player.sightRange);
                    locations.Remove(player.curLocation);
                    foreach (KeyValuePair<Location, KeyValuePair<int, List<Location>>> locationInfo in locations)
                        if (locationInfo.Key.name.ToLower() == args.ToLower())
                        {
                            foreach (Location nextLocation in locationInfo.Value.Value)
                                player.commandsQueue.Enqueue(new KeyValuePair<string, string>(keywords[0], args.Split(' ')[0].Trim('[', ']').Split('/')[0] + " " + nextLocation.name));
                            player.handleNextCommand();
                            return;
                        }
                    player.sendMessage(Translator.Get("commands.command_goto.text.no_path"));
                }
            }
        }
        public class Command_Say : Command
        {
            public override void trigger(Player player, string args)
            {
                if (args.Length == 0)
                {
                    player.sendMessage(Translator.Get("commands.command_say.text.empty_message"));
                    return;
                }
                player.chatMode = true;
                player.curLocation.sendToChat(player, args);
            }
        }
        public class Command_Traveling_Stop : Command
        {
            public override void trigger(Player player, string args)
            {
                player.commandsQueue.Clear();
                player.state = Player.PlayerStates.Default;
            }
        }
        public class Command_Admin : Command
        {
            public override void trigger(Player player, string args)
            {
                string keyword = args.Split(' ')[0];
                args = args.Length > keyword.Length + 1 ? args.Substring(keyword.Length + 1) : "";
                MethodInfo method = typeof(Command_Admin).GetMethod(keyword, BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(this, new object[] { player, args });
                    return;
                }
                player.sendMessage(Translator.Get("commands.command_admin.text.command_not_found"));
            }
            private void move(Player player, string args)
            {
                string playerName = args.Split(' ')[0];
                Player targetedPlayer = null;
                if (playerName == Translator.Get("commands.command_admin.move.text.me"))
                    targetedPlayer = player;
                else
                    foreach (Player p in Game.curGame.Players)
                        if (p.name == playerName)
                        {
                            targetedPlayer = p;
                            break;
                        }
                if (targetedPlayer == null)
                {
                    player.sendMessage(Translator.Get("commands.command_admin.move.text.player_not_found"));
                    return;
                }
                string location = (args.Length > playerName.Length + 1) ? args.Substring(playerName.Length + 1) : "";
                foreach (Location loc in Game.curGame.Map)
                    if (loc.name.ToLower() == location.ToLower())
                    {
                        targetedPlayer.moveTo(loc);
                        if (targetedPlayer != player)
                            player.sendMessage(Translator.Get("commands.command_admin.move.text.success"));
                        return;
                    }
                player.sendMessage(Translator.Get("commands.command_admin.move.text.location_not_found"));
            }
        }
    }
    public enum IgnorePathBlocks
    {
        DontIgnore,
        Once,
        Ignore
    }
    public enum Weather
    {
        None,
        Clear,
        Cloudy,
        Rain_Weak,
        Rain_Medium,
        Rain_Heavy,
        Snowfall_Weak,
        Snowfall_Medium,
        Snowfall_Heavy,
    }
    enum TimeDescription
    {
        /// <summary>
        /// Approximate description in words
        /// </summary>
        Simple,
        /// <summary>
        /// Detailed description in words
        /// </summary>
        Detailed,
        /// <summary>
        /// Amount of hours
        /// </summary>
        Precise,
        /// <summary>
        /// Amount of hours and minutes
        /// </summary>
        Exact,
        /// <summary>
        /// Amount of hours and detailed description in words
        /// </summary>
        PreciseDetailed,
        /// <summary>
        /// Amount of hours and minutes and detailed description in words
        /// </summary>
        ExactDetailed
    }
    class Location
    {
        private static int nextId = 0;
        private readonly int id;
        [JsonIgnore]
        public string name { get { return Translator.Get("location" + id.ToString() + ".name"); } set { Translator.Set("location" + id.ToString() + ".name", value); } }
        [JsonIgnore]
        public string description { get { return Translator.Get("location" + id.ToString() + ".description"); } set { Translator.Set("location" + id.ToString() + ".description", value); generateNoise(); } }
        public void generateNoise()
        {
            int wordsCount = description.Split(' ').Length;
            descriptionNoise = new double[wordsCount];
            double min = 1.0, max = 0.0;
            Random rand = new Random();
            for (int i = 0; i < wordsCount; i++)
            {
                descriptionNoise[i] = rand.NextDouble();
                if (descriptionNoise[i] < min)
                    min = descriptionNoise[i];
                if (descriptionNoise[i] > max)
                    max = descriptionNoise[i];
            }
            for (int i = 0; i < wordsCount; i++)
                descriptionNoise[i] = (descriptionNoise[i] - min) / (max - min);
        }
        [JsonIgnoreWhen("map")]
        public double[] descriptionNoise;
        public bool isSkyOpened = true;
        [JsonIgnoreWhen("map")]
        public Weather weather = Weather.None;

        [JsonIgnoreWhen("map")]
        public double temperature = 0;
        [JsonIgnore]
        public double temperatureDelta = 0;
        public double heatTransferCoef = 1.0;
        public double temperatureShift = 0.0;

        public double lightCoef = 1.0;
        public double light = 0.0;

        [JsonIgnoreWhen("map")]
        public List<Character> characters = new List<Character>();
        public List<Path> paths = new List<Path>();

        public Location()
        {
            id = nextId++;
        }
        public void updateTemperature()
        {
            temperature += temperatureDelta;
            temperatureDelta = 0;
        }
        public void sendToChat(Player sender, string msg)
        {
            foreach (Character character in characters)
                if (character is Player && character != sender)
                    (character as Player).sendMessage("(" + Translator.Get("commands.command_say.text.chat") + ")" + sender.name + ": " + msg);
        }
        public Dictionary<Location, int> getLocationsAround(int range, IgnorePathBlocks ignorePathBlocks = IgnorePathBlocks.Once)
        {
            Dictionary<Location, int> locations = new Dictionary<Location, int>();
            List<Location> curLocations = new List<Location>();
            List<Location> nextLocations = new List<Location>();
            foreach (Path path in paths)
            {
                locations[path.destination] = path.distance;
                curLocations.Add(path.destination);
            }
            while (curLocations.Count > 0)
            {
                foreach (Location loc in curLocations)
                    foreach (Path path in loc.paths)
                    {
                        if (path.blocksView && ignorePathBlocks == IgnorePathBlocks.DontIgnore)
                            continue;
                        int curDistance = locations[loc] + path.distance;
                        if (curDistance <= range)
                        {
                            if (!(locations.ContainsKey(path.destination) && curDistance >= locations[path.destination]))
                            {
                                locations[path.destination] = (path.blocksView && ignorePathBlocks == IgnorePathBlocks.Once ? Int32.MaxValue : curDistance);
                                nextLocations.Add(path.destination);
                            }
                        }
                    }
                curLocations = nextLocations;
                nextLocations = new List<Location>();
            }
            return locations;
        }
        public Dictionary<Location, KeyValuePair<int, List<Location>>> getPathsAround(int range, IgnorePathBlocks ignorePathBlocks = IgnorePathBlocks.Once)
        {
            Dictionary<Location, KeyValuePair<int, List<Location>>> locations = new Dictionary<Location, KeyValuePair<int, List<Location>>>();
            List<Location> curLocations = new List<Location>();
            List<Location> nextLocations = new List<Location>();
            foreach (Path path in paths)
            {
                locations[path.destination] = new KeyValuePair<int, List<Location>>(path.distance, new List<Location>() { path.destination });
                curLocations.Add(path.destination);
            }
            while (curLocations.Count > 0)
            {
                foreach (Location loc in curLocations)
                    foreach (Path path in loc.paths)
                    {
                        if (path.blocksView && ignorePathBlocks == IgnorePathBlocks.DontIgnore)
                            continue;
                        int curDistance = locations[loc].Key + path.distance;
                        if (curDistance <= range)
                        {
                            if (!(locations.ContainsKey(path.destination) && curDistance >= locations[path.destination].Key))
                            {
                                locations[path.destination] = new KeyValuePair<int, List<Location>>(path.blocksView && ignorePathBlocks == IgnorePathBlocks.Once ? Int32.MaxValue : curDistance, locations[loc].Value.Append(path.destination).ToList());
                                nextLocations.Add(path.destination);
                            }
                        }
                    }
                curLocations = nextLocations;
                nextLocations = new List<Location>();
            }
            return locations;
        }
        public string getTime(TimeDescription descriptionLevel)
        {
            if (isSkyOpened == false)
                return "";
            return Game.curGame.getTimeDescription(descriptionLevel);
        }
    }
    class Path
    {
        public Location destination;
        public bool blocksView;
        public int distance;
        public double heatTransferCoef = 1.0;
        public double lightTransferCoef = 1.0;
        public Path(Location destination, int distance = 0, bool blocksView = false)
        {
            this.destination = destination;
            this.blocksView = blocksView;
            this.distance = distance;
        }
    }
    static class Time
    {
        public static double deltaTime;
        public static int elapsedMilliseconds { get { return (int)time.ElapsedMilliseconds; } }
        private static Stopwatch time;
        public static void Initialize()
        {
            time = new Stopwatch();
            time.Start();
        }
        public static void Update()
        {
            deltaTime = time.ElapsedMilliseconds / 1000.0;
            time.Restart();
        }
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    class JsonIgnoreWhenAttribute : Attribute
    {
        public string when;
        public JsonIgnoreWhenAttribute(string when)
        {
            this.when = when;
        }
    }
    class Game
    {
        public static Game curGame;
        public List<Location> Map = new List<Location>();
        public List<Player> Players = new List<Player>();
        public Location startLocation;

        public Random rand = new Random();

        public DateTime worldTime;
        private Stopwatch ambienceTimer = new Stopwatch();
        private double timeScale = 4.8;

        private enum Precipitation
        {
            Clear,
            Cloudy,
            Weak,
            Medium,
            Heavy
        }
        private Precipitation precipitation = Precipitation.Clear;

        public int sunrise { get; private set; }
        public int sunset { get; private set; }
        private double sunriseRel { get { return sunrise / (60.0 * 60.0 * 24.0); } }
        private double sunsetRel { get { return sunset / (60.0 * 60.0 * 24.0); } }
        private double sunHigh = 13;
        private double sunAmplitude = 2.5;

        private double yearTempAmplitude = 20;
        private double dayTempAmplitude = 3;
        private double tempRandomDelta = 0.5;
        private double tempRandomSpeed = 0.1;
        public double temperature { get; private set; }
        private double globalLocationHeatTransferCoef = 0.01;
        private double globalPathHeatTransferCoef = 0.01;

        public double light { get; private set; }
        private double globalLocationLightCoef = 0.95;
        private double globalPathLightCoef = 0.7;

        public string[] admins = new string[] { "Notrum666" };

        private bool isAlive;

        public Game()
        {
            Commands.Initialize();
            curGame = this;

            worldTime = new DateTime(760, 6, 1, 12, 0, 0);
            ambienceTimer.Start();
            updateWorldAmbience();

            //Map.Add(new Location() { lightCoef = 1.0, isSkyOpened = true, heatTransferCoef = 1.0, temperatureShift = 0.0 });
            //Map.Add(new Location() { lightCoef = 1.0, isSkyOpened = true, heatTransferCoef = 1.0, temperatureShift = 0.0 });
            //Map.Add(new Location() { lightCoef = 1.0, isSkyOpened = true, heatTransferCoef = 1.0, temperatureShift = 0.0 });
            //
            //Map[0].paths.Add(new Path(Map[1], 100, false));
            //Map[1].paths.Add(new Path(Map[0], 100, false));
            //
            //Map[1].paths.Add(new Path(Map[2], 100, false));
            //Map[2].paths.Add(new Path(Map[1], 100, false));
            //
            //saveMap("World");
            loadMap();

            isAlive = true;
            Thread mainThread = new Thread(new ThreadStart(cycle));
            mainThread.Start();
        }
        public void shutdown()
        {
            isAlive = false;
        }
        public void registerPlayer(User user, string nickname, Race race)
        {
            Player player = new Player(user, nickname, race);
            player.curLocation = startLocation;
            player.curLocation.characters.Add(player);
            player.onModifyPerception += onModifyPerception;
            Players.Add(player);
        }
        public string getTimeDescription(TimeDescription descriptionLevel)
        {
            int seconds = worldTime.Hour * 60 * 60 + worldTime.Minute * 60 + worldTime.Second;
            string description = "";
            if (descriptionLevel == TimeDescription.Precise || descriptionLevel == TimeDescription.PreciseDetailed)
                description = Translator.Get("game.time_description.precise") + " " + (worldTime.Hour + (worldTime.Minute >= 30 ? 1 : 0)).ToString() + ":00";
            if (descriptionLevel == TimeDescription.Exact || descriptionLevel == TimeDescription.ExactDetailed)
                description = Translator.Get("game.time_description.exact") + " " + worldTime.ToString("H:mm");
            if (descriptionLevel != TimeDescription.Precise && descriptionLevel != TimeDescription.Exact)
            {
                if (descriptionLevel == TimeDescription.ExactDetailed || descriptionLevel == TimeDescription.PreciseDetailed)
                    description += " | ";
                if (seconds > sunrise && seconds <= sunset)
                {
                    double dayProgress = (double)(seconds - sunrise) / (double)(sunset - sunrise);
                    if (descriptionLevel == TimeDescription.Simple)
                    {
                        if (dayProgress < 0.05)
                            description += Translator.Get("game.time_description.sun.rising");
                        else if (dayProgress < 0.35)
                            description += Translator.Get("game.time_description.sun.goes_higher");
                        else if (dayProgress < 0.65)
                            description += Translator.Get("game.time_description.sun.high");
                        else if (dayProgress < 0.95)
                            description += Translator.Get("game.time_description.sun.goes_lower");
                        else
                            description += Translator.Get("game.time_description.sun.setting");
                    }
                    if (descriptionLevel == TimeDescription.Detailed || descriptionLevel == TimeDescription.ExactDetailed || descriptionLevel == TimeDescription.PreciseDetailed)
                    {
                        description += Translator.Get("game.time_description.sun.passed_about") + " " + (Math.Round(dayProgress * 20.0) / 20).ToString() + "% " + Translator.Get("game.time_description.of_sky");
                    }
                }
                else
                {
                    double nightProgress;
                    if (seconds > sunset)
                        nightProgress = (double)(seconds - sunset) / (double)(24 * 60 * 60 - sunset + sunrise);
                    else
                        nightProgress = (double)(seconds + 24 * 60 * 60 - sunset) / (double)(24 * 60 * 60 - sunset + sunrise);
                    if (descriptionLevel == TimeDescription.Simple)
                    {
                        if (nightProgress < 0.05)
                            description += Translator.Get("game.time_description.moon.rising");
                        else if (nightProgress < 0.35)
                            description += Translator.Get("game.time_description.moon.goes_higher");
                        else if (nightProgress < 0.65)
                            description += Translator.Get("game.time_description.moon.high");
                        else if (nightProgress < 0.95)
                            description += Translator.Get("game.time_description.moon.goes_lower");
                        else
                            description += Translator.Get("game.time_description.moon.setting");
                    }
                    if (descriptionLevel == TimeDescription.Detailed || descriptionLevel == TimeDescription.ExactDetailed || descriptionLevel == TimeDescription.PreciseDetailed)
                    {
                        description += Translator.Get("game.time_description.moon.passed_about") + " " + (Math.Round(nightProgress * 20.0) / 20).ToString() + "% " + Translator.Get("game.time_description.of_sky");
                    }
                }
            }
            return description;
        }
        private int onModifyPerception(Player player, Player.onModifyStatEventArgs e)
        {
            Weather weather = player.curLocation.weather;
            if (weather == Weather.Rain_Weak || weather == Weather.Snowfall_Weak)
                e.curValue -= (int)(e.curValue * 0.1);
            else if (weather == Weather.Rain_Medium || weather == Weather.Snowfall_Medium)
                e.curValue -= (int)(e.curValue * 0.2);
            else if (weather == Weather.Rain_Heavy || weather == Weather.Snowfall_Heavy)
                e.curValue -= (int)(e.curValue * 0.3);
            return e.curValue;
        }
        private void cycle()
        {
            Time.Initialize();
            updateWorldWeather();
            updateWorldAmbience();
            while (isAlive)
            {
                Console.Clear();
                Console.WriteLine("elapsedMilliseconds: " + Time.elapsedMilliseconds.ToString());
                Console.WriteLine("Precipitation: " + precipitation.ToString());
                Console.WriteLine("Time: " + worldTime.ToString("HH:mm"));
                Console.WriteLine("Temperature: " + temperature.ToString());
                Console.WriteLine("Light: " + light.ToString());
                Thread.Sleep(Math.Max(1000 - Time.elapsedMilliseconds, 0));
                Time.Update();
                update();
            }
        }
        private void update()
        {
            worldTime = worldTime.AddSeconds(Time.deltaTime * timeScale);
            updateWorldAmbience();
            foreach (Player player in Players)
                player.update();
        }
        private void updateWorldWeather()
        {
            foreach (Location loc in Map)
                if (loc.isSkyOpened)
                    switch (precipitation)
                    {
                        case Precipitation.Clear:
                            loc.weather = Weather.Clear;
                            continue;
                        case Precipitation.Cloudy:
                            loc.weather = Weather.Cloudy;
                            continue;
                        case Precipitation.Weak:
                            if (loc.temperature > 0)
                                loc.weather = Weather.Rain_Weak;
                            else
                                loc.weather = Weather.Snowfall_Weak;
                            continue;
                        case Precipitation.Medium:
                            if (loc.temperature > 0)
                                loc.weather = Weather.Rain_Medium;
                            else
                                loc.weather = Weather.Snowfall_Medium;
                            continue;
                        case Precipitation.Heavy:
                            if (loc.temperature > 0)
                                loc.weather = Weather.Rain_Heavy;
                            else
                                loc.weather = Weather.Snowfall_Heavy;
                            continue;
                    }
        }
        private void updateWorldAmbience()
        {
            if (rand.Next(0, 10000) < 5) // 0.05%
            {
                int w = rand.Next(0, 100);
                if (w < 30)
                    precipitation = Precipitation.Clear;
                else if (w < 60)
                    precipitation = Precipitation.Cloudy;
                else if (w < 70)
                    precipitation = Precipitation.Weak;
                else if (w < 90)
                    precipitation = Precipitation.Medium;
                else
                    precipitation = Precipitation.Heavy;
                updateWorldWeather();
            }

            double sunShift = sunAmplitude * Math.Cos(2 * Math.PI * worldTime.DayOfYear / 365.0);
            sunrise = (int)Math.Round((sunHigh - 6 + sunShift) * 60 * 60);
            sunset = (int)Math.Round((sunHigh + 6 - sunShift) * 60 * 60);

            int dayTime = worldTime.Hour * 60 * 60 + worldTime.Minute * 60 + worldTime.Second;
            double dayProgress = dayTime / (24.0 * 60.0 * 60.0);
            double dailyCos1 = Math.Cos(Math.PI * (dayProgress - (sunriseRel + sunsetRel - 1.0) / 2.0) / (sunriseRel + 1.0 - sunsetRel));
            double dailyCos2 = Math.Cos(Math.PI * (dayProgress - (sunsetRel - sunriseRel) / 2.0 - sunriseRel) / (sunsetRel - sunriseRel));
            double dailyCos3 = Math.Cos(Math.PI * (dayProgress - (sunriseRel + 1.0 - sunsetRel) / 2.0 - sunsetRel) / (sunriseRel + 1.0 - sunsetRel));
            calculateLight(dayTime, dayProgress, dailyCos1, dailyCos2, dailyCos3);
            calculateTemperature(dayTime, dayProgress, dailyCos1, dailyCos2, dailyCos3);

            foreach (Location loc in Map)
            {
                loc.light = light * loc.lightCoef * globalLocationLightCoef;
                foreach (Path path in loc.paths)
                    if (loc.temperature > path.destination.temperature)
                        path.destination.temperatureDelta += (loc.temperature - path.destination.temperature) * path.heatTransferCoef * globalPathHeatTransferCoef;
            }
            Map = Map.OrderByDescending((Location loc) => { return loc.light; }).ToList();
            foreach (Location loc in Map)
            {
                foreach (Path path in loc.paths)
                    path.destination.light = Math.Max(path.destination.light, loc.light * path.lightTransferCoef * globalPathLightCoef);
                loc.updateTemperature();
                loc.temperature -= (loc.temperature - loc.temperatureShift - temperature) * loc.heatTransferCoef * globalLocationHeatTransferCoef;
            }
        }
        private void calculateLight(int dayTime, double dayProgress, double dailyCos1, double dailyCos2, double dailyCos3)
        {
            if (dayTime < sunrise)
                light = -0.05 * dailyCos1;
            else if (dayTime < sunset)
                light = 0.98 * (-Math.Cos(2.0 * Math.PI * (worldTime.DayOfYear + dayProgress) / 365) * 0.05 + 0.95) * Math.Sqrt(dailyCos2);
            else
                light = -0.05 * dailyCos3;
            light = (Math.Abs(light + 0.02) + 0.05) / 1.05;
        }
        private void calculateTemperature(int dayTime, double dayProgress, double dailyCos1, double dailyCos2, double dailyCos3)
        {
            double desiredTemperature = -1 * yearTempAmplitude * Math.Cos(2 * Math.PI * (worldTime.DayOfYear + dayProgress) / 365);
            if (dayTime < sunrise)
                desiredTemperature -= dayTempAmplitude * dailyCos1;
            else if (dayTime < sunset)
                desiredTemperature += dayTempAmplitude * dailyCos2;
            else
                desiredTemperature -= dayTempAmplitude * dailyCos3;
            temperature = Math.Min(Math.Max(temperature + (rand.NextDouble() - tempRandomDelta) * 2.0 * tempRandomSpeed, desiredTemperature - tempRandomDelta), desiredTemperature + tempRandomDelta);
        }
        private void loadMap()
        {
            Map = JsonConvert.DeserializeObject<List<Location>>(File.ReadAllText("World/Locations.map"), 
                new JsonSerializerSettings() { ContractResolver = new CustomContractResolver("map"), 
                    ReferenceLoopHandling = ReferenceLoopHandling.Serialize, PreserveReferencesHandling = PreserveReferencesHandling.All, Formatting = Formatting.Indented });
            startLocation = Map[0];
            foreach (Location loc in Map)
                loc.generateNoise();
        }
        //private void saveMap(string folder)
        //{
        //    File.WriteAllText(folder + "/Locations.map", JsonConvert.SerializeObject(Map, 
        //        new JsonSerializerSettings() { ContractResolver = new CustomContractResolver("map"), 
        //            ReferenceLoopHandling = ReferenceLoopHandling.Serialize, PreserveReferencesHandling = PreserveReferencesHandling.All, Formatting = Formatting.Indented }));
        //}
        private void saveGame()
        {

        }
        private void loadGame(string folder)
        {

        }
        private class CustomContractResolver : DefaultContractResolver
        {
            private string curCase;
            public CustomContractResolver(string curCase = "")
            {
                this.curCase = curCase;
            }
            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                List<MemberInfo> members = new List<MemberInfo>();
                foreach (FieldInfo field in objectType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    if (field.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                    {
                        JsonIgnoreWhenAttribute attr = field.GetCustomAttribute<JsonIgnoreWhenAttribute>();
                        if (curCase == "" || attr == null || attr.when != curCase)
                            members.Add(field);
                    }
                foreach (PropertyInfo property in objectType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    if (property.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                    {
                        JsonIgnoreWhenAttribute attr = property.GetCustomAttribute<JsonIgnoreWhenAttribute>();
                        if (curCase == "" || attr == null || attr.when != curCase)
                            members.Add(property);
                    }
                return members;
            }
        }
    }
}
