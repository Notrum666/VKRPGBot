using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using VkNet;
using VkNet.Model;
using VkNet.Model.RequestParams;
using VkNet.Model.GroupUpdate;
using VkNet.Enums.SafetyEnums;
using System.Text.RegularExpressions;

namespace VKRPGBot
{
    class User
    {
        private VkApi api;
        private bool isRegistered;
        private long lastMessageId;
        public long userID { get; private set; }
        public Server.onMessageDel onMessageEvent;

        private bool awaitingInput = false;
        private string _inputData = "";
        private string inputData { get { return _inputData; } set { _inputData = value; awaitingInput = false; } }

        private bool slowMode = false;
        private int millisecondsForSlowmode = 1000;
        private int millisecondsDelay = 1500;
        private Stopwatch timeSinceLastMessage;
        private string messages = "";

        public User(long userID, VkApi api)
        {
            this.userID = userID;
            this.api = api;

            timeSinceLastMessage = new Stopwatch();
            timeSinceLastMessage.Start();

            isRegistered = false;

            _sendMessage(Translator.Get("registration.send_your_nickname"), false);
        }
        public void sendMessage(string msg, bool rememberMessage = false)
        {
            if (!rememberMessage)
            {
                if (slowMode)
                {
                    if (timeSinceLastMessage.ElapsedMilliseconds > millisecondsForSlowmode)
                        slowMode = false;
                    messages += '\n'+ msg;
                }
                else
                {
                    if (timeSinceLastMessage.ElapsedMilliseconds <= millisecondsForSlowmode)
                    {
                        timeSinceLastMessage.Restart();
                        slowMode = true;
                        messages += msg;
                        Thread.Sleep(millisecondsDelay);
                        _sendMessage(messages, false);
                        messages = "";
                    }
                    else
                    {
                        timeSinceLastMessage.Restart();
                        _sendMessage(msg, rememberMessage);
                    }
                }
            }
            else
                _sendMessage(msg, true);
        }
        private void _sendMessage(string msg, bool rememberMessage)
        {
            if (msg.Length > 4096)
            {
                if (rememberMessage)
                    lastMessageId = api.Messages.Send(new MessagesSendParams() { RandomId = Program.rng.Next(), UserId = userID, Message = msg.Substring(0, 4096) });
                else
                    api.Messages.Send(new MessagesSendParams() { RandomId = Program.rng.Next(), UserId = userID, Message = msg.Substring(0, 4096) });
                sendMessage(msg.Substring(4096).ToString());
            }
            else
            {
                if (rememberMessage)
                    lastMessageId = api.Messages.Send(new MessagesSendParams() { RandomId = Program.rng.Next(), UserId = userID, Message = msg });
                else
                    api.Messages.Send(new MessagesSendParams() { RandomId = Program.rng.Next(), UserId = userID, Message = msg });
            }
        }
        public void onMessage(string msg)
        {
            Thread thread = new Thread(new ParameterizedThreadStart(_onMessage));
            thread.Start(msg);
        }
        private void _onMessage(object msg)
        {
            if (awaitingInput)
            {
                inputData = (string)msg;
                return;
            }
            if (isRegistered)
            {
                onMessageEvent?.Invoke((string)msg);
            }
            else
            {
                string nickname = ((string)msg).Trim();
                if (nickname == "")
                {
                    _sendMessage(Translator.Get("registration.nickname_timeout"), false);
                    return;
                }
                Regex regex = new Regex("[^A-Za-zа-яА-Я_0-9]");
                while (regex.IsMatch(nickname))
                {
                    _sendMessage(Translator.Get("registration.nickname_invalid"), false);
                    nickname = ReadLine(60).Trim();
                    if (nickname == "")
                    {
                        _sendMessage(Translator.Get("registration.nickname_timeout"), false);
                        return;
                    }
                }
                _sendMessage(Translator.Get("registration.select_race") + " " + Races.ToString(), false);
                string raceName;
                Race race;
                while (!Races.TryParse(raceName = ReadLine(60), true, out race))
                {
                    if (raceName == "")
                    {
                        _sendMessage(Translator.Get("registration.race_timeout"), false);
                        return;
                    }
                    _sendMessage(Translator.Get("registration.race_invalid"), false);
                }
                _sendMessage(Translator.Get("registration.success"), false);
                isRegistered = true;
                Game.curGame.registerPlayer(this, nickname, race);
            }
        }
        public void markAsRead()
        {
            api.Messages.MarkAsRead(userID.ToString(), groupId: Convert.ToInt64(Server.curServer.groupID));
        }
        public void editLastMessage(string msg)
        {
            if (lastMessageId != 0)
                api.Messages.Edit(new MessageEditParams() { PeerId = userID, GroupId = Server.curServer.groupID, MessageId = lastMessageId, Message = msg });
        }
        public void deleteLastMessage()
        {
            if (lastMessageId != 0)
            {
                api.Messages.Delete(new ulong[] { (ulong)lastMessageId }, false, (ulong)Server.curServer.groupID, true);
                lastMessageId = 0;
            }
        }
        public string ReadLine(int waitingTime)
        {
            awaitingInput = true;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (awaitingInput)
                if (stopwatch.Elapsed.TotalSeconds >= waitingTime)
                    awaitingInput = false;
            stopwatch.Stop();
            return inputData;
        }
    }
    class Server
    {
        public static Server curServer;
        private VkApi api;
        public delegate void onMessageDel(string msg);
        public bool isAlive { get; private set; }
        public ulong groupID;
        public List<User> users = new List<User>();

        public void start(string token, ulong groupID)
        {
            curServer = this;

            api = new VkApi();
            api.Authorize(new ApiAuthParams() { AccessToken = token });

            this.groupID = groupID;

            isAlive = true;
            Thread mainThread = new Thread(new ParameterizedThreadStart(cycle));
            mainThread.Start(groupID);
        }
        public void stop()
        {
            isAlive = false;
        }
        private void cycle(object groupID)
        {
            LongPollServerResponse serverResponse = api.Groups.GetLongPollServer((ulong)groupID);
            string ts = serverResponse.Ts;
            while (isAlive)
            {
                BotsLongPollHistoryResponse historyResponce = api.Groups.GetBotsLongPollHistory(new BotsLongPollHistoryParams()
                { Server = serverResponse.Server, Ts = ts, Key = serverResponse.Key, Wait = 30 });
                if (historyResponce.Updates != null)
                    foreach (GroupUpdate update in historyResponce.Updates)
                        if (update.Type == GroupUpdateType.MessageNew)
                            onMessage((long)update.Message.UserId, update.Message.Body);
                ts = historyResponce.Ts;
            }
        }
        private void onMessage(long userID, string msg)
        {
            foreach (User user in users)
                if (user.userID == userID)
                {
                    user.onMessage(msg);
                    return;
                }
            if (msg.ToLower() == Translator.Get("commands.register"))
            {
                users.Add(new User(userID, api));
            }
            else
                api.Messages.Send(new MessagesSendParams() { RandomId = Program.rng.Next(), UserId = userID, Message = Translator.Get("messages.send_register") });
        }
    }
}
