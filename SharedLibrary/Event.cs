﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using SharedLibrary.Objects;

namespace SharedLibrary
{
    [Serializable]
    public class Chat
    {
        public Chat(string O, String M, DateTime D)
        {
            Name = O;
            Message = M;
            Time = D;
            
        }

        public String Message { get; private set; }
        public DateTime Time { get; private set; }
        public string Name;
    }

    [Serializable]
    public struct RestEvent
    {
        public RestEvent(EventType Ty, EventVersion V, string M, string T, string O, string Ta)
        {
            Type = Ty;
            Version = V;
            Message = System.Web.HttpUtility.HtmlEncode(M);
            Title = T;
            Origin = System.Web.HttpUtility.HtmlEncode(O);
            Target = System.Web.HttpUtility.HtmlEncode(Ta);

            ID = Math.Abs(DateTime.Now.GetHashCode());
        }

        public enum EventType
        {
            NOTIFICATION,
            STATUS,
            ALERT,
        }

        public enum EventVersion
        {
            IW4MAdmin
        }

        public EventType Type;
        public EventVersion Version;
        public string Message;
        public string Title;
        public string Origin;
        public string Target;
        public int ID;
    }


    public class Event
    {
        public enum GType
        {
            //FROM SERVER
            Start,
            Stop,
            Connect,
            Disconnect,
            Say,
            MapChange,
            MapEnd,

            //FROM ADMIN
            Broadcast,
            Tell,
            Kick,
            Ban,
            Remote,
            Unknown,

            //FROM PLAYER
            Report,
            Flag,

            // FROM GAME
            Script,
            Kill,
            Death,
        }

        public Event(GType t, string d, Player O, Player T, Server S)
        {
            Type = t;
            Data = d.Trim();
            Origin = O;
            Target = T;
            Owner = S;
        }

        public static Event ParseEventString(String[] line, Server SV)
        {
#if DEBUG == false
            try
#endif
            {
                string  removeTime = Regex.Replace(line[0], @"[0-9]+:[0-9]+\ ", "");

                if (removeTime[0] == 'K')
                {
                    StringBuilder Data = new StringBuilder();
                    if (line.Length > 9)
                    {
                        for (int i = 9; i < line.Length; i++)
                            Data.Append(line[i] + ";");
                    }

                    if (!SV.CustomCallback)
                        return new Event(GType.Script, Data.ToString(), SV.ParseClientFromString(line, 6), SV.ParseClientFromString(line, 2), SV);
                }

                if (line[0].Substring(line[0].Length - 3).Trim() == "say")
                {
                    Regex rgx = new Regex("[^a-zA-Z0-9 -! -_]");
                    string message = rgx.Replace(line[4], "");
                    return new Event(GType.Say, message.StripColors(), SV.ParseClientFromString(line, 2), null, SV) { Message = message };
                }

                if (removeTime.Contains("ScriptKill"))
                {
                    return new Event(GType.Script, String.Join(";", line), SV.Players.First(p => p != null && p.NetworkId == line[1].ConvertLong()), SV.Players.First(p => p != null && p.NetworkId == line[2].ConvertLong()), SV);
                }

                if (removeTime.Contains("ExitLevel"))
                    return new Event(GType.MapEnd, line[0], null, null, SV);

                if (removeTime.Contains("InitGame"))
                    return new Event(GType.MapChange, line[0], null, null, SV);


                return null;
            }
#if DEBUG == false
            catch (Exception E)
            {
                SV.Manager.GetLogger().WriteError("Error requesting event " + E.Message);
                return null;
            }
#endif
        }


        public GType Type;
        public string Data; // Data is usually the message sent by player
        public string Message;
        public Player Origin;
        public Player Target;
        public Server Owner;
        public Boolean Remote = false;
    }
}
