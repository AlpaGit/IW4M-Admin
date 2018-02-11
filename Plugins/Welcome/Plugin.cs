﻿using System;
using SharedLibrary;
using System.Collections.Generic;
using SharedLibrary.Interfaces;
using System.Threading.Tasks;

using SharedLibrary.Network;
using SharedLibrary.Objects;

namespace Welcome_Plugin
{
    public class Plugin : IPlugin
    {
        String TimesConnected(Player P)
        {
            int connection = P.Connections;
            String Prefix = String.Empty;
            if (connection % 10 > 3 || connection % 10 == 0 || (connection % 100 > 9 && connection % 100 < 19))
                Prefix = "th";
            else
            {
                switch (connection % 10)
                {
                    case 1:
                        Prefix = "st";
                        break;
                    case 2:
                        Prefix = "nd";
                        break;
                    case 3:
                        Prefix = "rd";
                        break;
                }
            }

            switch (connection)
            {
                case 0:
                case 1:
                    return "first";
                case 2:
                    return "second";
                case 3:
                    return "third";
                case 4:
                    return "fourth";
                case 5:
                    return "fifth";
                case 100:
                    return "One-Hundreth (amazing!)";
                case 500:
                    return "you're really ^5dedicated ^7to this server! This is your ^5500th^7";
                case 1000:
                    return "you deserve a medal. it's your ^11000th^7";

                default:
                    return connection.ToString() + Prefix;
            }
        }

        public string Author => "RaidMax";

        public float Version => 1.0f;

        public string Name => "Welcome Plugin";

        public async Task OnLoadAsync(IManager manager)
        {
        }

        public async Task OnUnloadAsync()
        {
        }

        public async Task OnTickAsync(Server S)
        {
        }

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Connect)
            {
                Player newPlayer = E.Origin;

                if (newPlayer.Level >= Player.Permission.Trusted && !E.Origin.Masked)
                    await E.Owner.Broadcast(Utilities.ConvertLevelToColor(newPlayer.Level) + " ^5" + newPlayer.Name + " ^7has joined the server.");

                await newPlayer.Tell($"Welcome ^5{newPlayer.Name}^7, this is your ^5{TimesConnected(newPlayer)} ^7time connecting!");

                if (newPlayer.Level == Player.Permission.Flagged)
                    await E.Owner.ToAdmins($"^1NOTICE: ^7Flagged player ^5{newPlayer.Name}^7 has joined!");

                else
                {
                    try
                    {
                        CountryLookupProj.CountryLookup CLT = new CountryLookupProj.CountryLookup("Plugins/GeoIP.dat");
                        await E.Owner.Broadcast($"^5{newPlayer.Name} ^7hails from ^5{CLT.lookupCountryName(newPlayer.IPAddressString)}");
                    }

                    catch (Exception)
                    {
                        E.Owner.Manager.GetLogger().WriteError("Could not open file Plugins/GeoIP.dat for Welcome Plugin");
                    }

                }
            }

            if (E.Type == Event.GType.Disconnect)
            {
            }
        }
    }
}
